using System.Windows;
using System.Windows.Automation;

namespace Kka.Desk;

public partial class ConfirmationDialog : Window
{
    public ConfirmationDialog(
        Window? owner,
        string title,
        string message,
        string confirmLabel,
        string cancelLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(cancelLabel);

        InitializeComponent();
        Owner = owner;
        Title = $"{title} | KKA Desk";
        AutomationProperties.SetName(this, title);
        HeadingText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmLabel;
        CancelButton.Content = cancelLabel;
        AutomationProperties.SetName(ConfirmButton, confirmLabel);
        AutomationProperties.SetName(CancelButton, cancelLabel);
        Loaded += (_, _) => CancelButton.Focus();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
