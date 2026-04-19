using NihomeBackend.Extensions;
using NihomeBackend.Services;

var builder = WebApplication.CreateBuilder(args);

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

app.Run();
