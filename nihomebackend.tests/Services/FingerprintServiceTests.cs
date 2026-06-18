using Microsoft.AspNetCore.Http;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public class FingerprintServiceTests
{
    private readonly FingerprintService _sut = new();

    [Fact]
    public void Compute_SameInputs_ProducesSameHash()
    {
        var first = _sut.Compute(BuildContext("Mozilla/5.0", "1.2.3.4", "vi-VN"));
        var second = _sut.Compute(BuildContext("Mozilla/5.0", "1.2.3.4", "vi-VN"));

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void Compute_DifferentUserAgent_DifferentHash()
    {
        var a = _sut.Compute(BuildContext("Mozilla/5.0", "1.2.3.4", "vi-VN"));
        var b = _sut.Compute(BuildContext("curl/8.0", "1.2.3.4", "vi-VN"));

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Compute_DifferentIp_DifferentHash()
    {
        var a = _sut.Compute(BuildContext("Mozilla/5.0", "1.2.3.4", "vi-VN"));
        var b = _sut.Compute(BuildContext("Mozilla/5.0", "9.9.9.9", "vi-VN"));

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Compute_PrefersXForwardedForOverRemoteIp()
    {
        var ctx = BuildContext("Mozilla/5.0", "10.0.0.1", "vi-VN");
        ctx.Request.Headers["X-Forwarded-For"] = "203.0.113.5, 10.0.0.1";

        var withProxy = _sut.Compute(ctx);
        var direct = _sut.Compute(BuildContext("Mozilla/5.0", "203.0.113.5", "vi-VN"));

        Assert.Equal(direct, withProxy);
    }

    private static DefaultHttpContext BuildContext(string ua, string ip, string lang)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.UserAgent = ua;
        ctx.Request.Headers.AcceptLanguage = lang;
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip);
        return ctx;
    }
}
