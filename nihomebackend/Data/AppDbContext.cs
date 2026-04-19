using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<RegistrationOtp> RegistrationOtps => Set<RegistrationOtp>();

    public DbSet<SiteSettings> SiteSettings => Set<SiteSettings>();

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
    }
}
