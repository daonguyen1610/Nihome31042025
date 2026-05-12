namespace NihomeBackend.Models.DTOs.Requests;

public class UpdateOtpSettingsRequest
{
    public bool EnableOtpForRegistration { get; set; }

    public bool EnableOtpForForgotPassword { get; set; }
}
