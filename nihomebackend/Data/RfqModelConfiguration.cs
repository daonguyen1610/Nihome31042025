using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.Data;

internal static class RfqModelConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<Rfq>(b =>
        {
            b.ToTable("rfqs");
            b.Property(x => x.Code).HasMaxLength(60);
            b.Property(x => x.Title).HasMaxLength(200);
            b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.Note).HasMaxLength(2000);
            b.Property(x => x.AwardReason).HasMaxLength(2000);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => new { x.OperationalProjectId, x.Status, x.DueAt });
            b.HasOne(x => x.OperationalProject).WithMany().HasForeignKey(x => x.OperationalProjectId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.SourceBoqRevision).WithMany().HasForeignKey(x => x.SourceBoqRevisionId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne(x => x.AwardedBy).WithMany().HasForeignKey(x => x.AwardedByUserId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne(x => x.SelectedBid).WithMany().HasForeignKey(x => x.SelectedBidId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<RfqLine>(b =>
        {
            b.ToTable("rfq_lines");
            b.Property(x => x.ItemCode).HasMaxLength(80);
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.Unit).HasMaxLength(50);
            b.Property(x => x.Quantity).HasPrecision(18, 6);
            b.Property(x => x.BudgetUnitPrice).HasPrecision(18, 4);
            b.HasOne(x => x.Rfq).WithMany(x => x.Lines).HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.ProjectBoqLine).WithMany().HasForeignKey(x => x.ProjectBoqLineId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.RfqId, x.ProjectBoqLineId }).IsUnique();
        });
        model.Entity<RfqInvitation>(b =>
        {
            b.ToTable("rfq_invitations");
            b.Property(x => x.VendorName).HasMaxLength(300);
            b.HasOne(x => x.Rfq).WithMany(x => x.Invitations).HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.RfqId, x.VendorId }).IsUnique();
        });
        model.Entity<RfqBid>(b =>
        {
            b.ToTable("rfq_bids");
            b.Property(x => x.PaymentTerms).HasMaxLength(1000);
            b.Property(x => x.Note).HasMaxLength(2000);
            b.Property(x => x.Total).HasPrecision(18, 4);
            b.HasOne(x => x.Rfq).WithMany(x => x.Bids).HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.SubmittedBy).WithMany().HasForeignKey(x => x.SubmittedByUserId).OnDelete(DeleteBehavior.NoAction);
            b.HasIndex(x => new { x.RfqId, x.VendorId, x.Revision }).IsUnique();
        });
        model.Entity<RfqBidLine>(b =>
        {
            b.ToTable("rfq_bid_lines");
            b.Property(x => x.UnitPrice).HasPrecision(18, 4);
            b.Property(x => x.Amount).HasPrecision(18, 4);
            b.HasOne(x => x.RfqBid).WithMany(x => x.Lines).HasForeignKey(x => x.RfqBidId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RfqLine).WithMany().HasForeignKey(x => x.RfqLineId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.RfqBidId, x.RfqLineId }).IsUnique();
        });
        model.Entity<RfqEvent>(b =>
        {
            b.ToTable("rfq_events");
            b.Property(x => x.Action).HasMaxLength(40);
            b.Property(x => x.Reason).HasMaxLength(2000);
            b.HasOne(x => x.Rfq).WithMany(x => x.Events).HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<RfqDocument>(b =>
        {
            b.ToTable("rfq_documents");
            b.HasOne(x => x.Rfq).WithMany(x => x.Documents).HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.RfqBid).WithMany().HasForeignKey(x => x.RfqBidId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ProjectDocument).WithMany().HasForeignKey(x => x.ProjectDocumentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.ProjectDocumentId).IsUnique();
        });
    }
}
