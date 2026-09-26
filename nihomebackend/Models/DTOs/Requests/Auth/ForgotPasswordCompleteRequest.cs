using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NihomeBackend.Models.DTOs.Requests.Auth;

public class ForgotPasswordCompleteRequest
{
    /// <summary>
    /// Phone number or email used to find the account. The JSON name remains
    /// <c>phoneNumber</c> so existing clients keep working.
    /// </summary>
    [Required(ErrorMessage = "Phone number or email is required")]
    [StringLength(150, ErrorMessage = "Phone number or email must be at most 150 characters")]
    [JsonPropertyName("phoneNumber")]
    public string Identifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
    public string NewPassword { get; set; } = string.Empty;
}
