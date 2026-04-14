using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests.Auth;

public class VerifyOtpRequest
{
    [Required]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, MinLength(4), MaxLength(6)]
    public string OtpCode { get; set; } = string.Empty;
}
