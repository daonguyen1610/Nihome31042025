using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RegistrationOtp> RegistrationOtps => Set<RegistrationOtp>();
    public DbSet<SiteSettings> SiteSettings => Set<SiteSettings>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // Content
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ServiceItem> ServiceItems => Set<ServiceItem>();
    public DbSet<ClientLogo> ClientLogos => Set<ClientLogo>();
    public DbSet<ProcessDocument> ProcessDocuments => Set<ProcessDocument>();
    public DbSet<SlideshowItem> SlideshowItems => Set<SlideshowItem>();
    public DbSet<AboutSectionContent> AboutSectionContents => Set<AboutSectionContent>();
    public DbSet<ActivityCategory> ActivityCategories => Set<ActivityCategory>();

    // Recruitment
    public DbSet<JobPosition> JobPositions => Set<JobPosition>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<EmploymentType> EmploymentTypes => Set<EmploymentType>();

    // Contact
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    // Internationalization (i18n)
    public DbSet<Translation> Translations => Set<Translation>();
    public DbSet<EntityTranslation> EntityTranslations => Set<EntityTranslation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>().ToTable("users");
        modelBuilder.Entity<ApplicationUser>().HasKey(u => u.Id);
        modelBuilder.Entity<ApplicationUser>().HasIndex(u => u.PhoneNumber).IsUnique();
        modelBuilder.Entity<ApplicationUser>()
            .Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(50);

        modelBuilder.Entity<RefreshToken>().ToTable("refresh_tokens");
        modelBuilder.Entity<RefreshToken>().HasKey(rt => rt.Id);
        modelBuilder.Entity<RefreshToken>().HasIndex(rt => rt.Token).IsUnique();
        modelBuilder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RegistrationOtp>().ToTable("registration_otp");
        modelBuilder.Entity<RegistrationOtp>().HasKey(otp => otp.Id);
        modelBuilder.Entity<RegistrationOtp>().HasIndex(otp => otp.PhoneNumber);

        modelBuilder.Entity<SiteSettings>().ToTable("site_settings");
        modelBuilder.Entity<SiteSettings>().HasKey(settings => settings.Id);

        modelBuilder.Entity<Notification>().ToTable("notifications");
        modelBuilder.Entity<Notification>().HasKey(n => n.Id);
        modelBuilder.Entity<Notification>().Property(n => n.Module).HasMaxLength(50);
        modelBuilder.Entity<Notification>().Property(n => n.Title).HasMaxLength(200);
        modelBuilder.Entity<Notification>().Property(n => n.Body).HasMaxLength(1000);
        modelBuilder.Entity<Notification>().Property(n => n.LinkUrl).HasMaxLength(500);
        modelBuilder.Entity<Notification>().HasIndex(n => new { n.UserId, n.CreatedAt, n.Id });
        modelBuilder.Entity<Notification>().HasIndex(n => new { n.UserId, n.IsRead });
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Content tables
        modelBuilder.Entity<Activity>().ToTable("activities");
        modelBuilder.Entity<Activity>().HasKey(a => a.Id);
        modelBuilder.Entity<Activity>().HasIndex(a => a.Slug).IsUnique();

        modelBuilder.Entity<NewsArticle>().ToTable("news_articles");
        modelBuilder.Entity<NewsArticle>().HasKey(n => n.Id);
        modelBuilder.Entity<NewsArticle>().HasIndex(n => n.Slug).IsUnique();

        modelBuilder.Entity<Project>().ToTable("projects");
        modelBuilder.Entity<Project>().HasKey(p => p.Id);
        modelBuilder.Entity<Project>().HasIndex(p => p.Slug).IsUnique();

        modelBuilder.Entity<ServiceItem>().ToTable("service_items");
        modelBuilder.Entity<ServiceItem>().HasKey(s => s.Id);
        modelBuilder.Entity<ServiceItem>().HasIndex(s => s.Slug).IsUnique();

        modelBuilder.Entity<ClientLogo>().ToTable("client_logos");
        modelBuilder.Entity<ClientLogo>().HasKey(l => l.Id);
        modelBuilder.Entity<ClientLogo>()
            .Property(l => l.Kind)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<ProcessDocument>().ToTable("process_documents");
        modelBuilder.Entity<ProcessDocument>().HasKey(p => p.Id);
        modelBuilder.Entity<ProcessDocument>().HasIndex(p => p.GroupKey);

        modelBuilder.Entity<SlideshowItem>().ToTable("slideshow_items");
        modelBuilder.Entity<SlideshowItem>().HasKey(s => s.Id);
        modelBuilder.Entity<SlideshowItem>().HasIndex(s => s.Slug).IsUnique();

        modelBuilder.Entity<AboutSectionContent>().ToTable("about_section_contents");
        modelBuilder.Entity<AboutSectionContent>().HasKey(a => a.Id);
        modelBuilder.Entity<AboutSectionContent>().HasIndex(a => a.Slug).IsUnique();

        modelBuilder.Entity<ActivityCategory>().ToTable("activity_categories");
        modelBuilder.Entity<ActivityCategory>().HasKey(c => c.Id);
        modelBuilder.Entity<ActivityCategory>().HasIndex(c => c.Name).IsUnique();

        modelBuilder.Entity<JobPosition>().ToTable("job_positions");
        modelBuilder.Entity<JobPosition>().HasKey(j => j.Id);

        modelBuilder.Entity<JobApplication>().ToTable("job_applications");
        modelBuilder.Entity<JobApplication>().HasKey(a => a.Id);
        modelBuilder.Entity<JobApplication>()
            .HasOne(a => a.JobPosition)
            .WithMany(j => j.Applications)
            .HasForeignKey(a => a.JobPositionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EmploymentType>().ToTable("employment_types");
        modelBuilder.Entity<EmploymentType>().HasKey(e => e.Id);
        modelBuilder.Entity<EmploymentType>().HasIndex(e => e.Code).IsUnique();

        modelBuilder.Entity<ContactMessage>().ToTable("contact_messages");
        modelBuilder.Entity<ContactMessage>().HasKey(c => c.Id);

        // i18n tables
        modelBuilder.Entity<Translation>().ToTable("translations");
        modelBuilder.Entity<Translation>().HasKey(t => t.TranslationId);

        modelBuilder.Entity<EntityTranslation>().ToTable("entity_translations");
        modelBuilder.Entity<EntityTranslation>().HasKey(t => t.Id);
    }
}
