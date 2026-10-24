using System.Windows;

namespace Kka.Desk;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void ShowModule(string title, string description)
    {
        PageTitle.Text = title;
        PanelTitle.Text = title;
        PanelDescription.Text = description;
    }

    private void Overview_Click(object sender, RoutedEventArgs e) =>
        ShowModule("Good day.", "Your firm's work, organised in one place. Dashboard data will appear after secure backend integration.");

    private void Documents_Click(object sender, RoutedEventArgs e) =>
        ShowModule("Documents", "Document listing, secure upload, version history, and PDF viewing are planned. This preview does not access client files.");

    private void Queries_Click(object sender, RoutedEventArgs e) =>
        ShowModule("Queries", "The staff query inbox will be connected after endpoint permissions and assignment rules are reviewed.");

    private void Directory_Click(object sender, RoutedEventArgs e) =>
        ShowModule("Client directory", "The directory will enforce staff-to-client assignments on the server. No client records are loaded in this preview.");

    private void Queue_Click(object sender, RoutedEventArgs e) =>
        ShowModule("Work queue", "A unified queue for pending tasks and deadlines is planned for a later phase.");

    private void Admin_Click(object sender, RoutedEventArgs e) =>
        ShowModule("Administration", "Admin controls will be available only after role and MFA requirements are verified.");

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
