using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Kka.Desk.Services;

/// <summary>
/// Read-only adapter for the existing KKA portal. It accepts a token from a separately
/// reviewed sign-in flow and never implements or bypasses the portal's MFA gate.
/// Returned rows remain subject to Supabase Row Level Security.
/// </summary>
public sealed class PortalSyncService(HttpClient httpClient, PortalSyncOptions options)
{
    public async Task<PortalSyncResult> LoadDirectoryAsync(
        string accessToken,
        bool portalMfaSatisfied,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new PortalSyncException("Sign in to the KKA portal before synchronising.");
        if (!portalMfaSatisfied)
            throw new PortalSyncException("Portal verification has not been confirmed. Synchronisation was blocked.");
        if (string.IsNullOrWhiteSpace(options.PublishableKey))
            throw new PortalSyncException("The Supabase publishable key is not configured.");

        var baseUrl = options.SupabaseUrl.TrimEnd('/');
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps && !options.AllowInsecureLocalDevelopment))
            throw new PortalSyncException("The portal connection must use HTTPS.");

        var userUri = new Uri(baseUri, "/auth/v1/user");
        using var userResponse = await SendAsync(userUri, accessToken, cancellationToken);
        if (userResponse.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new PortalSyncException("The portal session is expired or invalid. Sign in again.");
        userResponse.EnsureSuccessStatusCode();
        using var userJson = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync(cancellationToken));
        if (!userJson.RootElement.TryGetProperty("id", out var userIdElement) ||
            string.IsNullOrWhiteSpace(userIdElement.GetString()))
            throw new PortalSyncException("The portal did not return a valid authenticated user.");
        var userId = userIdElement.GetString()!;

        var profileUri = new Uri(baseUri,
            "/rest/v1/profiles?id=eq." + Uri.EscapeDataString(userId) +
            "&select=role,active,full_name,email_2fa_enabled");
        using var profileResponse = await SendAsync(profileUri, accessToken, cancellationToken);
        profileResponse.EnsureSuccessStatusCode();
        using var profileJson = JsonDocument.Parse(await profileResponse.Content.ReadAsStringAsync(cancellationToken));
        var profile = profileJson.RootElement.EnumerateArray().FirstOrDefault();
        if (profile.ValueKind != JsonValueKind.Object)
            throw new PortalSyncException("No active KKA portal profile was found.");

        var role = ReadString(profile, "role");
        if (ReadBool(profile, "active") != true || role is not ("admin" or "staff"))
            throw new PortalSyncException("KKA Desk is restricted to active staff and administrators.");

        // The caller must only set portalMfaSatisfied after the existing portal
        // verification flow has completed; password authentication alone is insufficient.
        if (ReadBool(profile, "email_2fa_enabled") == true && !portalMfaSatisfied)
            throw new PortalSyncException("Complete the portal's configured verification before syncing.");

        var membershipsUri = new Uri(baseUri,
            "/rest/v1/client_account_members?select=account_id,client_id,relationship,is_primary,active," +
            "clients(id,display_name,legal_name,pan,tan,cin,gstin,mobile,active)&active=eq.true");
        using var membershipsResponse = await SendAsync(membershipsUri, accessToken, cancellationToken);
        membershipsResponse.EnsureSuccessStatusCode();
        using var membershipsJson = JsonDocument.Parse(
            await membershipsResponse.Content.ReadAsStringAsync(cancellationToken));

        var memberships = new List<PortalFamilyProfile>();
        foreach (var item in membershipsJson.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("clients", out var clientNode) ||
                clientNode.ValueKind != JsonValueKind.Object ||
                ReadBool(clientNode, "active") == false)
                continue;

            var clientId = ReadString(item, "client_id");
            var accountId = ReadString(item, "account_id");
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(accountId))
                continue;

            memberships.Add(new PortalFamilyProfile(
                accountId, clientId, ReadString(item, "relationship"),
                ReadBool(item, "is_primary") == true,
                ReadString(clientNode, "display_name"), ReadString(clientNode, "legal_name"),
                ReadString(clientNode, "pan"), ReadString(clientNode, "tan"),
                ReadString(clientNode, "cin"), ReadString(clientNode, "gstin"),
                ReadString(clientNode, "mobile")));
        }

        return new PortalSyncResult(
            userId, role,
            memberships.OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.DisplayName ?? x.LegalName, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            DateTimeOffset.UtcNow);
    }

    private async Task<HttpResponseMessage> SendAsync(
        Uri uri, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("apikey", options.PublishableKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await httpClient.SendAsync(request, cancellationToken);
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? ReadBool(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
}

public sealed record PortalSyncOptions(
    string SupabaseUrl,
    string PublishableKey,
    bool AllowInsecureLocalDevelopment = false);

public sealed record PortalFamilyProfile(
    string FamilyAccountId,
    string ProfileId,
    string? Relationship,
    bool IsPrimary,
    string? DisplayName,
    string? LegalName,
    string? Pan,
    string? Tan,
    string? Cin,
    string? Gstin,
    string? Mobile);

public sealed record PortalSyncResult(
    string AuthenticatedUserId,
    string Role,
    IReadOnlyList<PortalFamilyProfile> Profiles,
    DateTimeOffset SyncedAtUtc);

public sealed class PortalSyncException(string message) : Exception(message);
