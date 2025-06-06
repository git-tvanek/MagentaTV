namespace MagentaTV.Client.Models;

public class LoginDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
    public int? SessionDurationHours { get; set; }
}
