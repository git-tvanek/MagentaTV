using MediatR;

namespace MagentaTV.Application.Events
{
    public class UserLoggedOutEvent : INotification
    {
        public string Username { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Reason { get; set; } = string.Empty; // Voluntary, Expired, Revoked
    }
}
