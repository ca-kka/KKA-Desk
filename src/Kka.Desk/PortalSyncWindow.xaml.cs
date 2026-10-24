using System;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kka.Desk.Services;

namespace Kka.Desk;

public partial class PortalSyncWindow : Window
{
    private const string SupabaseUrl = "https://wvyjyncgxtstyquecfdg.supabase.co";
    private const string PublishableKey = "sb_publishable_bz_IaZtgADuIBvBca25h7g_G0xLb_T3";
    private readonly HttpClient _httpClient = new();
    private readonly PortalSyncOptions _options = new(SupabaseUrl, PublishableKey);
    private readonly PortalAuthService _authService;
    private readonly PortalSyncService _syncService;
    private string? _challengeId;
    private string? _pendingEmail;
    private string? _pendingPassword;
    private PortalAccessSession? _session;

    public PortalSyncWindow()
    {
        InitializeComponent();
        _authService = new PortalAuthService(_httpClient, _options);
        _syncService = new PortalSyncService(_httpClient, _options);
    }

    private async void SignIn_Click(object sender, RoutedEventArgs e)
    {
        if (SignInButton.IsEnabled == false) return;
        SetBusy(true, "Checking credentials with the KKA portal…");
        try
        {
            _pendingEmail = EmailBox.Text.Trim();
            _pendingPassword = PasswordBox.Password;
            var attempt = await _authService.BeginSignInAsync(_pendingEmail, _pendingPassword);
            if (attempt.RequiresOtp)
            {
                _challengeId = attempt.ChallengeId;
                OtpHintText.Text = string.IsNullOrWhiteSpace(attempt.MaskedEmail)
                    ? "Enter the 6-digit code sent to your registered email."
                    : $"Enter the 6-digit code sent to {attempt.MaskedEmail}.";
                OtpPanel.Visibility = Visibility.Visible;
                OtpBox.Focus();
                StatusText.Text = "Verification required. No client data has been loaded yet.";
                return;
            }

            _session = attempt.Session;
            await SyncProfilesAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = SafeMessage(ex);
        }
        finally
        {
            SetBusy(false, StatusText.Text);
        }
    }

    private async void VerifyOtp_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_challengeId) ||
            string.IsNullOrWhiteSpace(_pendingEmail) ||
            string.IsNullOrEmpty(_pendingPassword))
        {
            StatusText.Text = "The verification challenge expired. Sign in again.";
            return;
        }

        SetBusy(true, "Verifying your KKA security code…");
        try
        {
            var attempt = await _authService.VerifyOtpAsync(
                _pendingEmail, _pendingPassword, _challengeId, OtpBox.Text.Trim());
            _session = attempt.Session;
            _challengeId = null;
            OtpPanel.Visibility = Visibility.Collapsed;
            await SyncProfilesAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = SafeMessage(ex);
            OtpBox.Clear();
            OtpBox.Focus();
        }
        finally
        {
            SetBusy(false, StatusText.Text);
        }
    }

    private async Task SyncProfilesAsync()
    {
        if (_session is null)
            throw new PortalSyncException("The portal did not provide an authenticated session.");

        StatusText.Text = "Checking active staff/admin permissions and reading family profiles…";
        var result = await _syncService.LoadDirectoryAsync(_session.AccessToken, portalMfaSatisfied: true);
        ProfilesGrid.ItemsSource = result.Profiles;
        StatusText.Text = $"Connected as {result.Role}. Loaded {result.Profiles.Count} active profile association(s). Last sync: {result.SyncedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}.";
        // Do not retain the password after a successful authentication. The access
        // token remains in memory only while this window is open.
        _pendingPassword = null;
        PasswordBox.Clear();
    }

    private void SetBusy(bool busy, string message)
    {
        SignInButton.IsEnabled = !busy;
        Mouse.OverrideCursor = busy ? Cursors.Wait : null;
        StatusText.Text = message;
    }

    private static string SafeMessage(Exception exception) =>
        exception is PortalSyncException ? exception.Message :
        "The portal connection failed. Check your network and try again; no client data was changed.";

    protected override void OnClosed(EventArgs e)
    {
        _session = null;
        _pendingPassword = null;
        _pendingEmail = null;
        _challengeId = null;
        _httpClient.Dispose();
        base.OnClosed(e);
    }
}
