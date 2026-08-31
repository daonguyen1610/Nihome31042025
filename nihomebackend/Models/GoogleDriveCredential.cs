namespace NihomeBackend.Models;

public class GoogleDriveCredential : IConcurrencyTracked
{
    public int Id { get; set; }
    public string ProtectedRefreshToken { get; set; } = string.Empty;
    public string? AccountEmail { get; set; }
    public int ConnectedByUserId { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}