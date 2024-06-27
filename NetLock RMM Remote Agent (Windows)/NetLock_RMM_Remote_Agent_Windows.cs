using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using System.Threading;
using System.Net.Sockets;
using System.Text.Json;

namespace NetLock_RMM_Remote_Agent_Windows
{
    public partial class Service : ServiceBase
    {
        public static bool debug_mode = true;

        // Local Server
        private const int Port = 5000;
        private const string ServerIp = "127.0.0.1"; // Localhost
        private TcpClient local_server_client;
        private NetworkStream _stream;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        // Remote Server
        private HubConnection remote_server_client;
        private Timer remote_server_clientCheckTimer;
        bool remote_server_client_setup = false;

        // Device Identity
        public string device_identity = String.Empty;

        public Service()
        {
            InitializeComponent();
        }

        protected override async void OnStart(string[] args)
        {
            Logging.Handler.Debug("Service.OnStart", "Service started", "Information");

            //_ = Task.Run(async () => await Local_Server_Connect());
            
            await Local_Server_Connect();

            // Start the timer to check remote_server_client status every minute
            remote_server_clientCheckTimer = new Timer(async (e) => await Local_Server_Check_Connection_Status(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        }

        protected override void OnStop()
        {
            Logging.Handler.Debug("Service.OnStop", "Service stopped", "Information");
        }

        private async Task Local_Server_Connect()
        {
            try
            {
                local_server_client = new TcpClient();
                await local_server_client.ConnectAsync(ServerIp, Port);

                _stream = local_server_client.GetStream();
                _ = Local_Server_Handle_Server_Messages(_cancellationTokenSource.Token);

                Logging.Handler.Debug("Service.Local_Server_Connect", "Connected to the local server.", "");

                // Get the device identity from the local server
                await Local_Server_Send_Message("get_device_identity");
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("Service.Local_Server_Connect", "Failed to connect to the local server.", ex.ToString());
            }
        }

        private async Task Local_Server_Handle_Server_Messages(CancellationToken cancellationToken)
        {
            try
            {
                byte[] buffer = new byte[1024];

                while (!cancellationToken.IsCancellationRequested)
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0) // Server disconnected
                        break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Logging.Handler.Debug("Service.Local_Server_Handle_Server_Messages", "Received message", message);

                    // Split the message per $
                    string[] messageParts = message.Split('$');
                    
                    // device_identity
                    if (messageParts[0].ToString() == "device_identity")
                    {
                        Logging.Handler.Debug("Service.Local_Server_Handle_Server_Messages", "Device identity received", messageParts[1]);
                        
                        if (String.IsNullOrEmpty(messageParts[1]))
                            Logging.Handler.Error("Service.Local_Server_Handle_Server_Messages", "Device identity is empty or not ready yet.", "");
                        else
                            device_identity = messageParts[1];
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("Service.Local_Server_Handle_Server_Messages", "Failed to handle server messages.", ex.ToString());
            }
        }

        private async Task Local_Server_Send_Message(string message)
        {
            try
            {
                if (_stream != null && local_server_client.Connected)
                {
                    Logging.Handler.Debug("Service.Local_Server_Send_Message", "Sent message", message);

                    byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                    await _stream.WriteAsync(messageBytes, 0, messageBytes.Length);
                    await _stream.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("Service.Local_Server_Send_Message", "Failed to send message to the local server.", ex.ToString());
            }
        }

        private async Task Local_Server_Check_Connection_Status()
        {
            try
            {
                // Check if the local_server_client is connected
                if (local_server_client.Connected)
                {
                    Logging.Handler.Debug("Service.Check_Connection_Status", "Local server connection is active.", "");

                    // Get the device identity from the local server
                    await Local_Server_Send_Message("get_device_identity");

                    // Check if the remote_server_client is setup
                    if (!String.IsNullOrEmpty(device_identity) && !remote_server_client_setup) // If the device identity is not empty and the remote_server_client is not setup, setup the remote_server_client
                        await Setup_SignalR();
                    else if (!String.IsNullOrEmpty(device_identity) && remote_server_client.State == HubConnectionState.Disconnected) // If the device identity is not empty and the remote_server_client is disconnected, reconnect
                        await Remote_Connect();
                    else if (!String.IsNullOrEmpty(device_identity) && remote_server_client.State == HubConnectionState.Connected) // If the device identity is not empty and the remote_server_client is connected, do nothing
                        Logging.Handler.Debug("Service.Check_Connection_Status", "Remote server connection is active.", "");
                }
                else
                {
                    Logging.Handler.Debug("Service.Check_Connection_Status", "Local server connection lost, attempting to reconnect.", "");
                    await Local_Server_Connect();
                }

            }
            catch (Exception ex)
            {
                Logging.Handler.Error("Service.Check_Connection_Status", "Failed to check remote_server_client or local_server_client status.", ex.ToString());
            }
        }

        public async Task Setup_SignalR()
        {
            try
            {
                // Check if the device_identity is empty, if so, return
                if (String.IsNullOrEmpty(device_identity))
                {
                    Logging.Handler.Error("Service.Setup_SignalR", "Device identity is empty.", "");
                    return;
                }
                else
                    Logging.Handler.Debug("Service.Setup_SignalR", "Device identity is not empty. Preparing remote connection.", "");

                Logging.Handler.Debug("Service.Setup_SignalR", "Device identity JSON", device_identity);

                remote_server_client = new HubConnectionBuilder()
                    .WithUrl("http://localhost:7173/commandHub", options =>
                    {
                        options.Headers.Add("Device-Identity", Uri.EscapeDataString(device_identity));
                    })
                    .Build();

                remote_server_client.On<string>("ReceiveMessage", async (command) =>
                {
                    Logging.Handler.Debug("Service.Setup_SignalR", "Received command", command);

                    // Insert the logic here to execute the command
                    
                    // Example: If the command is "sync", send a message to the local server to force a sync with the remote server
                    if (command == "sync")
                        await Local_Server_Send_Message("sync");
                });

                remote_server_client.On<string>("SendMessageToClientAndWaitForResponse", async (command) =>
                {
                    Logging.Handler.Debug("Service.Setup_SignalR", "Received command", command);

                    // Insert the logic here to execute the command

                    // Example: If the command is "sync", send a message to the local server to force a sync with the remote server
                    if (command == "sync")
                        await Local_Server_Send_Message("sync");
                });

                // Start the connection
                await remote_server_client.StartAsync();

                remote_server_client_setup = true;

                Logging.Handler.Debug("Service.Setup_SignalR", "Connected to the remote server.", "");
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("Service.Setup_SignalR", "Failed to start SignalR.", ex.ToString());
            }
        }

        private async Task Remote_Connect()
        {
            try
            {
                // Check if the device_identity is empty, if so, return
                if (String.IsNullOrEmpty(device_identity))
                {
                    Logging.Handler.Error("Service.Remote_Connect", "Device identity is empty.", device_identity);
                    return;
                }
                else
                    Logging.Handler.Debug("Service.Remote_Connect", "Device identity is not empty. Trying to connect to remote server.", device_identity);

                // Connect to the remote server
                await remote_server_client.StartAsync();
                Logging.Handler.Debug("Service.Remote_Connect", "Connected to the server.", "");
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("Service.Remote_Connect", "Failed to connect to the server.", ex.ToString());
            }
        }
    }
}

