using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Kka.Desk.Services;

namespace Kka.Desk;

public partial class TaxAutomationWindow : Window
{
    public TaxAutomationWindow()
    {
        InitializeComponent();
    }

    public TaxAutomationWindow(PortalFamilyProfile profile) : this()
    {
        ArgumentNullException.ThrowIfNull(profile);
        FamilyAccountIdBox.Text = profile.FamilyAccountId;
        ProfileIdBox.Text = profile.ProfileId;
        ProfileNameBox.Text = profile.DisplayName ?? profile.LegalName ?? string.Empty;
        PanBox.Text = profile.Pan ?? string.Empty;
        GstinBox.Text = profile.Gstin ?? string.Empty;
        ProfileTypeBox.SelectedIndex = profile.IsPrimary ? 0 : 1;
        IncomeTaxEnabled.IsChecked = !string.IsNullOrWhiteSpace(profile.Pan);
        GstEnabled.IsChecked = !string.IsNullOrWhiteSpace(profile.Gstin);
        if (string.IsNullOrWhiteSpace(profile.Pan))
            AuditCase.IsChecked = false;
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        var familyAccountId = FamilyAccountIdBox.Text.Trim();
        var profileId = ProfileIdBox.Text.Trim();
        var profileName = ProfileNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(familyAccountId) ||
            string.IsNullOrWhiteSpace(profileId) ||
            string.IsNullOrWhiteSpace(profileName))
        {
            MessageBox.Show(this, "Select a profile from the live portal sync or enter the existing family/account ID, member profile ID, and profile name.",
                "Family profile details required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var tasks = new List<TaxTaskPreview>();
        var yearLabel = (FinancialYearBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tax year not selected";
        var gstEnabled = GstEnabled.IsChecked == true;
        var incomeTaxEnabled = IncomeTaxEnabled.IsChecked == true;
        var audit = AuditCase.IsChecked == true;
        var quarterly = (FrequencyBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.StartsWith("Quarterly", StringComparison.OrdinalIgnoreCase) == true;
        var scope = (PeriodScopeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

        if (incomeTaxEnabled)
        {
            if (string.IsNullOrWhiteSpace(PanBox.Text))
                tasks.Add(new TaxTaskPreview("Income Tax", "ITR return / acknowledgement", yearLabel, "Blocked — PAN not loaded"));
            else if (ItrDocs.IsChecked == true)
                tasks.Add(new TaxTaskPreview("Income Tax", "ITR return / acknowledgement", yearLabel, "Preview only"));
            if (audit && AuditDocs.IsChecked == true)
                tasks.Add(new TaxTaskPreview("Income Tax", "Audit report / available attachments", yearLabel, "Case check required"));
            else if (AuditDocs.IsChecked == true && !audit)
                tasks.Add(new TaxTaskPreview("Income Tax", "Audit documents", yearLabel, "Skipped — audit not confirmed"));
        }

        if (gstEnabled && (Gstr1Docs.IsChecked == true || Gstr3bDocs.IsChecked == true))
        {
            if (string.IsNullOrWhiteSpace(GstinBox.Text))
                tasks.Add(new TaxTaskPreview("GST", "GST return documents", yearLabel, "Blocked — GSTIN not loaded"));
            else
            {
                foreach (var period in GetPeriods(quarterly, scope))
                {
                    if (Gstr1Docs.IsChecked == true)
                        tasks.Add(new TaxTaskPreview("GST", "GSTR-1", period, "Preview only"));
                    if (Gstr3bDocs.IsChecked == true)
                        tasks.Add(new TaxTaskPreview("GST", "GSTR-3B", period, "Preview only"));
                }
            }
        }

        TasksGrid.ItemsSource = tasks;
        TaskCountText.Text = $"{tasks.Count} task(s)";
        var root = $"<configured-root>/{SafeSegment(familyAccountId)}/{SafeSegment(profileId)}";
        var incomePath = $"{root}/Income Tax/{SafeSegment(yearLabel)}";
        var gstPath = $"{root}/GST/{SafeSegment(yearLabel)}";
        var profileType = (ProfileTypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Profile";
        PunchPreviewText.Text = $"Family/account: {familyAccountId}\nSelected profile: {profileName} ({profileId}) — {profileType}\nIncome Tax destination: {incomePath}\nGST destination: {gstPath}\n\nThis profile is isolated from other family members. Paths are illustrative only; no files are created or moved.";
        StatusText.Text = $"Preview created for {tasks.Count} task(s). Portal downloads and Punch remain disabled.";
    }

    private static IEnumerable<string> GetPeriods(bool quarterly, string scope)
    {
        if (scope.StartsWith("Latest", StringComparison.OrdinalIgnoreCase))
            return new[] { quarterly ? "Latest applicable quarter" : "Latest applicable month" };
        if (scope.StartsWith("Select", StringComparison.OrdinalIgnoreCase))
            return new[] { "Manual period selection required" };
        if (quarterly)
            return new[] { "Apr–Jun", "Jul–Sep", "Oct–Dec", "Jan–Mar" };
        return new[] { "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar" };
    }

    private static string SafeSegment(string value)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var safe = new string(value.Where(ch => !invalid.Contains(ch) && ch != '/' && ch != '\\').ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "Unspecified" : safe;
    }

    private void Run_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this, "Portal downloads are intentionally disabled until secure authentication, human-assisted OTP/CAPTCHA, file validation, and credential storage are implemented.",
            "Not available in preview", MessageBoxButton.OK, MessageBoxImage.Information);

    private void Punch_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this, "Punch Documents will be enabled only after download verification and the approved destination adapter are implemented.",
            "Not available in preview", MessageBoxButton.OK, MessageBoxImage.Information);
}

public sealed record TaxTaskPreview(string TaxType, string Document, string Period, string Status);
