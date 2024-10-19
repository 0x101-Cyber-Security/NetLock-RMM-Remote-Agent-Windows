using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NetLock_RMM_Remote_Agent_Windows
{
    internal class SignalRHttpServer
    {
        public async Task StartAsync()
        {
            try
            {
                HttpListener listener = new HttpListener();
                listener.Prefixes.Add("http://localhost:7338/commandHub/");
                listener.Start();

                Console.WriteLine("SignalR HTTP Server is running...");

                while (true)
                {
                    HttpListenerContext context = await listener.GetContextAsync();
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
                Logging.Handler.Error("SignalRHttpServer", "StartAsync", ex.ToString());
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
