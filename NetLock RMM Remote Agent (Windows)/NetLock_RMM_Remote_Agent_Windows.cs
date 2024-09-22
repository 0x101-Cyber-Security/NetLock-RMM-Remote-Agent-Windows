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
using System.IO;
using System.CodeDom;
using System.Xml.Linq;

namespace NetLock_RMM_Remote_Agent_Windows
{
    public partial class Service : ServiceBase
    {
        public static bool debug_mode = false;

        // Local Server
        private const int Port = 7337;
        private const string ServerIp = "127.0.0.1"; // Localhost
        private TcpClient local_server_client;
        private NetworkStream _stream;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        // Remote Server
        string remote_server_url = String.Empty;
        private HubConnection remote_server_client;
        private Timer remote_server_clientCheckTimer;
        bool remote_server_client_setup = false;

        // Device Identity
        public string device_identity = String.Empty;

        public class Device_Identity
        {
            public bool ssl { get; set; }
            public string agent_version { get; set; }
            public string package_guid { get; set; }
            public string device_name { get; set; }
            public string location_guid { get; set; }
            public string tenant_guid { get; set; }
            public string access_key { get; set; }
            public string hwid { get; set; }
            public string ip_address_internal { get; set; }
            public string operating_system { get; set; }
            public string domain { get; set; }
            public string antivirus_solution { get; set; }
            public string firewall_status { get; set; }
            public string architecture { get; set; }
            public string last_boot { get; set; }
            public string timezone { get; set; }
            public string cpu { get; set; }
            public string mainboard { get; set; }
            public string gpu { get; set; }
            public string ram { get; set; }
            public string tpm { get; set; }
        }

        public class Command_Entity
        {
            public int type { get; set; }
            public bool wait_response { get; set; }
            public string powershell_code { get; set; }
            public string response_id { get; set; }
            public int file_browser_command { get; set; }
            public string file_browser_path { get; set; }
            public string file_browser_path_move { get; set; }
            public string file_browser_file_content { get; set; }
            public string file_browser_file_guid { get; set; }
            public string command { get; set; }
        }

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
            remote_server_clientCheckTimer = new Timer(async (e) => await Local_Server_Check_Connection_Status(), null, TimeSpan.Zero, TimeSpan.FromSeconds(15));
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
                        {
                            device_identity = messageParts[1];
                            remote_server_url = $"{messageParts[2]}/commandHub";

                            Logging.Handler.Debug("Service.Local_Server_Handle_Server_Messages", "Device identity", device_identity);
                            Logging.Handler.Debug("Service.Local_Server_Handle_Server_Messages", "Remote server URL", remote_server_url);
                        }
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
                        Logging.Handler.Debug("Service.Check_Connection_Status", "Remote server connection is already active.", "");
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

                // Parse the JSON
                using (JsonDocument document = JsonDocument.Parse(device_identity))
                {
                    // Deserialise device identity
                    var jsonDocument = JsonDocument.Parse(device_identity);
                    var deviceIdentityElement = jsonDocument.RootElement.GetProperty("device_identity");

                    Device_Identity device_identity_object = JsonSerializer.Deserialize<Device_Identity>(deviceIdentityElement.ToString());

                    remote_server_client = new HubConnectionBuilder()
                    .WithUrl(remote_server_url, options =>
                    {
                        options.Headers.Add("Device-Identity", Uri.EscapeDataString(device_identity));
                    })
                    .Build();

                    remote_server_client.On<string>("ReceiveMessage", async (command) =>
                    {
                        Logging.Handler.Debug("Service.Setup_SignalR", "ReceiveMessage", command);

                        // Insert the logic here to execute the command

                        // Example: If the command is "sync", send a message to the local server to force a sync with the remote server
                        if (command == "sync")
                            await Local_Server_Send_Message("sync");
                    });

                    // Receive a message from the remote server, process the command and send a response back to the remote server
                    remote_server_client.On<string>("SendMessageToClientAndWaitForResponse", async (command) =>
                    {
                        Logging.Handler.Debug("Service.Setup_SignalR", "SendMessageToClientAndWaitForResponse", command);

                        // Deserialisation of the entire JSON string
                        Command_Entity command_object = JsonSerializer.Deserialize<Command_Entity>(command);
                        // Example: If the type is 0, execute the powershell code and send the response back to the remote server if wait_response is true

                        string result = string.Empty;

                        if (command_object.type == 0) // Remote Shell
                        {
                            result = Helper.PowerShell.Execute_Script(command_object.type.ToString(), command_object.powershell_code);
                            Logging.Handler.Debug("Client", "PowerShell executed", result);

                            // Send the response back to the server || Depreceated cause it always makes sense to wait for a response tho
                            /*if (wait_response)
                            {
                                Logging.Handler.Debug("Client", "Sending response back to the server", "result: " + result + "response_id: " + response_id);
                                await remote_server_client.InvokeAsync("ReceiveClientResponse", response_id, result);
                                Logging.Handler.Debug("Client", "Response sent back to the server", "result: " + result + "response_id: " + response_id);
                            }*/
                        }
                        else if (command_object.type == 1) // File Browser
                        {
                            // 0 = get drives, 1 = index, 2 = create dir, 3 = delete dir, 4 = move dir, 5 = rename dir, 6 = create file, 7 = delete file, 8 = move file, 9 = rename file, 10 = download file, 11 = upload file

                            // File Browser Command
                            try
                            {
                                if (command_object.file_browser_command == 0) // Get drives
                                {
                                    result = Helper.IO.Get_Drives();
                                }
                                if (command_object.file_browser_command == 1) // index
                                {
                                    // Get all directories and files in the specified path, create a json including date, size and file type
                                    var directoryDetails = await Helper.IO.Get_Directory_Index(command_object.file_browser_path);
                                    result = JsonSerializer.Serialize(directoryDetails, new JsonSerializerOptions { WriteIndented = true });
                                }
                                else if (command_object.file_browser_command == 2) // create dir
                                {
                                    result = Helper.IO.Create_Directory(command_object.file_browser_path);
                                }
                                else if (command_object.file_browser_command == 3) // delete dir
                                {
                                    result = Helper.IO.Delete_Directory(command_object.file_browser_path).ToString();
                                }
                                else if (command_object.file_browser_command == 4) // move dir
                                {
                                    result = Helper.IO.Move_Directory(command_object.file_browser_path, command_object.file_browser_path_move);
                                }
                                else if (command_object.file_browser_command == 5) // rename dir
                                {
                                    result = Helper.IO.Rename_Directory(command_object.file_browser_path, command_object.file_browser_path_move);
                                }
                                else if (command_object.file_browser_command == 6) // create file
                                {
                                    result = await Helper.IO.Create_File(command_object.file_browser_path, command_object.file_browser_file_content);
                                }
                                else if (command_object.file_browser_command == 7) // delete file
                                {
                                    result = Helper.IO.Delete_File(command_object.file_browser_path).ToString();
                                }
                                else if (command_object.file_browser_command == 8) // move file
                                {
                                    result = Helper.IO.Move_File(command_object.file_browser_path, command_object.file_browser_path_move);
                                }
                                else if (command_object.file_browser_command == 9) // rename file
                                {
                                    result = Helper.IO.Rename_File(command_object.file_browser_path, command_object.file_browser_path_move);
                                }
                                else if (command_object.file_browser_command == 10) // download file
                                {
                                    // download url with tenant guid, location guid & device name
                                    string download_url = "localhost:7080/admin/files/download/device" + "?guid=" + command_object.file_browser_file_guid + "&tenant_guid=" + device_identity_object.tenant_guid + "&location_guid=" + device_identity_object.location_guid + "&device_name=" + device_identity_object.device_name + "&access_key=" + device_identity_object.access_key + "&hwid=" + device_identity_object.hwid;

                                    Logging.Handler.Debug("Service.Setup_SignalR", "Download URL", download_url);
                                    result = await Helper.Http.DownloadFileAsync(device_identity_object.ssl, download_url, command_object.file_browser_path, device_identity_object.package_guid);

                                    Logging.Handler.Debug("Service.Setup_SignalR", "File downloaded", result);
                                }
                                else if (command_object.file_browser_command == 11) // upload file
                                {
                                    string file_name = Path.GetFileName(command_object.file_browser_path);

                                    // upload url with tenant guid, location guid & device name
                                    string upload_url = "localhost:7080/admin/files/upload/device" + "?tenant_guid=" + device_identity_object.tenant_guid + "&location_guid=" + device_identity_object.location_guid + "&device_name=" + device_identity_object.device_name + "&access_key=" + device_identity_object.access_key + "&hwid=" + device_identity_object.hwid;

                                    Logging.Handler.Debug("Service.Setup_SignalR", "Upload URL", upload_url);

                                    // Upload the file to the server
                                    result = await Helper.Http.UploadFileAsync(device_identity_object.ssl, upload_url, command_object.file_browser_path, device_identity_object.package_guid);
                                }
                            }
                            catch (Exception ex)
                            {
                                result = ex.Message;
                                Logging.Handler.Error("Service.Setup_SignalR", "Failed to execute file browser command.", ex.ToString());
                            }
                        }

                        // Send the response back to the server
                        if (!String.IsNullOrEmpty(command_object.type.ToString()))
                        {
                            Logging.Handler.Debug("Client", "Sending response back to the server", "result: " + result + "response_id: " + command_object.response_id);
                            await remote_server_client.InvokeAsync("ReceiveClientResponse", command_object.response_id, result);
                            Logging.Handler.Debug("Client", "Response sent back to the server", "result: " + result + "response_id: " + command_object.response_id);
                        }
                    });

                    // Start the connection
                    await remote_server_client.StartAsync();

                    remote_server_client_setup = true;

                    Logging.Handler.Debug("Service.Setup_SignalR", "Connected to the remote server.", "");
                }
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

