namespace NihomeBackend.Models.DTOs.Requests.Auth;

public class ResendOtpRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
}
