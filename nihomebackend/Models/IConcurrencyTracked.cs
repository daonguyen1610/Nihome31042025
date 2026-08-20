namespace NihomeBackend.Models;

public interface IConcurrencyTracked
{
    byte[] RowVersion { get; set; }
}
