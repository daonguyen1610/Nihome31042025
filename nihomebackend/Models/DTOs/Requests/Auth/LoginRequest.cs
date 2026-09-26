using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests.Auth;

public class LoginRequest
{
    /// <summary>
    /// Phone number or email. The JSON name stays <c>phoneNumber</c> so existing
    /// clients keep working.
    /// </summary>
    [Required(ErrorMessage = "Phone number or email is required")]
    [StringLength(150, ErrorMessage = "Phone number or email must be at most 150 characters")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
    public string Password { get; set; } = string.Empty;
}
