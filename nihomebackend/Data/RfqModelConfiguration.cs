using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.Data;

internal static class RfqModelConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<Rfq>(b =>
        {
            b.ToTable("rfqs", table => table.HasCheckConstraint("CK_rfqs_scoring_weights",
                "[PriceWeight] + [LeadTimeWeight] + [VendorRatingWeight] + [CommercialWeight] = 100"));
            b.Property(x => x.Code).HasMaxLength(60);
            b.Property(x => x.Title).HasMaxLength(200);
            b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.Note).HasMaxLength(2000);
            b.Property(x => x.AwardReason).HasMaxLength(2000);
            b.Property(x => x.PriceWeight).HasPrecision(5, 2);
            b.Property(x => x.LeadTimeWeight).HasPrecision(5, 2);
            b.Property(x => x.VendorRatingWeight).HasPrecision(5, 2);
            b.Property(x => x.CommercialWeight).HasPrecision(5, 2);
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
            b.Property(x => x.PortalTokenHash).HasMaxLength(64);
            b.Property(x => x.InvitationDeliveryError).HasMaxLength(1000);
            b.HasOne(x => x.Rfq).WithMany(x => x.Invitations).HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.RfqId, x.VendorId }).IsUnique();
            b.HasIndex(x => x.PortalTokenHash).IsUnique().HasFilter("[PortalTokenHash] IS NOT NULL");
        });
        model.Entity<RfqBid>(b =>
        {
            b.ToTable("rfq_bids", table =>
            {
                table.HasCheckConstraint("CK_rfq_bids_exchange_rate", "[ExchangeRateToVnd] > 0");
                table.HasCheckConstraint("CK_rfq_bids_commercial_values",
                    "[FreightAmount] >= 0 AND [DiscountPercent] >= 0 AND [DiscountPercent] <= 100 AND [DiscountAmount] >= 0 AND [VatPercent] >= 0 AND [VatPercent] <= 100");
            });
            b.Property(x => x.PaymentTerms).HasMaxLength(1000);
            b.Property(x => x.Note).HasMaxLength(2000);
            b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.ExchangeRateToVnd).HasPrecision(20, 8);
            b.Property(x => x.Subtotal).HasPrecision(18, 4);
            b.Property(x => x.FreightAmount).HasPrecision(18, 4);
            b.Property(x => x.DiscountPercent).HasPrecision(5, 2);
            b.Property(x => x.DiscountAmount).HasPrecision(18, 4);
            b.Property(x => x.VatPercent).HasPrecision(5, 2);
            b.Property(x => x.TotalOriginal).HasPrecision(18, 4);
            b.Property(x => x.CommercialScore).HasPrecision(5, 2);
            b.Property(x => x.EvaluationNote).HasMaxLength(2000);
            b.Property(x => x.Total).HasPrecision(18, 4);
            b.HasOne(x => x.Rfq).WithMany(x => x.Bids).HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.SubmittedBy).WithMany().HasForeignKey(x => x.SubmittedByUserId).OnDelete(DeleteBehavior.NoAction);
            b.HasOne(x => x.EvaluatedBy).WithMany().HasForeignKey(x => x.EvaluatedByUserId).OnDelete(DeleteBehavior.NoAction);
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
            b.Property(x => x.ActorLabel).HasMaxLength(300);
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
        model.Entity<RfqAward>(b =>
        {
            b.ToTable("rfq_awards");
            b.Property(x => x.Currency).HasMaxLength(3);
            b.Property(x => x.ExchangeRateToVnd).HasPrecision(20, 8);
            b.Property(x => x.OriginalValue).HasPrecision(18, 4);
            b.Property(x => x.ValueVnd).HasPrecision(18, 4);
            b.HasOne(x => x.Rfq).WithMany(x => x.Awards).HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.RfqBid).WithMany().HasForeignKey(x => x.RfqBidId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.AwardedBy).WithMany().HasForeignKey(x => x.AwardedByUserId).OnDelete(DeleteBehavior.NoAction);
            b.HasIndex(x => new { x.RfqId, x.VendorId }).IsUnique();
            b.HasIndex(x => x.ContractId).IsUnique();
        });
        model.Entity<RfqAwardLine>(b =>
        {
            b.ToTable("rfq_award_lines");
            b.Property(x => x.Quantity).HasPrecision(18, 6);
            b.Property(x => x.UnitPrice).HasPrecision(18, 4);
            b.Property(x => x.AmountOriginal).HasPrecision(18, 4);
            b.Property(x => x.AmountVnd).HasPrecision(18, 4);
            b.HasOne(x => x.RfqAward).WithMany(x => x.Lines).HasForeignKey(x => x.RfqAwardId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RfqLine).WithMany().HasForeignKey(x => x.RfqLineId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.RfqBidLine).WithMany().HasForeignKey(x => x.RfqBidLineId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.RfqAwardId, x.RfqLineId });
        });
        model.Entity<RfqAwardMaterialRequestAllocation>(b =>
        {
            b.ToTable("rfq_award_material_request_allocations");
            b.Property(x => x.Quantity).HasPrecision(18, 6);
            b.HasOne(x => x.RfqAwardLine).WithMany(x => x.MaterialRequestAllocations).HasForeignKey(x => x.RfqAwardLineId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.MaterialRequestLine).WithMany().HasForeignKey(x => x.MaterialRequestLineId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.RfqAwardLineId, x.MaterialRequestLineId }).IsUnique();
        });
    }
}
