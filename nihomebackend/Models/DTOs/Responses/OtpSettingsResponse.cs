namespace NihomeBackend.Models.DTOs.Responses;

public class OtpSettingsResponse
{
    public bool EnableOtpForRegistration { get; set; }

    public bool EnableOtpForForgotPassword { get; set; }
}
