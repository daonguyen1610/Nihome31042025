namespace NihomeBackend.Models.DTOs.Requests.Auth;

public class ForgotPasswordStartRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
}
