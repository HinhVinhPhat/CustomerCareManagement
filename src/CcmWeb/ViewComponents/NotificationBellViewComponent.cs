using CcmWeb.Services;
using Microsoft.AspNetCore.Mvc;

namespace CcmWeb.ViewComponents;

/// <summary>Notification bell in the topbar; the counts always come from the database.</summary>
public sealed class NotificationBellViewComponent(INotificationService notifications) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync() =>
        View(await notifications.GetSummaryAsync());
}
