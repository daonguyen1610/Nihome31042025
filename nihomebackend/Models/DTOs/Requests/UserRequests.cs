using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class CreateUserRequest
{
    [Required, StringLength(20, MinimumLength = 8)]
    [RegularExpression(@"^[\d\+\-\s]+$", ErrorMessage = "Invalid phone number format")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;
}

public class UpdateUserRequest
{
    [StringLength(150, MinimumLength = 2)]
    public string? FullName { get; set; }

    [EmailAddress, StringLength(150, MinimumLength = 5)]
    public string? Email { get; set; }

    public string? Role { get; set; }

    public bool? IsActive { get; set; }
}
