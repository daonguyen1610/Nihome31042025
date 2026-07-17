using NihomeBackend.Extensions;
using NihomeBackend.Infrastructure;
using NihomeBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

var currentDirectory = Directory.GetCurrentDirectory();

var configuredFrontendDistPath = Environment.GetEnvironmentVariable("NIHOMEWEB_DIST_PATH");
string? frontendDistPath = null;

if (!string.IsNullOrWhiteSpace(configuredFrontendDistPath))
{
    var resolvedConfiguredFrontendDistPath = Path.IsPathRooted(configuredFrontendDistPath)
        ? configuredFrontendDistPath
        : Path.GetFullPath(Path.Combine(currentDirectory, configuredFrontendDistPath));

    if (!Directory.Exists(resolvedConfiguredFrontendDistPath))
    {
        throw new InvalidOperationException(
            $"NIHOMEWEB_DIST_PATH points to a non-existent directory: {resolvedConfiguredFrontendDistPath}");
    }

    frontendDistPath = resolvedConfiguredFrontendDistPath;
}
else
{
    var localFrontendDistPath = Path.GetFullPath(
        Path.Combine(currentDirectory, "..", "nihomeweb", "dist"));
    const string dockerFrontendDistPath = "/nihomeweb/dist";

    if (Directory.Exists(localFrontendDistPath))
    {
        frontendDistPath = localFrontendDistPath;
    }
    else if (Directory.Exists(dockerFrontendDistPath))
    {
        frontendDistPath = dockerFrontendDistPath;
    }
}

var appOptions = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = currentDirectory,
    WebRootPath = !string.IsNullOrWhiteSpace(frontendDistPath)
        ? frontendDistPath
        : null
};

var builder = WebApplication.CreateBuilder(appOptions);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    options.SingleLine = true;
});
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddDebug();
}

builder.Services.AddOpenApiServices();
builder.Services.AddFrontendCors(builder.Configuration);
builder.Services.AddAuthAndEmail(builder.Configuration);
builder.Services.AddScoped<TimeService>();

// Central exception handling: every uncaught exception flows through
// GlobalExceptionHandler which writes a ProblemDetails body and logs with
// the request's TraceIdentifier so an incident can be traced back to a
// single log line.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

// UseExceptionHandler must run before any middleware that may throw, so the
// handler can catch failures from auth, MVC, static files, etc.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

var uploadImagesPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "images", "upload");
Directory.CreateDirectory(uploadImagesPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadImagesPath),
    RequestPath = "/images/upload"
});

var uploadCvPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "files", "cv");
Directory.CreateDirectory(uploadCvPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadCvPath),
    RequestPath = "/files/cv"
});

var uploadCapabilityPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "files", "capability");
Directory.CreateDirectory(uploadCapabilityPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadCapabilityPath),
    RequestPath = "/files/capability"
});

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    var logger = context.RequestServices
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("ApiRequestLogger");

    var start = DateTime.UtcNow;
    logger.LogDebug("HTTP {Method} {Path}{QueryString} started", context.Request.Method, context.Request.Path, context.Request.QueryString);

    await next();

    var elapsedMs = (DateTime.UtcNow - start).TotalMilliseconds;
    logger.LogDebug(
        "HTTP {Method} {Path} finished {StatusCode} in {ElapsedMs} ms",
        context.Request.Method,
        context.Request.Path,
        context.Response.StatusCode,
        Math.Round(elapsedMs, 2));
});

app.UseRouting();
app.UseCors(FrontendCorsExtensions.PolicyName);
app.UseAuthentication();
app.UseAuthorization();

// In integration tests the WebApplicationFactory provisions and seeds the database itself,
// so we skip the production migrate/seed path that targets SQL Server.
if (!string.Equals(app.Environment.EnvironmentName, "IntegrationTests", StringComparison.OrdinalIgnoreCase))
{
    app.MigrateDatabase();
}

app.MapControllers();

app.MapFallbackToFile("{*path:regex(^(?!api($|/)).*$)}", "index.html");

app.Run();

public partial class Program { }
