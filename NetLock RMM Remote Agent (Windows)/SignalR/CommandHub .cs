using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Data;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace NetLock_RMM_Remote_Agent_Windows.SignalR
{
    public class CommandHub : Hub
    {    
        public class User_Identity
        {
            public string username { get; set; }
        }

        private readonly ConcurrentDictionary<string, bool> _clientConnections = new ConcurrentDictionary<string, bool>();

        public override Task OnConnectedAsync()
        {
            try
            {
                Logging.Handler.Debug("SignalR CommandHub", "OnConnectedAsync", "Client connected");

                var clientId = Context.ConnectionId;

                // Speichere clientId in deiner Datenstruktur
                _clientConnections.TryAdd(clientId, true);

                Logging.Handler.Debug("SignalR CommandHub", "OnConnectedAsync", $"Client connected with clientId {clientId}");
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("SignalR CommandHub", "OnConnectedAsync", ex.ToString());
            }

            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            try
            {
                Logging.Handler.Debug("SignalR CommandHub", "OnDisconnectedAsync", "Client disconnected");

                var clientId = Context.ConnectionId;

                // Remove the client from the data structure when it logs out
                _clientConnections.TryRemove(clientId, out _);

                Logging.Handler.Debug("SignalR CommandHub", "OnDisconnectedAsync", $"Client {clientId} disconnected");
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("SignalR CommandHub", "OnDisconnectedAsync", ex.ToString());
            }

            return base.OnDisconnectedAsync(exception);
        }

        public async Task ReceiveMessageFromClient(string message)
        {
            try
            {
                Logging.Handler.Debug("SignalR CommandHub", "ReceiveMessageFromClient", $"Message received from client: {message}");

                //await remote_server_client.InvokeAsync("ReceiveClientResponse", command_object.response_id, result);
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("SignalR CommandHub", "ReceiveMessageFromClient", ex.ToString());
            }
        }

        public async Task SendMessageToClient(string client_id, string command_json)
        {
            try
            {
                Logging.Handler.Debug("SignalR CommandHub", "SendMessageToClient", $"Sending command to client {client_id}: {command_json}");

                // Send the command to the client
                await Clients.Client(client_id).SendAsync("ReceiveMessage", command_json);

                Logging.Handler.Debug("SignalR CommandHub", "SendMessageToClient", $"Command sent to client {client_id}: {command_json}");
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("SignalR CommandHub", "SendMessageToClient", ex.ToString());
            }
        }

        public async Task SendMessageToClientAndWaitForResponse(string admin_identity_info_json, string client_id, string command_json)
        {
            try
            {
                Logging.Handler.Debug("SignalR CommandHub", "SendMessageToClientAndWaitForResponse", $"Sending command to client {client_id}: {command_json}");

                // Generate a unique responseId for the command
                var responseId = Guid.NewGuid().ToString();

                // Save responseId & admin_identity_info_json
                //_adminCommands.TryAdd(responseId, admin_identity_info_json);

                // Add the responseId to the command JSON
                //command_json = AddResponseIdToJson(command_json, responseId);

                Logging.Handler.Debug("SignalR CommandHub", "SendMessageToClientAndWaitForResponse", $"Modified command JSON with responseId: {command_json}");

                // Send the command to the client
                await Clients.Client(client_id).SendAsync("SendMessageToClientAndWaitForResponse", command_json);

                Logging.Handler.Debug("SignalR CommandHub", "SendMessageToClientAndWaitForResponse", $"Command sent to client {client_id}: {command_json}");
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("SignalR CommandHub", "SendMessageToClientAndWaitForResponse", ex.ToString());
            }
        }
    }
}

