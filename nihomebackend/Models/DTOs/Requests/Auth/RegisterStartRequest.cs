using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests.Auth;

public class RegisterStartRequest
{
    [Required(ErrorMessage = "Phone number is required")]
    [StringLength(20, MinimumLength = 8, ErrorMessage = "Phone number must be between 8 and 20 characters")]
    [RegularExpression(@"^[\d\+\-\s]+$", ErrorMessage = "Invalid phone number format")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 150 characters")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(150, MinimumLength = 5, ErrorMessage = "Email must be between 5 and 150 characters")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
    public string Password { get; set; } = string.Empty;
}
