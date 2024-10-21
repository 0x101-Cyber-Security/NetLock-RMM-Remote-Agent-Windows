using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NetLock_RMM_Remote_Agent_Windows
{
    public class SignalRHttpServerSingleton
    {
        private static SignalRHttpServerSingleton _instance;
        private static readonly object _lock = new object();
        private HttpListener _listener;
        private bool _isRunning;

        private SignalRHttpServerSingleton()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:7338/commandHub/");
        }

        public static SignalRHttpServerSingleton Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new SignalRHttpServerSingleton();
                    }
                    return _instance;
                }
            }
        }

        public async Task StartAsync()
        {
            if (_isRunning)
            {
                Logging.Handler.Debug("SignalRHttpServer", "StartAsync", "Server is already running.");
                return;
            }

            try
            {
                _listener.Start();
                _isRunning = true;

                Logging.Handler.Debug("SignalRHttpServer", "StartAsync", "Server started.");

                while (true)
                {
                    HttpListenerContext context = await _listener.GetContextAsync();
                    HttpListenerRequest request = context.Request;

                    // Handle the request (this is where SignalR processing would occur)
                    string responseString = "SignalR Server Running...";
                    byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                    context.Response.ContentLength64 = buffer.Length;
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    context.Response.OutputStream.Close();
                }
            }
            catch (Exception ex)
            {
                _isRunning = false;
                Logging.Handler.Error("SignalRHttpServer", "StartAsync", ex.ToString());
            }
        }

        public void Stop()
        {
            if (_listener != null && _isRunning)
            {
                _listener.Stop();
                _isRunning = false;
                Logging.Handler.Debug("SignalRHttpServer", "Stop", "Server stopped.");
            }
        }
    }

}
