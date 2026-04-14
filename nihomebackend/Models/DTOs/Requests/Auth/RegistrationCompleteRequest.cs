using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests.Auth;

public class RegistrationCompleteRequest
{
    [Required(ErrorMessage = "Phone number is required")]
    [StringLength(20, MinimumLength = 8, ErrorMessage = "Phone number must be between 8 and 20 characters")]
    [RegularExpression(@"^[\d\+\-\s]+$", ErrorMessage = "Invalid phone number format")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
    public string Password { get; set; } = string.Empty;
}
