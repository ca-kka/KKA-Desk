using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Kka.Desk.Services;

/// <summary>
/// Mirrors the portal's existing client-login OTP gate for desktop sign-in.
/// Passwords are used only for the request and are never persisted or logged.
/// </summary>
public sealed class PortalAuthService(HttpClient httpClient, PortalSyncOptions options)
{
    public async Task<PortalLoginAttempt> BeginSignInAsync(
        string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            throw new PortalSyncException("Enter your registered email and password.");

        var result = await PostFunctionAsync(new
        {
            action = "request",
            email = email.Trim().ToLowerInvariant(),
            password
        }, cancellationToken);

        if (result.TryGetProperty("requiresOtp", out var otp) && otp.ValueKind == JsonValueKind.True)
        {
            var challengeId = ReadString(result, "challengeId");
            if (string.IsNullOrWhiteSpace(challengeId))
                throw new PortalSyncException("The portal did not return a valid verification challenge.");

            return new PortalLoginAttempt(
                null,
                challengeId,
                ReadString(result, "maskedEmail"),
                true);
        }

        return new PortalLoginAttempt(
            await PasswordSignInAsync(email.Trim(), password, cancellationToken),
            null,
            null,
            false);
    }

    public async Task<PortalLoginAttempt> VerifyOtpAsync(
        string email,
        string password,
        string challengeId,
        string otp,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(challengeId) || !System.Text.RegularExpressions.Regex.IsMatch(otp, "^\\d{6}$"))
            throw new PortalSyncException("Enter the 6-digit portal verification code.");

        var result = await PostFunctionAsync(new
        {
            action = "verify",
            email = email.Trim().ToLowerInvariant(),
            password,
            challengeId,
            otp
        }, cancellationToken);

        if (!result.TryGetProperty("session", out var session))
            throw new PortalSyncException("The portal did not return a verified sign-in session.");

        var accessToken = ReadString(session, "access_token");
        var refreshToken = ReadString(session, "refresh_token");
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
            throw new PortalSyncException("The verified portal session was incomplete.");

        return new PortalLoginAttempt(new PortalAccessSession(accessToken, refreshToken), null,
            ReadString(result, "maskedEmail"), false);
    }

    private async Task<PortalAccessSession> PasswordSignInAsync(
        string email, string password, CancellationToken cancellationToken)
    {
        var uri = new Uri(new Uri(options.SupabaseUrl.TrimEnd('/') + "/"),
            "auth/v1/token?grant_type=password");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.TryAddWithoutValidation("apikey", options.PublishableKey);
        request.Content = new StringContent(JsonSerializer.Serialize(new { email, password }),
            Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new PortalSyncException("Sign-in failed. Check your credentials and try again.");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var accessToken = ReadString(json.RootElement, "access_token");
        var refreshToken = ReadString(json.RootElement, "refresh_token");
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
            throw new PortalSyncException("The portal did not return a valid sign-in session.");
        return new PortalAccessSession(accessToken, refreshToken);
    }

    private async Task<JsonElement> PostFunctionAsync(object payload, CancellationToken cancellationToken)
    {
        var uri = new Uri(new Uri(options.SupabaseUrl.TrimEnd('/') + "/"),
            "functions/v1/client-login-otp");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.TryAddWithoutValidation("apikey", options.PublishableKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!response.IsSuccessStatusCode)
            throw new PortalSyncException("KKA portal verification failed. Check your credentials and try again.");
        if (json.RootElement.TryGetProperty("error", out var error) &&
            error.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(error.GetString()))
            throw new PortalSyncException("KKA portal verification could not be completed.");
        return json.RootElement.Clone();
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

public sealed record PortalAccessSession(string AccessToken, string RefreshToken);

public sealed record PortalLoginAttempt(
    PortalAccessSession? Session,
    string? ChallengeId,
    string? MaskedEmail,
    bool RequiresOtp);
