using CcmWeb.Helpers;
using CcmWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace CcmWeb.ViewComponents;

/// <summary>Chuông trên thanh trên cùng; số đếm và danh sách luôn đọc từ cơ sở dữ liệu.</summary>
public sealed class NotificationBellViewComponent(INotificationService notifications) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync() =>
        View(await notifications.GetSummaryAsync(
            ViewContext.HttpContext.User.GetRole()));
}
