using NihomeBackend.Extensions;
using NihomeBackend.Services;

var currentDirectory = Directory.GetCurrentDirectory();

var frontendDistPath = Environment.GetEnvironmentVariable("NIHOMEWEB_DIST_PATH");
if (string.IsNullOrWhiteSpace(frontendDistPath))
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

builder.Services.AddOpenApiServices();
builder.Services.AddFrontendCors(builder.Configuration);
builder.Services.AddAuthAndEmail(builder.Configuration);
builder.Services.AddScoped<TimeService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseCors(FrontendCorsExtensions.PolicyName);
app.UseAuthentication();
app.UseAuthorization();

app.MigrateDatabase();

app.MapControllers();

app.MapFallbackToFile("{*path:regex(^(?!api($|/)).*$)}", "index.html");

app.Run();
