using System.Text.Json;
using NihomeBackend.Services.Audit;

namespace nihomebackend.tests.Services;

public class SensitiveDataMaskerTests
{
    [Fact]
    public void Serialize_Null_ReturnsNull()
    {
        Assert.Null(SensitiveDataMasker.Serialize(null));
    }

    [Fact]
    public void Serialize_MasksPasswordField()
    {
        var input = new { username = "datran", password = "supersecret" };
        var json = SensitiveDataMasker.Serialize(input)!;
        var doc = JsonDocument.Parse(json);

        Assert.Equal("datran", doc.RootElement.GetProperty("username").GetString());
        Assert.Equal("***", doc.RootElement.GetProperty("password").GetString());
    }

    [Theory]
    [InlineData("token")]
    [InlineData("authToken")]
    [InlineData("apiKey")]
    [InlineData("api_key")]
    [InlineData("refreshToken")]
    [InlineData("authorization")]
    [InlineData("otp")]
    [InlineData("pin")]
    [InlineData("creditCard")]
    [InlineData("credit_card")]
    [InlineData("cvv")]
    [InlineData("cardNumber")]
    [InlineData("ssn")]
    [InlineData("privateKey")]
    [InlineData("salt")]
    [InlineData("hash")]
    public void Serialize_MasksKnownSensitiveKeys(string keyName)
    {
        var dict = new Dictionary<string, object> { [keyName] = "sensitive-value-here" };
        var json = SensitiveDataMasker.Serialize(dict)!;
        var doc = JsonDocument.Parse(json);

        Assert.Equal("***", doc.RootElement.GetProperty(keyName).GetString());
    }

    [Fact]
    public void Serialize_MasksNestedFields()
    {
        var input = new
        {
            user = new
            {
                name = "datran",
                credentials = new { password = "abc123", apiKey = "key-xxx" }
            }
        };
        var json = SensitiveDataMasker.Serialize(input)!;
        var doc = JsonDocument.Parse(json);

        var creds = doc.RootElement.GetProperty("user").GetProperty("credentials");
        Assert.Equal("***", creds.GetProperty("password").GetString());
        Assert.Equal("***", creds.GetProperty("apiKey").GetString());
        Assert.Equal("datran", doc.RootElement.GetProperty("user").GetProperty("name").GetString());
    }

    [Fact]
    public void Serialize_MasksFieldsInsideArrays()
    {
        var input = new
        {
            sessions = new[]
            {
                new { id = 1, token = "abc" },
                new { id = 2, token = "def" }
            }
        };
        var json = SensitiveDataMasker.Serialize(input)!;
        var doc = JsonDocument.Parse(json);

        var arr = doc.RootElement.GetProperty("sessions");
        foreach (var element in arr.EnumerateArray())
        {
            Assert.Equal("***", element.GetProperty("token").GetString());
        }
    }

    [Fact]
    public void Serialize_IsCaseInsensitive()
    {
        var input = new { PASSWORD = "x", Token = "y", AuthOrization = "z" };
        var json = SensitiveDataMasker.Serialize(input)!;
        Assert.Contains("\"***\"", json);
        Assert.DoesNotContain("\"x\"", json);
        Assert.DoesNotContain("\"y\"", json);
        Assert.DoesNotContain("\"z\"", json);
    }

    [Fact]
    public void Serialize_PreservesNonSensitiveFields()
    {
        var input = new { name = "datran", phone = "0335240370", role = "ADMIN" };
        var json = SensitiveDataMasker.Serialize(input)!;
        var doc = JsonDocument.Parse(json);

        Assert.Equal("datran", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("0335240370", doc.RootElement.GetProperty("phone").GetString());
        Assert.Equal("ADMIN", doc.RootElement.GetProperty("role").GetString());
    }

    [Fact]
    public void Serialize_TruncatesHugePayload()
    {
        var huge = new { blob = new string('a', 20_000) };
        var json = SensitiveDataMasker.Serialize(huge)!;

        Assert.EndsWith("...[truncated]", json);
        Assert.True(json.Length <= 8000 + "...[truncated]".Length);
    }

    [Fact]
    public void Serialize_SerializationFailure_ReturnsNull()
    {
        // A self-referencing object will fail to serialize with default options.
        var loop = new List<object>();
        loop.Add(loop);
        // Should not throw.
        var json = SensitiveDataMasker.Serialize(loop);
        Assert.Null(json);
    }
}
