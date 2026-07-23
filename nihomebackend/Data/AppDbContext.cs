using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Models.Rbac;

namespace NihomeBackend.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RegistrationOtp> RegistrationOtps => Set<RegistrationOtp>();
    public DbSet<SiteSettings> SiteSettings => Set<SiteSettings>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<UserDocument> UserDocuments => Set<UserDocument>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    // RBAC
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

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
    public DbSet<NewsCategory> NewsCategories => Set<NewsCategory>();
    public DbSet<ProjectCategory> ProjectCategories => Set<ProjectCategory>();

    // Recruitment
    public DbSet<JobPosition> JobPositions => Set<JobPosition>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<EmploymentType> EmploymentTypes => Set<EmploymentType>();
    public DbSet<RecruitmentDropdownOption> RecruitmentDropdownOptions => Set<RecruitmentDropdownOption>();

    // Contact
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    // Master data — generic category-driven lookup for CRM / Design / Permit modules.
    public DbSet<MasterDataOption> MasterDataOptions => Set<MasterDataOption>();

    // Workflow — reusable approval flow definitions (NIH-225 config, runtime later).
    public DbSet<WorkflowConfig> WorkflowConfigs => Set<WorkflowConfig>();

    // CRM — M1 chain (Lead → Customer → Opportunity → Quote/Bid → Contract)
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<LeadActivity> LeadActivities => Set<LeadActivity>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerContact> CustomerContacts => Set<CustomerContact>();
    public DbSet<CustomerActivity> CustomerActivities => Set<CustomerActivity>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<OpportunityActivity> OpportunityActivities => Set<OpportunityActivity>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteItem> QuoteItems => Set<QuoteItem>();
    public DbSet<QuoteApprovalLog> QuoteApprovalLogs => Set<QuoteApprovalLog>();
    public DbSet<QuoteVersionSnapshot> QuoteVersionSnapshots => Set<QuoteVersionSnapshot>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractPaymentMilestone> ContractPaymentMilestones => Set<ContractPaymentMilestone>();
    public DbSet<ContractAppendix> ContractAppendices => Set<ContractAppendix>();
    public DbSet<ContractAttachment> ContractAttachments => Set<ContractAttachment>();
    public DbSet<CapabilityDocument> CapabilityDocuments => Set<CapabilityDocument>();
    public DbSet<CapabilityDocumentVersion> CapabilityDocumentVersions => Set<CapabilityDocumentVersion>();
    public DbSet<Tender> Tenders => Set<Tender>();
    public DbSet<TenderChecklistItem> TenderChecklistItems => Set<TenderChecklistItem>();

    public DbSet<Survey> Surveys => Set<Survey>();

    public DbSet<DesignProject> DesignProjects => Set<DesignProject>();

    public DbSet<PermitChecklistItem> PermitChecklistItems => Set<PermitChecklistItem>();

    public DbSet<ConceptOption> ConceptOptions => Set<ConceptOption>();

    public DbSet<BasicDesignDoc> BasicDesignDocs => Set<BasicDesignDoc>();

    public DbSet<ShopDrawing> ShopDrawings => Set<ShopDrawing>();

    public DbSet<DrawingRevision> DrawingRevisions => Set<DrawingRevision>();

    public DbSet<IfcRelease> IfcReleases => Set<IfcRelease>();
    public DbSet<IfcReleaseItem> IfcReleaseItems => Set<IfcReleaseItem>();
    public DbSet<IfcReleaseRecipient> IfcReleaseRecipients => Set<IfcReleaseRecipient>();

    public DbSet<ConstructionTask> ConstructionTasks => Set<ConstructionTask>();
    public DbSet<ConstructionTaskDependency> ConstructionTaskDependencies => Set<ConstructionTaskDependency>();
    public DbSet<AcceptanceRecord> AcceptanceRecords => Set<AcceptanceRecord>();

    public DbSet<SiteDiary> SiteDiaries => Set<SiteDiary>();

    public DbSet<PunchItem> PunchItems => Set<PunchItem>();

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
            .Property(u => u.Email)
            .HasMaxLength(150)
            .IsRequired();
        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_users_Email_Unique");
        modelBuilder.Entity<ApplicationUser>()
            .Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(50);
        modelBuilder.Entity<ApplicationUser>()
            .HasOne(u => u.RoleEntity)
            .WithMany()
            .HasForeignKey(u => u.RoleEntityId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Role>().ToTable("roles");
        modelBuilder.Entity<Role>().HasKey(r => r.Id);
        modelBuilder.Entity<Role>().HasIndex(r => r.Code).IsUnique();
        modelBuilder.Entity<Role>().Property(r => r.Code).HasMaxLength(50).IsRequired();
        modelBuilder.Entity<Role>().Property(r => r.Name).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<Role>().Property(r => r.LabelKey).HasMaxLength(150);
        modelBuilder.Entity<Role>().Property(r => r.DescriptionKey).HasMaxLength(150);

        modelBuilder.Entity<Permission>().ToTable("permissions");
        modelBuilder.Entity<Permission>().HasKey(p => p.Id);
        modelBuilder.Entity<Permission>().Property(p => p.Module).HasMaxLength(60).IsRequired();
        modelBuilder.Entity<Permission>().Property(p => p.Action).HasMaxLength(60).IsRequired();
        modelBuilder.Entity<Permission>().Property(p => p.DescriptionKey).HasMaxLength(150);
        modelBuilder.Entity<Permission>().Ignore(p => p.Code);
        modelBuilder.Entity<Permission>().HasIndex(p => new { p.Module, p.Action }).IsUnique();

        modelBuilder.Entity<RolePermission>().ToTable("role_permissions");
        modelBuilder.Entity<RolePermission>().HasKey(rp => rp.Id);
        modelBuilder.Entity<RolePermission>().HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();
        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

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

        modelBuilder.Entity<IdempotencyRecord>().ToTable("idempotency_records");
        modelBuilder.Entity<IdempotencyRecord>().HasKey(r => r.Id);
        modelBuilder.Entity<IdempotencyRecord>()
            .HasIndex(r => new { r.Scope, r.Key })
            .IsUnique()
            .HasDatabaseName("IX_idempotency_records_Scope_Key");
        modelBuilder.Entity<IdempotencyRecord>().HasIndex(r => r.ExpiresAt);
        modelBuilder.Entity<IdempotencyRecord>().Property(r => r.ResponseJson).HasColumnType("nvarchar(max)");

        modelBuilder.Entity<SiteSettings>().ToTable("site_settings");
        modelBuilder.Entity<SiteSettings>().HasKey(settings => settings.Id);
        modelBuilder.Entity<SiteSettings>().Property(s => s.MapEmbedUrl).HasMaxLength(1000);

        modelBuilder.Entity<Notification>().ToTable("notifications");
        modelBuilder.Entity<Notification>().HasKey(n => n.Id);
        // Kept in sync with NotificationTemplate.Module so template-driven
        // notifications can persist the template's module verbatim.
        modelBuilder.Entity<Notification>().Property(n => n.Module).HasMaxLength(80);
        modelBuilder.Entity<Notification>().Property(n => n.TemplateCode).HasMaxLength(80);
        modelBuilder.Entity<Notification>().Property(n => n.RefEntityType).HasMaxLength(80);
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

        modelBuilder.Entity<NotificationTemplate>().ToTable("notification_templates");
        modelBuilder.Entity<NotificationTemplate>().HasKey(t => t.Id);
        modelBuilder.Entity<NotificationTemplate>().HasIndex(t => t.Code).IsUnique();
        modelBuilder.Entity<NotificationTemplate>().HasIndex(t => t.Module);

        modelBuilder.Entity<AuditLog>().ToTable("audit_logs");
        modelBuilder.Entity<AuditLog>().HasKey(a => a.Id);
        modelBuilder.Entity<AuditLog>().Property(a => a.AuditId).HasMaxLength(40).IsRequired();
        modelBuilder.Entity<AuditLog>().Property(a => a.Action).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<AuditLog>().Property(a => a.ResourceType).HasMaxLength(80).IsRequired();
        modelBuilder.Entity<AuditLog>().Property(a => a.ResourceId).HasMaxLength(100);
        modelBuilder.Entity<AuditLog>().Property(a => a.Message).HasMaxLength(500).IsRequired();
        modelBuilder.Entity<AuditLog>().Property(a => a.ActorType).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<AuditLog>().Property(a => a.ActorPhone).HasMaxLength(30);
        modelBuilder.Entity<AuditLog>().Property(a => a.ActorRole).HasMaxLength(30);
        modelBuilder.Entity<AuditLog>().Property(a => a.SourceSystem).HasMaxLength(40).IsRequired();
        modelBuilder.Entity<AuditLog>().Property(a => a.TargetSystem).HasMaxLength(40);
        modelBuilder.Entity<AuditLog>().Property(a => a.Channel).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<AuditLog>().Property(a => a.IpAddress).HasMaxLength(64);
        modelBuilder.Entity<AuditLog>().Property(a => a.UserAgent).HasMaxLength(300);
        modelBuilder.Entity<AuditLog>().Property(a => a.Status).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<AuditLog>().Property(a => a.FailureReason).HasMaxLength(500);
        modelBuilder.Entity<AuditLog>().Property(a => a.CorrelationId).HasMaxLength(80);
        modelBuilder.Entity<AuditLog>().Property(a => a.RequestId).HasMaxLength(80);
        modelBuilder.Entity<AuditLog>().Property(a => a.OldValueJson).HasColumnType("nvarchar(max)");
        modelBuilder.Entity<AuditLog>().Property(a => a.NewValueJson).HasColumnType("nvarchar(max)");
        modelBuilder.Entity<AuditLog>().Property(a => a.MetadataJson).HasColumnType("nvarchar(max)");
        modelBuilder.Entity<AuditLog>().HasIndex(a => a.AuditId).IsUnique();
        modelBuilder.Entity<AuditLog>().HasIndex(a => a.CreatedAt);
        modelBuilder.Entity<AuditLog>().HasIndex(a => new { a.Action, a.CreatedAt });
        modelBuilder.Entity<AuditLog>().HasIndex(a => new { a.ActorUserId, a.CreatedAt });
        modelBuilder.Entity<AuditLog>().HasIndex(a => new { a.ResourceType, a.ResourceId });
        modelBuilder.Entity<AuditLog>().HasIndex(a => a.CorrelationId);
        modelBuilder.Entity<AuditLog>().HasIndex(a => a.Status);

        modelBuilder.Entity<UserDocument>().ToTable("user_documents");
        modelBuilder.Entity<UserDocument>().HasKey(d => d.Id);
        modelBuilder.Entity<UserDocument>()
            .Property(d => d.DocumentType)
            .HasConversion<string>()
            .HasMaxLength(20);
        modelBuilder.Entity<UserDocument>().HasIndex(d => new { d.UserId, d.CreatedAt });
        modelBuilder.Entity<UserDocument>()
            .HasOne(d => d.User)
            .WithMany(u => u.Documents)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Content tables
        modelBuilder.Entity<Activity>().ToTable("activities");
        modelBuilder.Entity<Activity>().HasKey(a => a.Id);
        modelBuilder.Entity<Activity>().HasIndex(a => a.Slug).IsUnique();
        modelBuilder.Entity<Activity>()
            .HasOne(a => a.CategoryRef)
            .WithMany()
            .HasForeignKey(a => a.ActivityCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<NewsArticle>().ToTable("news_articles");
        modelBuilder.Entity<NewsArticle>().HasKey(n => n.Id);
        modelBuilder.Entity<NewsArticle>().HasIndex(n => n.Slug).IsUnique();
        modelBuilder.Entity<NewsArticle>()
            .HasOne(n => n.CategoryRef)
            .WithMany()
            .HasForeignKey(n => n.NewsCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Project>().ToTable("projects");
        modelBuilder.Entity<Project>().HasKey(p => p.Id);
        modelBuilder.Entity<Project>().HasIndex(p => p.Slug).IsUnique();
        modelBuilder.Entity<Project>()
            .HasOne(p => p.CategoryRef)
            .WithMany()
            .HasForeignKey(p => p.ProjectCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

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

        modelBuilder.Entity<ProjectCategory>().ToTable("project_categories");
        modelBuilder.Entity<ProjectCategory>().HasKey(c => c.Id);
        modelBuilder.Entity<ProjectCategory>().HasIndex(c => c.Name).IsUnique();
        modelBuilder.Entity<SlideshowItem>().HasKey(s => s.Id);
        modelBuilder.Entity<SlideshowItem>().HasIndex(s => s.Slug).IsUnique();

        modelBuilder.Entity<AboutSectionContent>().ToTable("about_section_contents");
        modelBuilder.Entity<AboutSectionContent>().HasKey(a => a.Id);
        modelBuilder.Entity<AboutSectionContent>().HasIndex(a => a.Slug).IsUnique();

        modelBuilder.Entity<ActivityCategory>().ToTable("activity_categories");
        modelBuilder.Entity<ActivityCategory>().HasKey(c => c.Id);
        modelBuilder.Entity<ActivityCategory>().HasIndex(c => c.Name).IsUnique();

        modelBuilder.Entity<NewsCategory>().ToTable("news_categories");
        modelBuilder.Entity<NewsCategory>().HasKey(c => c.Id);
        modelBuilder.Entity<NewsCategory>().HasIndex(c => c.Name).IsUnique();

        modelBuilder.Entity<JobPosition>().ToTable("job_positions");
        modelBuilder.Entity<JobPosition>().HasKey(j => j.Id);

        modelBuilder.Entity<JobApplication>().ToTable("job_applications");
        modelBuilder.Entity<JobApplication>().HasKey(a => a.Id);
        modelBuilder.Entity<JobApplication>()
            .HasOne(a => a.JobPosition)
            .WithMany(j => j.Applications)
            .HasForeignKey(a => a.JobPositionId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<JobApplication>()
            .Property(a => a.Status)
            .HasMaxLength(32);
        modelBuilder.Entity<JobApplication>().HasIndex(a => a.Status);
        modelBuilder.Entity<JobApplication>().HasIndex(a => a.AppliedAt);

        modelBuilder.Entity<EmploymentType>().ToTable("employment_types");
        modelBuilder.Entity<EmploymentType>().HasKey(e => e.Id);
        modelBuilder.Entity<EmploymentType>().HasIndex(e => e.Code).IsUnique();

        modelBuilder.Entity<RecruitmentDropdownOption>().ToTable("recruitment_dropdown_options");
        modelBuilder.Entity<RecruitmentDropdownOption>().HasKey(r => r.Id);
        modelBuilder.Entity<RecruitmentDropdownOption>().HasIndex(r => new { r.Type, r.Code }).IsUnique();

        modelBuilder.Entity<MasterDataOption>().ToTable("master_data_options");
        modelBuilder.Entity<MasterDataOption>().HasKey(m => m.Id);
        modelBuilder.Entity<MasterDataOption>().HasIndex(m => new { m.Category, m.Code }).IsUnique();
        modelBuilder.Entity<MasterDataOption>().HasIndex(m => m.Category);

        modelBuilder.Entity<WorkflowConfig>().ToTable("workflow_configs");
        modelBuilder.Entity<WorkflowConfig>().HasKey(w => w.Id);
        modelBuilder.Entity<WorkflowConfig>().HasIndex(w => new { w.Module, w.Action }).IsUnique();
        modelBuilder.Entity<WorkflowConfig>().HasIndex(w => w.Module);

        modelBuilder.Entity<Lead>(b =>
        {
            b.ToTable("leads");
            b.HasKey(l => l.Id);
            b.Property(l => l.Name).HasMaxLength(200).IsRequired();
            b.Property(l => l.CompanyName).HasMaxLength(200);
            b.Property(l => l.Phone).HasMaxLength(30);
            b.Property(l => l.Email).HasMaxLength(150);
            b.Property(l => l.SourceCode).HasMaxLength(60).IsRequired();
            b.Property(l => l.Status).HasConversion<string>().HasMaxLength(30);
            b.HasIndex(l => l.Status);
            b.HasIndex(l => l.OwnerUserId);
            b.HasIndex(l => l.SourceCode);
            b.HasIndex(l => l.CreatedAt);
            b.HasOne(l => l.Owner)
                .WithMany()
                .HasForeignKey(l => l.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LeadActivity>(b =>
        {
            b.ToTable("lead_activities");
            b.HasKey(a => a.Id);
            b.Property(a => a.Type).HasConversion<string>().HasMaxLength(30);
            b.Property(a => a.Content).HasMaxLength(2000).IsRequired();
            b.HasOne(a => a.Lead)
                .WithMany(l => l.Activities)
                .HasForeignKey(a => a.LeadId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(a => a.CreatedBy)
                .WithMany()
                .HasForeignKey(a => a.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => a.LeadId);
            b.HasIndex(a => a.CreatedAt);
        });

        modelBuilder.Entity<Customer>(b =>
        {
            b.ToTable("customers");
            b.HasKey(c => c.Id);
            b.Property(c => c.Type).HasConversion<string>().HasMaxLength(30);
            b.Property(c => c.Name).HasMaxLength(200).IsRequired();
            b.Property(c => c.TaxId).HasMaxLength(30);
            b.Property(c => c.Address).HasMaxLength(500);
            b.Property(c => c.RepresentativeName).HasMaxLength(200);
            b.Property(c => c.SourceCode).HasMaxLength(60).IsRequired();
            b.Property(c => c.RelationshipStatus).HasConversion<string>().HasMaxLength(30);
            b.HasIndex(c => c.Type);
            b.HasIndex(c => c.RelationshipStatus);
            b.HasIndex(c => c.OwnerUserId);
            b.HasIndex(c => c.SourceCode);
            b.HasIndex(c => c.TaxId); // used for duplicate detection on Company
            b.HasIndex(c => c.CreatedAt);
            b.HasOne(c => c.Owner)
                .WithMany()
                .HasForeignKey(c => c.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CustomerContact>(b =>
        {
            b.ToTable("customer_contacts");
            b.HasKey(c => c.Id);
            b.Property(c => c.FullName).HasMaxLength(200).IsRequired();
            b.Property(c => c.Position).HasMaxLength(150);
            b.Property(c => c.Phone).HasMaxLength(30);
            b.Property(c => c.Email).HasMaxLength(150);
            b.HasOne(c => c.Customer)
                .WithMany(cu => cu.Contacts)
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(c => c.CustomerId);
            b.HasIndex(c => c.Phone); // used for duplicate detection on Individual
        });

        modelBuilder.Entity<CustomerActivity>(b =>
        {
            b.ToTable("customer_activities");
            b.HasKey(a => a.Id);
            b.Property(a => a.Type).HasConversion<string>().HasMaxLength(30);
            b.Property(a => a.Content).HasMaxLength(4000).IsRequired();
            b.HasOne(a => a.Customer)
                .WithMany(c => c.Activities)
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(a => a.CreatedBy)
                .WithMany()
                .HasForeignKey(a => a.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => a.CustomerId);
            b.HasIndex(a => a.OccurredAt);
        });

        modelBuilder.Entity<Opportunity>(b =>
        {
            b.ToTable("opportunities");
            b.HasKey(o => o.Id);
            b.Property(o => o.Name).HasMaxLength(200).IsRequired();
            b.Property(o => o.Stage).HasConversion<string>().HasMaxLength(30);
            b.Property(o => o.LostReasonCode).HasMaxLength(60);
            b.Property(o => o.LostNote).HasMaxLength(2000);
            b.Property(o => o.Note).HasMaxLength(4000);
            b.Property(o => o.EstimatedValue).HasColumnType("decimal(18,2)");
            b.HasOne(o => o.Customer)
                .WithMany()
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(o => o.Owner)
                .WithMany()
                .HasForeignKey(o => o.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(o => o.CustomerId);
            b.HasIndex(o => o.OwnerUserId);
            b.HasIndex(o => o.Stage);
            b.HasIndex(o => o.ExpectedCloseDate);
            b.HasIndex(o => o.CreatedAt);
        });

        modelBuilder.Entity<OpportunityActivity>(b =>
        {
            b.ToTable("opportunity_activities");
            b.HasKey(a => a.Id);
            b.Property(a => a.Type).HasConversion<string>().HasMaxLength(30);
            b.Property(a => a.Content).HasMaxLength(4000).IsRequired();
            b.HasOne(a => a.Opportunity)
                .WithMany(o => o.Activities)
                .HasForeignKey(a => a.OpportunityId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(a => a.CreatedBy)
                .WithMany()
                .HasForeignKey(a => a.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(a => a.OpportunityId);
            b.HasIndex(a => a.OccurredAt);
        });

        modelBuilder.Entity<Quote>(b =>
        {
            b.ToTable("quotes");
            b.HasKey(q => q.Id);
            b.Property(q => q.Code).HasMaxLength(40).IsRequired();
            b.HasIndex(q => q.Code).IsUnique();
            b.Property(q => q.Method).HasConversion<string>().HasMaxLength(20);
            b.Property(q => q.Status).HasConversion<string>().HasMaxLength(30);
            b.Property(q => q.PackageDescription).HasMaxLength(2000);
            b.Property(q => q.Note).HasMaxLength(4000);
            b.Property(q => q.AreaSqm).HasColumnType("decimal(18,2)");
            b.Property(q => q.UnitPricePerSqm).HasColumnType("decimal(18,2)");
            b.Property(q => q.Subtotal).HasColumnType("decimal(18,2)");
            b.Property(q => q.DiscountPercent).HasColumnType("decimal(5,2)");
            b.Property(q => q.VatPercent).HasColumnType("decimal(5,2)");
            b.Property(q => q.GrandTotal).HasColumnType("decimal(18,2)");
            b.HasOne(q => q.Opportunity)
                .WithMany()
                .HasForeignKey(q => q.OpportunityId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(q => q.Owner)
                .WithMany()
                .HasForeignKey(q => q.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(q => q.OpportunityId);
            b.HasIndex(q => q.OwnerUserId);
            b.HasIndex(q => q.Status);
            b.HasIndex(q => q.ValidUntil);
            b.HasIndex(q => q.CreatedAt);
        });

        modelBuilder.Entity<QuoteItem>(b =>
        {
            b.ToTable("quote_items");
            b.HasKey(i => i.Id);
            b.Property(i => i.ItemCode).HasMaxLength(60);
            b.Property(i => i.Name).HasMaxLength(300).IsRequired();
            b.Property(i => i.Unit).HasMaxLength(30).IsRequired();
            b.Property(i => i.Quantity).HasColumnType("decimal(18,4)");
            b.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
            b.Property(i => i.Amount).HasColumnType("decimal(18,2)");
            b.HasOne(i => i.Quote)
                .WithMany(q => q.Items)
                .HasForeignKey(i => i.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(i => i.QuoteId);
        });

        modelBuilder.Entity<QuoteApprovalLog>(b =>
        {
            b.ToTable("quote_approval_logs");
            b.HasKey(l => l.Id);
            b.Property(l => l.Action).HasConversion<string>().HasMaxLength(40);
            b.Property(l => l.FromStatus).HasConversion<string>().HasMaxLength(30);
            b.Property(l => l.ToStatus).HasConversion<string>().HasMaxLength(30);
            b.Property(l => l.Note).HasMaxLength(2000);
            b.HasOne(l => l.Quote)
                .WithMany(q => q.ApprovalLogs)
                .HasForeignKey(l => l.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(l => l.By)
                .WithMany()
                .HasForeignKey(l => l.ByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(l => l.QuoteId);
            b.HasIndex(l => l.CreatedAt);
        });

        modelBuilder.Entity<QuoteVersionSnapshot>(b =>
        {
            b.ToTable("quote_version_snapshots");
            b.HasKey(s => s.Id);
            b.Property(s => s.Method).HasConversion<string>().HasMaxLength(20);
            b.Property(s => s.PackageDescription).HasMaxLength(2000);
            b.Property(s => s.ItemsJson).HasColumnType("nvarchar(max)");
            b.Property(s => s.AreaSqm).HasColumnType("decimal(18,2)");
            b.Property(s => s.UnitPricePerSqm).HasColumnType("decimal(18,2)");
            b.Property(s => s.Subtotal).HasColumnType("decimal(18,2)");
            b.Property(s => s.DiscountPercent).HasColumnType("decimal(5,2)");
            b.Property(s => s.VatPercent).HasColumnType("decimal(5,2)");
            b.Property(s => s.GrandTotal).HasColumnType("decimal(18,2)");
            b.HasOne(s => s.Quote)
                .WithMany(q => q.VersionSnapshots)
                .HasForeignKey(s => s.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(s => s.QuoteId);
            b.HasIndex(s => new { s.QuoteId, s.VersionNumber }).IsUnique();
        });

        modelBuilder.Entity<Contract>(b =>
        {
            b.ToTable("contracts");
            b.HasKey(c => c.Id);
            b.Property(c => c.ContractNumber).HasMaxLength(40).IsRequired();
            b.HasIndex(c => c.ContractNumber).IsUnique();
            b.Property(c => c.Status).HasConversion<string>().HasMaxLength(30);
            b.Property(c => c.Value).HasColumnType("decimal(18,2)");
            b.Property(c => c.ScopeOfWork).HasColumnType("nvarchar(max)");
            b.Property(c => c.Note).HasMaxLength(4000);
            b.HasOne(c => c.Customer)
                .WithMany()
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(c => c.Opportunity)
                .WithMany()
                .HasForeignKey(c => c.OpportunityId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasOne(c => c.Quote)
                .WithMany()
                .HasForeignKey(c => c.QuoteId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasOne(c => c.Owner)
                .WithMany()
                .HasForeignKey(c => c.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(c => c.CustomerId);
            b.HasIndex(c => c.OwnerUserId);
            b.HasIndex(c => c.Status);
            b.HasIndex(c => c.SignedDate);
            b.HasIndex(c => c.EndDate);
        });

        modelBuilder.Entity<ContractPaymentMilestone>(b =>
        {
            b.ToTable("contract_payment_milestones");
            b.HasKey(m => m.Id);
            b.Property(m => m.Name).HasMaxLength(200).IsRequired();
            b.Property(m => m.PercentValue).HasColumnType("decimal(5,2)");
            b.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(m => m.Note).HasMaxLength(500);
            b.HasOne(m => m.Contract)
                .WithMany()
                .HasForeignKey(m => m.ContractId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(m => m.ContractId);
            b.HasIndex(m => new { m.ContractId, m.Order }).IsUnique();
        });

        modelBuilder.Entity<ContractAppendix>(b =>
        {
            b.ToTable("contract_appendices");
            b.HasKey(v => v.Id);
            b.Property(v => v.Title).HasMaxLength(300).IsRequired();
            b.Property(v => v.Reason).HasMaxLength(4000).IsRequired();
            b.Property(v => v.ValueDelta).HasColumnType("decimal(18,2)");
            b.Property(v => v.FilePath).HasMaxLength(500);
            b.Property(v => v.OriginalFileName).HasMaxLength(300);
            b.Property(v => v.ContentType).HasMaxLength(150);
            b.Property(v => v.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(v => v.DecisionNote).HasMaxLength(1000);
            b.HasOne(v => v.Contract)
                .WithMany()
                .HasForeignKey(v => v.ContractId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(v => v.SubmittedBy)
                .WithMany()
                .HasForeignKey(v => v.SubmittedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            b.HasOne(v => v.DecidedBy)
                .WithMany()
                .HasForeignKey(v => v.DecidedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            b.HasIndex(v => v.ContractId);
            b.HasIndex(v => new { v.ContractId, v.VoNumber }).IsUnique();
            b.HasIndex(v => v.Status);
        });

        modelBuilder.Entity<ContractAttachment>(b =>
        {
            b.ToTable("contract_attachments");
            b.HasKey(a => a.Id);
            b.Property(a => a.Kind).HasConversion<string>().HasMaxLength(30);
            b.Property(a => a.FilePath).HasMaxLength(500).IsRequired();
            b.Property(a => a.OriginalFileName).HasMaxLength(300).IsRequired();
            b.Property(a => a.ContentType).HasMaxLength(150).IsRequired();
            b.Property(a => a.Label).HasMaxLength(300);
            b.HasOne(a => a.Contract)
                .WithMany()
                .HasForeignKey(a => a.ContractId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(a => a.UploadedBy)
                .WithMany()
                .HasForeignKey(a => a.UploadedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            b.HasIndex(a => a.ContractId);
            b.HasIndex(a => a.Kind);
        });

        modelBuilder.Entity<CapabilityDocument>(b =>
        {
            b.ToTable("capability_documents");
            b.HasKey(d => d.Id);
            b.Property(d => d.Name).HasMaxLength(300).IsRequired();
            b.Property(d => d.TagCode).HasMaxLength(80).IsRequired();
            b.Property(d => d.Description).HasMaxLength(2000);
            b.Property(d => d.FilePath).HasMaxLength(500).IsRequired();
            b.Property(d => d.OriginalFileName).HasMaxLength(300).IsRequired();
            b.Property(d => d.ContentType).HasMaxLength(150).IsRequired();
            b.HasOne(d => d.UploadedBy)
                .WithMany()
                .HasForeignKey(d => d.UploadedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(d => d.TagCode);
            b.HasIndex(d => d.ExpiryDate);
            b.HasIndex(d => d.CreatedAt);
        });

        modelBuilder.Entity<CapabilityDocumentVersion>(b =>
        {
            b.ToTable("capability_document_versions");
            b.HasKey(v => v.Id);
            b.Property(v => v.FilePath).HasMaxLength(500).IsRequired();
            b.Property(v => v.OriginalFileName).HasMaxLength(300).IsRequired();
            b.Property(v => v.ContentType).HasMaxLength(150).IsRequired();
            b.HasOne(v => v.CapabilityDocument)
                .WithMany(d => d.Versions)
                .HasForeignKey(v => v.CapabilityDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(v => v.UploadedBy)
                .WithMany()
                .HasForeignKey(v => v.UploadedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(v => v.CapabilityDocumentId);
            b.HasIndex(v => new { v.CapabilityDocumentId, v.VersionNumber }).IsUnique();
        });

        modelBuilder.Entity<Tender>(b =>
        {
            b.ToTable("tenders");
            b.HasKey(t => t.Id);
            b.Property(t => t.Code).HasMaxLength(40).IsRequired();
            b.HasIndex(t => t.Code).IsUnique();
            b.Property(t => t.Name).HasMaxLength(300).IsRequired();
            b.Property(t => t.InfoSource).HasMaxLength(200);
            b.Property(t => t.Note).HasMaxLength(4000);
            b.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(t => t.LostReasonCode).HasMaxLength(80);
            b.Property(t => t.LostNote).HasMaxLength(2000);
            b.HasOne(t => t.Customer)
                .WithMany()
                .HasForeignKey(t => t.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(t => t.Preparer)
                .WithMany()
                .HasForeignKey(t => t.PreparerUserId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(t => t.CustomerId);
            b.HasIndex(t => t.PreparerUserId);
            b.HasIndex(t => t.Status);
            b.HasIndex(t => t.SubmissionDeadline);
            b.HasIndex(t => t.CreatedAt);
        });

        modelBuilder.Entity<TenderChecklistItem>(b =>
        {
            b.ToTable("tender_checklist_items");
            b.HasKey(i => i.Id);
            b.Property(i => i.TemplateCode).HasMaxLength(80);
            b.Property(i => i.Title).HasMaxLength(300).IsRequired();
            b.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(i => i.FilePath).HasMaxLength(500);
            b.Property(i => i.OriginalFileName).HasMaxLength(300);
            b.HasOne(i => i.Tender)
                .WithMany(t => t.ChecklistItems)
                .HasForeignKey(i => i.TenderId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(i => i.Owner)
                .WithMany()
                .HasForeignKey(i => i.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(i => i.TenderId);
            b.HasIndex(i => new { i.TenderId, i.SortOrder });
        });

        modelBuilder.Entity<Survey>(b =>
        {
            b.ToTable("surveys");
            b.HasKey(s => s.Id);
            b.Property(s => s.Code).HasMaxLength(40).IsRequired();
            b.HasIndex(s => s.Code).IsUnique();
            b.Property(s => s.Location).HasMaxLength(300).IsRequired();
            b.Property(s => s.ConstructionTypeCode).HasMaxLength(80);
            b.Property(s => s.Note).HasMaxLength(4000);
            b.Property(s => s.DriveSyncStatus).HasConversion<string>().HasMaxLength(20);
            b.Property(s => s.DriveSyncError).HasMaxLength(1000);
            b.HasOne(s => s.Surveyor)
                .WithMany()
                .HasForeignKey(s => s.SurveyorUserId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasOne(s => s.LinkedProject)
                .WithMany()
                .HasForeignKey(s => s.LinkedProjectId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasOne(s => s.LinkedOpportunity)
                .WithMany()
                .HasForeignKey(s => s.LinkedOpportunityId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(s => s.SurveyorUserId);
            b.HasIndex(s => s.LinkedProjectId);
            b.HasIndex(s => s.LinkedOpportunityId);
            b.HasIndex(s => s.SurveyDate);
            b.HasIndex(s => s.DriveSyncStatus);
        });

        modelBuilder.Entity<DesignProject>(b =>
        {
            b.ToTable("design_projects");
            b.HasKey(dp => dp.Id);
            b.Property(dp => dp.ProjectCode).HasMaxLength(40).IsRequired();
            b.HasIndex(dp => dp.ProjectCode).IsUnique();
            b.Property(dp => dp.Name).HasMaxLength(300).IsRequired();
            b.Property(dp => dp.Note).HasMaxLength(4000);
            b.Property(dp => dp.CurrentStage).HasConversion<string>().HasMaxLength(20);
            b.Property(dp => dp.Status).HasConversion<string>().HasMaxLength(20);

            b.HasOne(dp => dp.Customer)
                .WithMany()
                .HasForeignKey(dp => dp.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(dp => dp.Contract)
                .WithMany()
                .HasForeignKey(dp => dp.ContractId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasOne(dp => dp.ProjectManager)
                .WithMany()
                .HasForeignKey(dp => dp.ProjectManagerUserId)
                .OnDelete(DeleteBehavior.NoAction);
            b.HasOne(dp => dp.DesignLead)
                .WithMany()
                .HasForeignKey(dp => dp.DesignLeadUserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(dp => dp.CustomerId);
            b.HasIndex(dp => dp.ContractId).IsUnique().HasFilter("[ContractId] IS NOT NULL");
            b.HasIndex(dp => dp.ProjectManagerUserId);
            b.HasIndex(dp => dp.DesignLeadUserId);
            b.HasIndex(dp => dp.Status);
            b.HasIndex(dp => dp.CurrentStage);
        });

        modelBuilder.Entity<PermitChecklistItem>(b =>
        {
            b.ToTable("permit_checklist_items");
            b.HasKey(p => p.Id);
            b.Property(p => p.PermitTypeCode).HasMaxLength(60).IsRequired();
            b.Property(p => p.IssuingAgency).HasMaxLength(200);
            b.Property(p => p.SubmittedFilePath).HasMaxLength(500);
            b.Property(p => p.IssuedFilePath).HasMaxLength(500);
            b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(p => p.Note).HasMaxLength(4000);

            b.HasOne(p => p.DesignProject)
                .WithMany()
                .HasForeignKey(p => p.DesignProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(p => p.Owner)
                .WithMany()
                .HasForeignKey(p => p.OwnerUserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(p => p.DesignProjectId);
            b.HasIndex(p => new { p.DesignProjectId, p.PermitTypeCode }).IsUnique();
            b.HasIndex(p => p.Status);
            b.HasIndex(p => p.TargetDeadline);
            b.HasIndex(p => p.ExpiresAt);
        });

        modelBuilder.Entity<ConceptOption>(b =>
        {
            b.ToTable("concept_options");
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).HasMaxLength(200).IsRequired();
            b.Property(c => c.Description).HasMaxLength(4000);
            b.Property(c => c.InternalNote).HasMaxLength(4000);
            b.Property(c => c.Status).HasConversion<string>().HasMaxLength(30);

            b.HasOne(c => c.DesignProject)
                .WithMany()
                .HasForeignKey(c => c.DesignProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(c => c.Owner)
                .WithMany()
                .HasForeignKey(c => c.OwnerUserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(c => c.DesignProjectId);
            b.HasIndex(c => c.Status);
        });

        modelBuilder.Entity<BasicDesignDoc>(b =>
        {
            b.ToTable("basic_design_docs");
            b.HasKey(d => d.Id);
            b.Property(d => d.DisciplineCode).HasMaxLength(60).IsRequired();
            b.Property(d => d.DocumentCode).HasMaxLength(60).IsRequired();
            b.Property(d => d.Title).HasMaxLength(300).IsRequired();
            b.Property(d => d.Description).HasMaxLength(4000);
            b.Property(d => d.Note).HasMaxLength(4000);
            b.Property(d => d.Status).HasConversion<string>().HasMaxLength(30);

            b.HasOne(d => d.DesignProject)
                .WithMany()
                .HasForeignKey(d => d.DesignProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(d => d.Owner)
                .WithMany()
                .HasForeignKey(d => d.OwnerUserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(d => d.DesignProjectId);
            b.HasIndex(d => new { d.DesignProjectId, d.DocumentCode }).IsUnique();
            b.HasIndex(d => d.DisciplineCode);
            b.HasIndex(d => d.Status);
        });

        modelBuilder.Entity<ShopDrawing>(b =>
        {
            b.ToTable("shop_drawings");
            b.HasKey(d => d.Id);
            b.Property(d => d.DisciplineCode).HasMaxLength(60).IsRequired();
            b.Property(d => d.ConstructionItem).HasMaxLength(200).IsRequired();
            b.Property(d => d.DrawingCode).HasMaxLength(60).IsRequired();
            b.Property(d => d.Title).HasMaxLength(300).IsRequired();
            b.Property(d => d.Description).HasMaxLength(4000);
            b.Property(d => d.Note).HasMaxLength(4000);
            b.Property(d => d.Status).HasConversion<string>().HasMaxLength(30);

            b.HasOne(d => d.DesignProject)
                .WithMany()
                .HasForeignKey(d => d.DesignProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(d => d.Owner)
                .WithMany()
                .HasForeignKey(d => d.OwnerUserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(d => d.DesignProjectId);
            b.HasIndex(d => new { d.DesignProjectId, d.DrawingCode }).IsUnique();
            b.HasIndex(d => d.DisciplineCode);
            b.HasIndex(d => d.Status);
        });

        modelBuilder.Entity<DrawingRevision>(b =>
        {
            b.ToTable("drawing_revisions");
            b.HasKey(d => d.Id);
            b.Property(d => d.TargetType).HasConversion<string>().HasMaxLength(30);
            b.Property(d => d.ReasonCode).HasMaxLength(60).IsRequired();
            b.Property(d => d.Note).HasMaxLength(4000).IsRequired();

            b.HasOne(d => d.CreatedBy)
                .WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(d => new { d.TargetType, d.TargetId });
            b.HasIndex(d => new { d.TargetType, d.TargetId, d.RevisionNumber }).IsUnique();
        });

        modelBuilder.Entity<IfcRelease>(b =>
        {
            b.ToTable("ifc_releases");
            b.HasKey(r => r.Id);
            b.Property(r => r.ReleaseNumber).HasMaxLength(60).IsRequired();
            b.Property(r => r.Title).HasMaxLength(300).IsRequired();
            b.Property(r => r.Note).HasMaxLength(4000);
            b.Property(r => r.Status).HasConversion<string>().HasMaxLength(30);

            b.HasOne(r => r.DesignProject)
                .WithMany()
                .HasForeignKey(r => r.DesignProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(r => r.IssuedBy)
                .WithMany()
                .HasForeignKey(r => r.IssuedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(r => r.DesignProjectId);
            b.HasIndex(r => new { r.DesignProjectId, r.ReleaseNumber }).IsUnique();
            b.HasIndex(r => r.Status);
        });

        modelBuilder.Entity<IfcReleaseItem>(b =>
        {
            b.ToTable("ifc_release_items");
            b.HasKey(i => i.Id);

            b.HasOne(i => i.IfcRelease)
                .WithMany(r => r.Items)
                .HasForeignKey(i => i.IfcReleaseId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(i => i.ShopDrawing)
                .WithMany()
                .HasForeignKey(i => i.ShopDrawingId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(i => new { i.IfcReleaseId, i.ShopDrawingId }).IsUnique();
        });

        modelBuilder.Entity<IfcReleaseRecipient>(b =>
        {
            b.ToTable("ifc_release_recipients");
            b.HasKey(x => x.Id);
            b.Property(x => x.Name).HasMaxLength(300).IsRequired();
            b.Property(x => x.RecipientTypeCode).HasMaxLength(60).IsRequired();
            b.Property(x => x.AcknowledgementNote).HasMaxLength(1000);

            b.HasOne(x => x.IfcRelease)
                .WithMany(r => r.Recipients)
                .HasForeignKey(x => x.IfcReleaseId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(x => x.IfcReleaseId);
        });

        modelBuilder.Entity<ConstructionTask>(b =>
        {
            b.ToTable("construction_tasks");
            b.HasKey(t => t.Id);
            b.Property(t => t.TaskCode).HasMaxLength(60).IsRequired();
            b.Property(t => t.Wbs).HasMaxLength(60);
            b.Property(t => t.Name).HasMaxLength(300).IsRequired();
            b.Property(t => t.Description).HasMaxLength(4000);
            b.Property(t => t.Status).HasConversion<string>().HasMaxLength(30);

            b.HasOne(t => t.DesignProject)
                .WithMany()
                .HasForeignKey(t => t.DesignProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(t => t.Owner)
                .WithMany()
                .HasForeignKey(t => t.OwnerUserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(t => t.DesignProjectId);
            b.HasIndex(t => new { t.DesignProjectId, t.TaskCode }).IsUnique();
            b.HasIndex(t => t.Status);
        });

        modelBuilder.Entity<ConstructionTaskDependency>(b =>
        {
            b.ToTable("construction_task_dependencies");
            b.HasKey(x => x.Id);

            b.HasOne(x => x.Task)
                .WithMany(t => t.Predecessors)
                .HasForeignKey(x => x.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
            // Predecessor side: keep the edge unless the predecessor is
            // explicitly cleared — no cascade, which would otherwise create
            // multiple-cascade-path errors on SQL Server.
            b.HasOne(x => x.PredecessorTask)
                .WithMany()
                .HasForeignKey(x => x.PredecessorTaskId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new { x.TaskId, x.PredecessorTaskId }).IsUnique();
            b.HasIndex(x => x.PredecessorTaskId);
        });

        modelBuilder.Entity<SiteDiary>(b =>
        {
            b.ToTable("site_diaries");
            b.HasKey(d => d.Id);
            b.Property(d => d.WeatherCode).HasMaxLength(60).IsRequired();
            b.Property(d => d.WeatherNote).HasMaxLength(300);
            b.Property(d => d.MachinesSummary).HasMaxLength(2000);
            b.Property(d => d.MaterialsReceived).HasMaxLength(2000);
            b.Property(d => d.WorkPerformed).HasMaxLength(4000).IsRequired();
            b.Property(d => d.Incidents).HasMaxLength(2000);
            b.Property(d => d.Note).HasMaxLength(2000);
            b.Property(d => d.Status).HasConversion<string>().HasMaxLength(30);

            b.HasOne(d => d.DesignProject)
                .WithMany()
                .HasForeignKey(d => d.DesignProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(d => d.SubmittedBy)
                .WithMany()
                .HasForeignKey(d => d.SubmittedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            b.HasOne(d => d.ConfirmedBy)
                .WithMany()
                .HasForeignKey(d => d.ConfirmedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(d => d.DesignProjectId);
            b.HasIndex(d => new { d.DesignProjectId, d.DiaryDate }).IsUnique();
            b.HasIndex(d => d.Status);
        });

        modelBuilder.Entity<PunchItem>(b =>
        {
            b.ToTable("punch_items");
            b.HasKey(p => p.Id);
            b.Property(p => p.PunchCode).HasMaxLength(60).IsRequired();
            b.Property(p => p.Title).HasMaxLength(300).IsRequired();
            b.Property(p => p.Description).HasMaxLength(4000);
            b.Property(p => p.Location).HasMaxLength(300);
            b.Property(p => p.ResolutionNote).HasMaxLength(2000);
            b.Property(p => p.Note).HasMaxLength(2000);
            b.Property(p => p.Severity).HasConversion<string>().HasMaxLength(30);
            b.Property(p => p.Status).HasConversion<string>().HasMaxLength(30);

            b.HasOne(p => p.DesignProject)
                .WithMany()
                .HasForeignKey(p => p.DesignProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(p => p.Assignee)
                .WithMany()
                .HasForeignKey(p => p.AssigneeUserId)
                .OnDelete(DeleteBehavior.NoAction);
            b.HasOne(p => p.VerifiedBy)
                .WithMany()
                .HasForeignKey(p => p.VerifiedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(p => p.DesignProjectId);
            b.HasIndex(p => new { p.DesignProjectId, p.PunchCode }).IsUnique();
            b.HasIndex(p => p.Status);
            b.HasIndex(p => p.Severity);
        });

        modelBuilder.Entity<AcceptanceRecord>(b =>
        {
            b.ToTable("acceptance_records");
            b.HasKey(a => a.Id);
            b.Property(a => a.AcceptanceCode).HasMaxLength(60).IsRequired();
            b.Property(a => a.Title).HasMaxLength(300).IsRequired();
            b.Property(a => a.Description).HasMaxLength(4000);
            b.Property(a => a.Location).HasMaxLength(200);
            b.Property(a => a.Participants).HasMaxLength(1000);
            b.Property(a => a.Findings).HasMaxLength(4000);
            b.Property(a => a.ResolutionNote).HasMaxLength(2000);
            b.Property(a => a.Documents).HasMaxLength(4000);
            b.Property(a => a.Status).HasConversion<string>().HasMaxLength(30);

            b.HasOne(a => a.DesignProject)
                .WithMany()
                .HasForeignKey(a => a.DesignProjectId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(a => a.ConstructionTask)
                .WithMany()
                .HasForeignKey(a => a.ConstructionTaskId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasOne(a => a.SubmittedBy)
                .WithMany()
                .HasForeignKey(a => a.SubmittedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            b.HasOne(a => a.ApprovedBy)
                .WithMany()
                .HasForeignKey(a => a.ApprovedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            b.HasOne(a => a.RejectedBy)
                .WithMany()
                .HasForeignKey(a => a.RejectedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(a => a.DesignProjectId);
            b.HasIndex(a => new { a.DesignProjectId, a.AcceptanceCode }).IsUnique();
            b.HasIndex(a => a.Status);
        });

        modelBuilder.Entity<ContactMessage>().ToTable("contact_messages");
        modelBuilder.Entity<ContactMessage>().HasKey(c => c.Id);

        // i18n tables
        modelBuilder.Entity<Translation>().ToTable("translations");
        modelBuilder.Entity<Translation>().HasKey(t => t.TranslationId);

        modelBuilder.Entity<EntityTranslation>().ToTable("entity_translations");
        modelBuilder.Entity<EntityTranslation>().HasKey(t => t.Id);
    }
}
