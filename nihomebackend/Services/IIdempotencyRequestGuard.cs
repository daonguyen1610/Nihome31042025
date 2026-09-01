using Microsoft.AspNetCore.Mvc;

namespace NihomeBackend.Services;

public interface IIdempotencyRequestGuard
{
    Task<IActionResult?> ValidateAsync(HttpContext context, CancellationToken ct = default);
}