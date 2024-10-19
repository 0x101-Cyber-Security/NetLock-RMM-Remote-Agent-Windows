using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLock_RMM_Remote_Agent_Windows
{
    public class RemoteServerClientSingleton
    {
        private static RemoteServerClientSingleton _instance;
        private static readonly object _lock = new object();
        public HubConnection Client { get; private set; }

        private RemoteServerClientSingleton()
        {
            //Client = CreateConnection("your_url_here", null);
        }

        public static RemoteServerClientSingleton Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new RemoteServerClientSingleton();
                    }
                    return _instance;
                }
            }
        }

        public void UpdateUrl(string newUrl, string deviceIdentity)
        {
            lock (_lock)
            {
                // Prüfen, ob die Client-Instanz bereits existiert und eine Verbindung besteht
                if (Client != null && Client.State == HubConnectionState.Connected)
                {
                    Client.StopAsync().Wait();
                }

                // Erstellen einer neuen HubConnection mit dem neuen URL und Device-Identity-Header
                Client = new HubConnectionBuilder()
                    .WithUrl(newUrl, options =>
                    {
                        options.Headers.Add("Device-Identity", Uri.EscapeDataString(deviceIdentity));
                    })
                    .Build();

                // Verbindung erneut starten
                Client.StartAsync().Wait();
            }
        }

        private HubConnection CreateConnection(string url, string deviceIdentity)
        {
            Logging.Handler.Debug("NetLock_RMM_Remote_Agent_Windows.RemoteServerClientSingleton.CreateConnection", "Creating new connection to " + url + " with deviceIdentity: " + deviceIdentity, "");

            var builder = new HubConnectionBuilder().WithUrl(url);

            // Set the headers if deviceIdentity is provided
            if (!string.IsNullOrEmpty(deviceIdentity))
            {
                builder = builder.WithUrl(url, options =>
                {
                    options.Headers.Add("Device-Identity", Uri.EscapeDataString(deviceIdentity));
                });
            }

            return builder.Build();
        }
    }
}
