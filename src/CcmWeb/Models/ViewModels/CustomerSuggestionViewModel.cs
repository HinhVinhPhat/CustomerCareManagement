namespace CcmWeb.Models.ViewModels;

/// <summary>
/// Một gợi ý của ô tìm nhanh (design.md 3.2). Chỉ các trường này rời khỏi máy chủ: không điện thoại,
/// không email, không địa chỉ. Đường dẫn tới khách hàng do trình duyệt ghép từ mã số.
///
/// <para>
/// <c>CustomerTypeLabel</c> là nhãn đã dịch ("Cá nhân" / "Doanh nghiệp") của mã <c>CustomerType</c>:
/// mã hóa chỉ nằm trong cơ sở dữ liệu, còn chữ hiện ra phải qua resource key (ccm-language mục 4).
/// </para>
/// </summary>
public sealed record CustomerSuggestion(
    int CustomerId,
    string CustomerName,
    string CustomerType,
    string CustomerTypeLabel);
