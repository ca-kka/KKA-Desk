using System.Windows;
using System.Windows.Controls;
using Kka.Desk.Services;

namespace Kka.Desk;

public partial class MainWindow : Window
{
    private readonly PersonalWorkQueueStore _workQueueStore = new();
    private readonly Button[] _navigationButtons;

    public MainWindow()
    {
        InitializeComponent();
        WorkQueuePage.Store = _workQueueStore;
        WorkQueuePage.QueueChanged += UpdateOverview;
        WorkQueuePage.QueueLoadFailed += ShowQueueUnavailable;
        _navigationButtons =
        [
            OverviewNavButton, QueueNavButton, DirectoryNavButton,
            DocumentsNavButton, QueriesNavButton, AdminNavButton
        ];
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e) =>
        await WorkQueuePage.RefreshAsync();

    private void Navigate(
        Grid visiblePage,
        Button activeNavigation,
        string title,
        string subtitle)
    {
        OverviewContent.Visibility = Visibility.Collapsed;
        WorkQueueContent.Visibility = Visibility.Collapsed;
        DirectoryContent.Visibility = Visibility.Collapsed;
        PlaceholderContent.Visibility = Visibility.Collapsed;
        visiblePage.Visibility = Visibility.Visible;

        foreach (var button in _navigationButtons)
            button.Tag = null;
        activeNavigation.Tag = "Active";

        PageTitle.Text = title;
        PageSubtitle.Text = subtitle;
    }

    private void Overview_Click(object sender, RoutedEventArgs e) => Navigate(
        OverviewContent, OverviewNavButton, "Good day.",
        "Your firm's work, organised in one place.");

    private void Queue_Click(object sender, RoutedEventArgs e) => Navigate(
        WorkQueueContent, QueueNavButton, "Work queue",
        "Personal reminders saved on this Windows account.");

    private void OpenQueue_Click(object sender, RoutedEventArgs e) => Queue_Click(sender, e);

    private void Directory_Click(object sender, RoutedEventArgs e) => Navigate(
        DirectoryContent, DirectoryNavButton, "Client directory",
        "Existing administrator profile sync · read-only.");

    private void Documents_Click(object sender, RoutedEventArgs e)
    {
        Navigate(PlaceholderContent, DocumentsNavButton, "Documents", "Secure file access is not connected.");
        PlaceholderEyebrow.Text = "CLIENT FILES · DISABLED";
        PlaceholderTitle.Text = "Documents";
        PlaceholderDescription.Text = "Document listing, secure download, upload, version history, and PDF viewing will follow review of the approved storage endpoints and server-side ownership checks.";
        PlaceholderStatusText.Text = "No client files are accessed or cached in this preview.";
    }

    private void Queries_Click(object sender, RoutedEventArgs e)
    {
        Navigate(PlaceholderContent, QueriesNavButton, "Queries", "The shared query inbox is not connected.");
        PlaceholderEyebrow.Text = "SHARED WORK · DISABLED";
        PlaceholderTitle.Text = "Queries";
        PlaceholderDescription.Text = "Query assignment and responses will be enabled after the current endpoints, staff assignment rules, and audit behavior are reviewed.";
        PlaceholderStatusText.Text = "No shared records are changed from this screen.";
    }

    private void Admin_Click(object sender, RoutedEventArgs e)
    {
        Navigate(PlaceholderContent, AdminNavButton, "Administration", "Administrative controls remain locked.");
        PlaceholderEyebrow.Text = "ADMIN CONTROLS · DISABLED";
        PlaceholderTitle.Text = "Administration";
        PlaceholderDescription.Text = "Admin actions require verified portal role and MFA state, a reviewed server-side authorization path, and an explicit operational approval.";
        PlaceholderStatusText.Text = "No production controls are available in this preview.";
    }

    private void UpdateOverview(WorkQueueSnapshot snapshot)
    {
        var openItems = snapshot.Items.Where(item => !item.IsCompleted).ToArray();
        OpenReminderCountText.Text = openItems.Length.ToString();
        OpenReminderSummaryText.Text = openItems.Length == 1
            ? "open personal reminder"
            : "open personal reminders";

        var next = openItems
            .Where(item => item.DueOn is not null)
            .OrderBy(item => item.DueOn)
            .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();

        if (next is null)
        {
            NextReminderTitleText.Text = openItems.Length == 0 ? "You're all caught up" : "No due dates yet";
            NextReminderDueText.Text = openItems.Length == 0
                ? "No open personal reminders."
                : "Add a date from your work queue when it helps.";
            return;
        }

        NextReminderTitleText.Text = next.Title;
        var today = DateOnly.FromDateTime(DateTime.Today);
        NextReminderDueText.Text = next.DueOn < today
            ? $"Overdue · {next.DueDateLabel}"
            : $"Due {next.DueDateLabel}";
    }

    private void ShowQueueUnavailable()
    {
        OpenReminderCountText.Text = "Unavailable";
        OpenReminderSummaryText.Text = "Your local reminder list could not be read.";
        NextReminderTitleText.Text = "Reminder file unavailable";
        NextReminderDueText.Text = "The existing file was left untouched.";
    }

    private void TaxAutomation_Click(object sender, RoutedEventArgs e)
    {
        var window = new TaxAutomationWindow { Owner = this };
        window.ShowDialog();
    }

    private void PortalSync_Click(object sender, RoutedEventArgs e)
    {
        var window = new PortalSyncWindow { Owner = this };
        window.ShowDialog();
    }
}
