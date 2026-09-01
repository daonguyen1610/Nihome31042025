using System.Globalization;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IQuotePdfService
{
    Task<byte[]> CreateAsync(QuoteResponse quote, string languageCode, CancellationToken ct = default);
}

public sealed class QuotePdfService(TranslationService translations) : IQuotePdfService
{
    private static readonly HashSet<string> SupportedLanguages = ["vi", "en", "zh", "ja"];

    public async Task<byte[]> CreateAsync(QuoteResponse quote, string languageCode, CancellationToken ct = default)
    {
        _ = ct;
        var language = languageCode.Trim().ToLowerInvariant();
        if (!SupportedLanguages.Contains(language))
            throw new QuoteOperationException("Ngôn ngữ xuất PDF không hợp lệ. Chỉ chấp nhận vi, en, zh hoặc ja.");

        var text = await translations.GetTranslationMapAsync(language);
        string T(string key, string fallback) => text.GetValueOrDefault(key, fallback);
        string Money(decimal? value) => (value ?? 0).ToString("N0", CultureInfo.InvariantCulture) + " VND";
        string Date(DateOnly? value) => value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "-";
        var preliminary = quote.Status is "Draft" or "PendingApproval";
        var lines = new List<string>();
        var preliminaryWatermark = T("quotes.pdf.preliminaryWatermark", "SƠ BỘ");
        if (preliminary) lines.Add($"***** {preliminaryWatermark} *****");
        lines.Add(T("quotes.pdf.title", "BÁO GIÁ SƠ BỘ"));
        lines.Add($"{T("quotes.pdf.code", "Mã báo giá")}: {quote.Code}   |   {T("quotes.pdf.version", "Phiên bản")}: V{quote.Version}");
        lines.Add($"{T("quotes.pdf.validUntil", "Hiệu lực đến")}: {quote.ValidUntil:dd/MM/yyyy}");
        lines.Add("");
        lines.Add(T("quotes.pdf.customerOpportunity", "KHÁCH HÀNG / CƠ HỘI"));
        lines.Add($"{T("quotes.pdf.customer", "Khách hàng")}: {quote.CustomerName ?? "-"}");
        lines.Add($"{T("quotes.pdf.opportunity", "Cơ hội")}: {quote.OpportunityName ?? "-"}");
        lines.Add("");
        lines.Add(T("quotes.pdf.pricing", "CƠ SỞ ĐƠN GIÁ"));
        lines.Add($"{T("quotes.pdf.area", "Diện tích")}: {quote.AreaSqm?.ToString("N2", CultureInfo.InvariantCulture) ?? "-"} m²");
        lines.Add($"{T("quotes.pdf.catalog", "Danh mục")}: {quote.MaterialRateCatalogCode ?? "-"} - {quote.MaterialRateCatalogName ?? "-"}");
        lines.Add($"{T("quotes.pdf.revision", "Phiên bản đơn giá")}: {quote.MaterialRateRevisionVersion?.ToString() ?? "-"}");
        lines.Add($"{T("quotes.pdf.effectiveDate", "Ngày áp dụng")}: {Date(quote.PricingEffectiveDate)}");
        lines.Add($"{T("quotes.pdf.catalogRate", "Đơn giá danh mục/m²")}: {Money(quote.CatalogUnitPricePerSqm)}");
        lines.Add($"{T("quotes.pdf.appliedRate", "Đơn giá áp dụng/m²")}: {Money(quote.UnitPricePerSqm)}");
        lines.Add($"{T("quotes.pdf.rateSource", "Nguồn đơn giá")}: {T($"quotes.rateSource.{quote.RateSource}", quote.RateSource)}");
        if (!string.IsNullOrWhiteSpace(quote.RateOverrideReason))
            lines.Add($"{T("quotes.pdf.overrideReason", "Lý do điều chỉnh")}: {quote.RateOverrideReason}");
        lines.Add("");
        lines.Add(T("quotes.pdf.totals", "TỔNG HỢP GIÁ TRỊ"));
        lines.Add($"{T("quotes.pdf.subtotal", "Tạm tính")}: {Money(quote.Subtotal)}");
        lines.Add($"{T("quotes.pdf.discount", "Chiết khấu")}: {quote.DiscountPercent:N2}%");
        lines.Add($"{T("quotes.pdf.vat", "VAT")}: {quote.VatPercent:N2}%");
        lines.Add($"{T("quotes.pdf.grandTotal", "TỔNG CỘNG")}: {Money(quote.GrandTotal)}");
        if (preliminary) lines.Add($"***** {preliminaryWatermark} *****");
        return SimplePdfWriter.Create(lines, language);
    }
}