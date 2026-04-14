namespace NihomeBackend.Models.DTOs.Requests.Auth;

public class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
