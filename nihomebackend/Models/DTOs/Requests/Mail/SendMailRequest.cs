using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests.Mail;

public class SendMailRequest
{
    [Required(ErrorMessage = "Recipient email is required")]
    [EmailAddress(ErrorMessage = "Recipient email is invalid")]
    [StringLength(150, ErrorMessage = "Recipient email must not exceed 150 characters")]
    public string ToEmail { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "Subject must not exceed 255 characters")]
    public string? Subject { get; set; }

    [StringLength(10000, ErrorMessage = "HtmlBody must not exceed 10000 characters")]
    public string? HtmlBody { get; set; }
}
