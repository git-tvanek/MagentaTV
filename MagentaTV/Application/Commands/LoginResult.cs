namespace MagentaTV.Application.Commands
{
    public class LoginResult
    {
        public bool Success { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
    }
}