using System.Net;
using System.Text;
using Kka.Desk.Services;

var vault = new WindowsCredentialVault();
var familyId = "ci-family-" + Guid.NewGuid().ToString("N");
var profileA = "ci-profile-a-" + Guid.NewGuid().ToString("N");
var profileB = "ci-profile-b-" + Guid.NewGuid().ToString("N");
const string dummyLogin = "kka-ci-dummy-login";
const string dummyPassword = "NotARealPassword!CI-Only-123";

var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("KKA Desk integration test failed: " + name);
    checks++;
    Console.WriteLine("PASS: " + name);
}

try
{
    // All credential entries below use random CI-only account/profile IDs and dummy values.
    vault.Save(familyId, profileA, "IncomeTax", null, dummyLogin, dummyPassword);
    Check(vault.Exists(familyId, profileA, "IncomeTax"), "Income Tax credential can be saved and found");
    Check(!vault.Exists(familyId, profileB, "IncomeTax"), "credential is isolated from another member profile");

    vault.Save(familyId, profileA, "GST", "03AAAAA0000A1Z5", dummyLogin, dummyPassword);
    Check(vault.Exists(familyId, profileA, "GST", "03AAAAA0000A1Z5"), "GST credential can be saved for its registration");
    Check(!vault.Exists(familyId, profileA, "GST", "03BBBBB0000B1Z6"), "GST credential is isolated by GSTIN");
    Check(!vault.Exists(familyId, profileB, "GST", "03AAAAA0000A1Z5"), "GST credential is isolated by member profile");

    Check(vault.Remove(familyId, profileA, "IncomeTax"), "Income Tax credential can be removed");
    Check(!vault.Exists(familyId, profileA, "IncomeTax"), "removed Income Tax credential no longer exists");
    Check(vault.Remove(familyId, profileA, "GST", "03AAAAA0000A1Z5"), "GST credential can be removed");
    Check(!vault.Exists(familyId, profileA, "GST", "03AAAAA0000A1Z5"), "removed GST credential no longer exists");

    var invalidLoginRejected = false;
    try { vault.Save(familyId, profileA, "IncomeTax", null, " ", dummyPassword); }
    catch (ArgumentException) { invalidLoginRejected = true; }
    Check(invalidLoginRejected, "empty login ID is rejected");

    var missingGstinRejected = false;
    try { vault.Save(familyId, profileA, "GST", null, dummyLogin, dummyPassword); }
    catch (ArgumentException) { missingGstinRejected = true; }
    Check(missingGstinRejected, "GST credential without a GSTIN is rejected");

    var mfaHandler = new FakeHttpMessageHandler(_ =>
        throw new InvalidOperationException("MFA rejection must happen before any network request."));
    using (var mfaHttp = new HttpClient(mfaHandler))
    {
        var sync = new PortalSyncService(mfaHttp,
            new PortalSyncOptions("https://example.invalid", "ci-publishable-key"));
        var blocked = false;
        try { await sync.LoadDirectoryAsync("dummy-token", portalMfaSatisfied: false); }
        catch (PortalSyncException) { blocked = true; }
        Check(blocked && mfaHandler.RequestCount == 0,
            "directory sync refuses to run before the MFA gate is satisfied");
    }

    var staffHandler = new FakeHttpMessageHandler(request =>
    {
        if (request.RequestUri!.AbsolutePath == "/auth/v1/user")
            return JsonResponse("{\"id\":\"ci-user-id\"}");
        if (request.RequestUri.AbsolutePath.StartsWith("/rest/v1/profiles", StringComparison.Ordinal))
            return JsonResponse("[{\"role\":\"staff\",\"active\":true,\"full_name\":\"CI Test\",\"email_2fa_enabled\":true}]");
        return JsonResponse("[]");
    });
    using (var staffHttp = new HttpClient(staffHandler))
    {
        var sync = new PortalSyncService(staffHttp,
            new PortalSyncOptions("https://example.invalid", "ci-publishable-key"));
        var staffBlocked = false;
        try { await sync.LoadDirectoryAsync("dummy-token", portalMfaSatisfied: true); }
        catch (PortalSyncException) { staffBlocked = true; }
        Check(staffBlocked && staffHandler.RequestCount == 2,
            "staff role is rejected before client/account membership data is requested");
    }

    var otpHandler = new FakeHttpMessageHandler(_ =>
        JsonResponse("{\"requiresOtp\":true,\"challengeId\":\"ci-dummy-challenge\",\"maskedEmail\":\"c***@example.invalid\"}"));
    using (var otpHttp = new HttpClient(otpHandler))
    {
        var auth = new PortalAuthService(otpHttp,
            new PortalSyncOptions("https://example.invalid", "ci-publishable-key"));
        var attempt = await auth.BeginSignInAsync("ci-dummy@example.invalid", "dummy-password");
        Check(attempt.RequiresOtp && attempt.ChallengeId == "ci-dummy-challenge",
            "portal sign-in preserves an OTP challenge response");

        var invalidOtpRejected = false;
        try
        {
            await auth.VerifyOtpAsync("ci-dummy@example.invalid", "dummy-password",
                "ci-dummy-challenge", "12");
        }
        catch (PortalSyncException) { invalidOtpRejected = true; }
        Check(invalidOtpRejected && otpHandler.RequestCount == 1,
            "invalid OTP format is rejected without sending a verification request");
    }

    Console.WriteLine($"PASS: all {checks} Windows and mocked-auth integration checks completed.");
}
finally
{
    // Ensure the ephemeral runner's dummy entries are cleaned up even if a check fails.
    vault.Remove(familyId, profileA, "IncomeTax");
    vault.Remove(familyId, profileA, "GST", "03AAAAA0000A1Z5");
}

static HttpResponseMessage JsonResponse(string json) =>
    new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        return Task.FromResult(responder(request));
    }
}
