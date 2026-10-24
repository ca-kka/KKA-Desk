using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Kka.Desk.Services;

namespace Kka.Desk;

public partial class TaxAutomationWindow : Window
{
    private readonly WindowsCredentialVault _credentialVault = new();
    private PortalFamilyProfile? _profile;

    public TaxAutomationWindow()
    {
        InitializeComponent();
        Closed += TaxAutomationWindow_Closed;
    }

    public TaxAutomationWindow(PortalFamilyProfile profile) : this()
    {
        ArgumentNullException.ThrowIfNull(profile);
        _profile = profile;
        FamilyAccountIdBox.Text = profile.FamilyAccountId;
        ProfileIdBox.Text = profile.ProfileId;
        ProfileNameBox.Text = profile.DisplayName ?? profile.LegalName ?? string.Empty;
        PanBox.Text = profile.Pan ?? string.Empty;
        GstinBox.Text = profile.Gstin ?? string.Empty;
        FamilyAccountIdBox.IsReadOnly = true;
        ProfileIdBox.IsReadOnly = true;
        ProfileNameBox.IsReadOnly = true;
        GstinBox.IsReadOnly = true;
        ProfileTypeBox.IsEnabled = false;
        ProfileTypeBox.SelectedIndex = profile.IsPrimary ? 0 : 1;
        IncomeTaxEnabled.IsChecked = !string.IsNullOrWhiteSpace(profile.Pan);
        GstEnabled.IsChecked = !string.IsNullOrWhiteSpace(profile.Gstin);
        if (string.IsNullOrWhiteSpace(profile.Pan))
            AuditCase.IsChecked = false;
        ConfigureCredentialControls();
    }

    private void ConfigureCredentialControls()
    {
        var hasProfile = _profile is not null;
        IncomeTaxLoginBox.IsEnabled = hasProfile && !string.IsNullOrWhiteSpace(_profile?.Pan);
        IncomeTaxPasswordBox.IsEnabled = IncomeTaxLoginBox.IsEnabled;
        SaveIncomeTaxCredentialButton.IsEnabled = IncomeTaxLoginBox.IsEnabled;
        GstLoginBox.IsEnabled = hasProfile && !string.IsNullOrWhiteSpace(_profile?.Gstin);
        GstPasswordBox.IsEnabled = GstLoginBox.IsEnabled;
        SaveGstCredentialButton.IsEnabled = GstLoginBox.IsEnabled;
        if (_profile is null)
        {
            CredentialStatusText.Text = "Select a profile through live portal sync before managing credentials.";
            return;
        }

        try
        {
            var incomeTaxSaved = !string.IsNullOrWhiteSpace(_profile.Pan) &&
                _credentialVault.Exists(_profile.FamilyAccountId, _profile.ProfileId, "INCOME_TAX");
            var gstSaved = !string.IsNullOrWhiteSpace(_profile.Gstin) &&
                _credentialVault.Exists(_profile.FamilyAccountId, _profile.ProfileId, "GST", _profile.Gstin);
            RemoveIncomeTaxCredentialButton.IsEnabled = incomeTaxSaved;
            RemoveGstCredentialButton.IsEnabled = gstSaved;
            CredentialStatusText.Text = $"Income Tax: {(incomeTaxSaved ? "configured" : "not configured")}. GST registration {_profile.Gstin ?? "(none)"}: {(gstSaved ? "configured" : "not configured")}. Secrets remain in Windows Credential Manager.";
        }
        catch
        {
            RemoveIncomeTaxCredentialButton.IsEnabled = false;
            RemoveGstCredentialButton.IsEnabled = false;
            CredentialStatusText.Text = "Windows Credential Manager is unavailable. No credential status could be confirmed.";
        }
    }

    private void SaveIncomeTaxCredential_Click(object sender, RoutedEventArgs e)
    {
        if (_profile is null || string.IsNullOrWhiteSpace(_profile.Pan))
        {
            CredentialStatusText.Text = "A portal-synced profile with a PAN is required.";
            return;
        }

        try
        {
            var exists = _credentialVault.Exists(_profile.FamilyAccountId, _profile.ProfileId, "INCOME_TAX");
            if (exists && !ConfirmCredentialReplacement("Income Tax"))
            {
                IncomeTaxPasswordBox.Clear();
                IncomeTaxLoginBox.Clear();
                CredentialStatusText.Text = "The existing Income Tax credential was left unchanged.";
                return;
            }

            _credentialVault.Save(_profile.FamilyAccountId, _profile.ProfileId, "INCOME_TAX", null,
                IncomeTaxLoginBox.Text, IncomeTaxPasswordBox.Password);
            IncomeTaxPasswordBox.Clear();
            IncomeTaxLoginBox.Clear();
            ConfigureCredentialControls();
            CredentialStatusText.Text = "Income Tax credential saved to this Windows user's Credential Manager.";
        }
        catch
        {
            IncomeTaxPasswordBox.Clear();
            CredentialStatusText.Text = "Could not save the Income Tax credential. Check the login ID, password, and Windows Credential Manager.";
        }
    }

    private void RemoveIncomeTaxCredential_Click(object sender, RoutedEventArgs e)
    {
        if (_profile is null) return;
        if (!ConfirmAction("Remove saved credential",
                "Remove the saved Income Tax credential for the selected profile? You will need to save it again before use.",
                "Remove credential", "Keep credential"))
            return;

        try
        {
            var removed = _credentialVault.Remove(_profile.FamilyAccountId, _profile.ProfileId, "INCOME_TAX");
            ConfigureCredentialControls();
            CredentialStatusText.Text = removed
                ? "Saved Income Tax credential removed from Windows Credential Manager."
                : "No saved Income Tax credential was found for this profile.";
        }
        catch
        {
            CredentialStatusText.Text = "Could not remove the saved Income Tax credential.";
        }
    }

    private void SaveGstCredential_Click(object sender, RoutedEventArgs e)
    {
        if (_profile is null || string.IsNullOrWhiteSpace(_profile.Gstin))
        {
            CredentialStatusText.Text = "A portal-synced profile with a GSTIN is required.";
            return;
        }

        try
        {
            var exists = _credentialVault.Exists(_profile.FamilyAccountId, _profile.ProfileId, "GST", _profile.Gstin);
            if (exists && !ConfirmCredentialReplacement("GST"))
            {
                GstPasswordBox.Clear();
                GstLoginBox.Clear();
                CredentialStatusText.Text = "The existing GST credential was left unchanged.";
                return;
            }

            _credentialVault.Save(_profile.FamilyAccountId, _profile.ProfileId, "GST", _profile.Gstin,
                GstLoginBox.Text, GstPasswordBox.Password);
            GstPasswordBox.Clear();
            GstLoginBox.Clear();
            ConfigureCredentialControls();
            CredentialStatusText.Text = $"GST credential saved for registration {_profile.Gstin} in Windows Credential Manager.";
        }
        catch
        {
            GstPasswordBox.Clear();
            CredentialStatusText.Text = "Could not save the GST credential. Check the login ID, password, and Windows Credential Manager.";
        }
    }

    private void RemoveGstCredential_Click(object sender, RoutedEventArgs e)
    {
        if (_profile is null || string.IsNullOrWhiteSpace(_profile.Gstin)) return;
        if (!ConfirmAction("Remove saved credential",
                "Remove the saved GST credential for the selected profile and registration? You will need to save it again before use.",
                "Remove credential", "Keep credential"))
            return;

        try
        {
            var removed = _credentialVault.Remove(_profile.FamilyAccountId, _profile.ProfileId, "GST", _profile.Gstin);
            ConfigureCredentialControls();
            CredentialStatusText.Text = removed
                ? "Saved GST credential removed from Windows Credential Manager."
                : "No saved GST credential was found for this profile.";
        }
        catch
        {
            CredentialStatusText.Text = "Could not remove the saved GST credential.";
        }
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

    private bool ConfirmCredentialReplacement(string taxSystem) => ConfirmAction(
        "Replace saved credential",
        $"A saved {taxSystem} credential exists for this profile. Replace it with the value you entered?",
        "Replace credential", "Keep existing");

    private bool ConfirmAction(string title, string message, string confirmLabel, string cancelLabel) =>
        new ConfirmationDialog(this, title, message, confirmLabel, cancelLabel).ShowDialog() == true;

    private void TaxAutomationWindow_Closed(object? sender, EventArgs e)
    {
        IncomeTaxLoginBox.Clear();
        IncomeTaxPasswordBox.Clear();
        GstLoginBox.Clear();
        GstPasswordBox.Clear();
        FamilyAccountIdBox.Clear();
        ProfileIdBox.Clear();
        ProfileNameBox.Clear();
        PanBox.Clear();
        GstinBox.Clear();
        _profile = null;
    }
}

public sealed record TaxTaskPreview(string TaxType, string Document, string Period, string Status);
