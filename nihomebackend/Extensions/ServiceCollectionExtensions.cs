using System.Text;
using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using NihomeBackend.Authorization;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOpenApiServices(this IServiceCollection services)
    {
        services.AddControllers(options =>
            {
                options.Filters.Add<PermissionAuthorizationFilter>();
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }

    public static IServiceCollection AddAuthAndEmail(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMemoryCache();

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql =>
                {
                    sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    sql.CommandTimeout(60);
                }));

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));

        var jwtSection = configuration.GetSection("Jwt");
        var activeKeyId = jwtSection["ActiveKeyId"];
        var activeKey = jwtSection.GetSection("Keys")[activeKeyId ?? string.Empty];
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(activeKey ?? "development-fallback-key-change-me"))
            };
        });

        services.AddAuthorization();
        services.AddAutoMapper(cfg => cfg.AddProfile<Mappings.AutoMapperProfile>());
        services.AddScoped<PasswordService>();
        services.AddScoped<JwtService>();
        services.AddScoped<RefreshTokenService>();
        services.AddScoped<OtpService>();
        services.AddScoped<UserService>();
        services.AddScoped<IdempotencyService>();
        services.AddSingleton<FingerprintService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IMasterDataService, MasterDataService>();
        services.AddScoped<IWorkflowConfigService, WorkflowConfigService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ILeadService, LeadService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IOpportunityService, OpportunityService>();
        services.AddScoped<IQuoteService, QuoteService>();
        services.AddScoped<IContractService, ContractService>();
        services.AddScoped<IContractAppendixService, ContractAppendixService>();
        services.AddScoped<IContractAttachmentService, ContractAttachmentService>();
        services.AddScoped<ICapabilityDocumentService, CapabilityDocumentService>();
        services.AddScoped<ITenderService, TenderService>();
        services.AddScoped<ISurveyService, SurveyService>();
        services.AddScoped<IDesignProjectService, DesignProjectService>();
        services.AddScoped<HostedImageService>();

        // Content services
        services.AddScoped<ActivityService>();
        services.AddScoped<ActivityCategoryService>();
        services.AddScoped<EmploymentTypeService>();
        services.AddScoped<RecruitmentDropdownOptionService>();
        services.AddScoped<JobPositionService>();
        services.AddScoped<JobApplicationService>();
        services.AddScoped<SiteSettingsService>();
        services.AddScoped<NewsService>();
        services.AddScoped<NewsCategoryService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<ProjectCategoryService>();
        services.AddScoped<ServiceItemService>();
        services.AddScoped<LogoService>();
        services.AddScoped<ProcessService>();
        services.AddScoped<SlideshowService>();
        services.AddScoped<AboutSectionService>();
        services.AddScoped<ContactMessageService>();

        // Translation services
        services.AddScoped<TranslationService>();
        services.AddScoped<EntityTranslationService>();

        services.AddHostedService<UploadedImageCleanupService>();

        // Audit logging (non-blocking queue + background writer + retention sweeper)
        services.AddHttpContextAccessor();
        services.AddSingleton<Services.Audit.AuditLogQueue>();
        services.AddSingleton<Services.Audit.IAuditLogger, Services.Audit.AuditLogger>();
        services.AddHostedService<Services.Audit.AuditLogWriterService>();
        services.AddHostedService<Services.Audit.AuditLogRetentionService>();

        return services;
    }
}
