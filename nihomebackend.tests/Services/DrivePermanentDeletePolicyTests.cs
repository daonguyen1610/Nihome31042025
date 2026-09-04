using NihomeBackend.Services.GoogleDrive;

namespace nihomebackend.tests.Services;

public sealed class DrivePermanentDeletePolicyTests
{
    private static readonly Dictionary<string, string> ExpectedProperties = new()
    {
        ["niconReplicaKey"] = "document:42",
    };

    [Fact]
    public void EnsureOwned_MatchingInstancePropertiesParentAndCapabilities_IsAccepted()
    {
        DrivePermanentDeletePolicy.EnsureOwned(Item(), "nicon-production", ExpectedProperties, "parent-1");
    }

    [Theory]
    [InlineData("other-instance", "drive_instance_mismatch")]
    [InlineData("nicon-production", "drive_properties_mismatch")]
    public void EnsureOwned_MismatchedIdentity_IsRejected(string instanceId, string expectedCode)
    {
        var expected = expectedCode == "drive_properties_mismatch"
            ? new Dictionary<string, string> { ["niconReplicaKey"] = "other" }
            : ExpectedProperties;

        var exception = Assert.Throws<DrivePermanentDeleteRejectedException>(() =>
            DrivePermanentDeletePolicy.EnsureOwned(Item(), instanceId, expected, "parent-1"));

        Assert.Equal(expectedCode, exception.Code);
    }

    [Fact]
    public void EnsureOwned_SharedOrImportedItem_IsRejected()
    {
        var item = Item() with { IsOwnedByMe = false };

        var exception = Assert.Throws<DrivePermanentDeleteRejectedException>(() =>
            DrivePermanentDeletePolicy.EnsureOwned(item, "nicon-production", ExpectedProperties, "parent-1"));

        Assert.Equal("drive_not_exclusively_owned", exception.Code);
    }

    [Fact]
    public void EnsureOwned_UnknownCallerProperties_IsRejected()
    {
        var exception = Assert.Throws<DrivePermanentDeleteRejectedException>(() =>
            DrivePermanentDeletePolicy.EnsureOwned(Item(), "nicon-production",
                new Dictionary<string, string>(), "parent-1"));

        Assert.Equal("drive_ownership_unknown", exception.Code);
    }

    [Fact]
    public void EnsureOwned_ExpectedParentOutsideCurrentParents_IsRejected()
    {
        var exception = Assert.Throws<DrivePermanentDeleteRejectedException>(() =>
            DrivePermanentDeletePolicy.EnsureOwned(Item(), "nicon-production", ExpectedProperties, "other-parent"));

        Assert.Equal("drive_parent_mismatch", exception.Code);
    }

    private static DriveItem Item() => new(
        "file-1",
        "drawing.pdf",
        "application/pdf",
        10,
        "1",
        DateTime.UtcNow,
        null,
        new Dictionary<string, string>
        {
            ["niconInstance"] = "nicon-production",
            ["niconReplicaKey"] = "document:42",
        },
        false,
        ["parent-1"],
        true,
        true);
}