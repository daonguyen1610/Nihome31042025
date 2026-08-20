namespace NihomeBackend.Models.DTOs.Requests;

public interface IConcurrencyRequest
{
    string? RowVersion { get; set; }
}

public class ConcurrencyRequest : IConcurrencyRequest
{
    public string? RowVersion { get; set; }
}
