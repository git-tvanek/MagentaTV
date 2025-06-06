using Microsoft.AspNetCore.SignalR;

namespace MagentaTV.Hubs
{
    /// <summary>
    /// SignalR hub used to broadcast simple notifications to all connected
    /// clients. At the moment it only exposes a single method that relays a
    /// message to every subscriber.
    /// </summary>
    public class NotificationHub : Hub
    {
        /// <summary>
        /// Sends a chat style message to all connected clients.
        /// </summary>
        /// <param name="user">Name of the sender.</param>
        /// <param name="message">Message text.</param>
        public async Task SendMessage(string user, string message) =>
            await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}
