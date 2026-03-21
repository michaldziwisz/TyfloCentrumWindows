using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;

namespace TyfloCentrum.Windows.App.Services;

internal static class AutomationAnnouncementHelper
{
    public static void Announce(
        FrameworkElement element,
        string? message,
        bool important = false
    )
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var peer =
            FrameworkElementAutomationPeer.FromElement(element)
            ?? FrameworkElementAutomationPeer.CreatePeerForElement(element);

        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.Other,
            important
                ? AutomationNotificationProcessing.ImportantMostRecent
                : AutomationNotificationProcessing.MostRecent,
            message,
            "StatusAnnouncement"
        );
    }
}
