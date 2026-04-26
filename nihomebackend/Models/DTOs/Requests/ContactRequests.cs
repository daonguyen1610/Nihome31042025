using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class SubmitContactRequest
{
    [Required, StringLength(150)]
    public string Name { get; set; } = "";

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = "";

    [StringLength(30)]
    public string? Phone { get; set; }

    [Required, StringLength(255)]
    public string Subject { get; set; } = "";

    [Required, StringLength(5000)]
    public string Message { get; set; } = "";
}

public class ReplyContactRequest
{
    [Required, StringLength(10000)]
    public string ReplyContent { get; set; } = "";
}
