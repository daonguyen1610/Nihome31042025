using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NihomeBackend.Models.DTOs.Requests.Auth;

public class ForgotPasswordStartRequest
{
    /// <summary>
    /// Phone number or email used to find the account. The JSON name remains
    /// <c>phoneNumber</c> so existing clients keep working.
    /// </summary>
    [Required(ErrorMessage = "Phone number or email is required")]
    [StringLength(150, ErrorMessage = "Phone number or email must be at most 150 characters")]
    [JsonPropertyName("phoneNumber")]
    public string Identifier { get; set; } = string.Empty;
}
