using System.Net;
using System.Text;
using Kka.Desk.Services;

var vault = new WindowsCredentialVault();
var familyId = "ci-family-" + Guid.NewGuid().ToString("N");
var profileA = "ci-profile-a-" + Guid.NewGuid().ToString("N");
var profileB = "ci-profile-b-" + Guid.NewGuid().ToString("N");
const string dummyLogin = "kka-ci-dummy-login";
const string dummyPassword = "NotARealPassword!CI-Only-123";
var queueTestDirectory = Path.Combine(Environment.CurrentDirectory, $".queue-tests-{Guid.NewGuid():N}");
var queueOnly = args.Contains("--queue-only", StringComparer.OrdinalIgnoreCase);
var credentialCleanupRequired = false;

var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("KKA Desk integration test failed: " + name);
    checks++;
    Console.WriteLine("PASS: " + name);
}

try
{
    Directory.CreateDirectory(queueTestDirectory);
    var queueFile = Path.Combine(queueTestDirectory, "personal-work-queue.json");
    var fixedNow = new DateTimeOffset(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);
    var queueStore = new PersonalWorkQueueStore(queueFile, new FixedTimeProvider(fixedNow));
    Check(queueStore.Load().Items.Count == 0 && !File.Exists(queueFile),
        "personal reminder queue starts empty without creating client-data cache files");

    var reminder = queueStore.Add("Prepare weekly team rota", WorkItemCategory.Operations,
        WorkItemPriority.Normal, new DateOnly(2026, 11, 2));
    Check(reminder.Title == "Prepare weekly team rota" && reminder.CreatedAtUtc == fixedNow,
        "generic personal reminder is normalized and timestamped");
    var reloadedQueue = new PersonalWorkQueueStore(queueFile, new FixedTimeProvider(fixedNow)).Load();
    Check(reloadedQueue.Items.Count == 1 && reloadedQueue.Items[0].Id == reminder.Id &&
          reloadedQueue.Items[0].DueOn == new DateOnly(2026, 11, 2),
        "personal reminders persist and reload from the isolated local file");
    Check(reloadedQueue.AuditEvents.Count == 1 && reloadedQueue.AuditEvents[0].Action == WorkQueueAction.Created &&
          !reloadedQueue.AuditEvents[0].ToString()!.Contains(reminder.Title, StringComparison.Ordinal),
        "audit history records the action without copying reminder text");

    var concurrentFile = Path.Combine(queueTestDirectory, "concurrent-queue.json");
    var concurrentStoreA = new PersonalWorkQueueStore(concurrentFile, new FixedTimeProvider(fixedNow));
    var concurrentStoreB = new PersonalWorkQueueStore(concurrentFile, new FixedTimeProvider(fixedNow));
    var concurrentWrites = Enumerable.Range(0, 12)
        .Select(index => Task.Run(() =>
            (index % 2 == 0 ? concurrentStoreA : concurrentStoreB).Add(
                $"Prepare review cycle {index}", WorkItemCategory.Operations,
                WorkItemPriority.Normal, null)))
        .ToArray();
    await Task.WhenAll(concurrentWrites);
    var concurrentSnapshot = new PersonalWorkQueueStore(concurrentFile).Load();
    Check(concurrentSnapshot.Items.Count == 12 && concurrentSnapshot.AuditEvents.Count == 12,
        "concurrent reminder writes from separate app instances do not overwrite each other");

    Check(queueStore.SetCompleted(reminder.Id, true) && queueStore.Load().Items.Single().IsCompleted,
        "personal reminder can be completed and audited");
    Check(queueStore.SetCompleted(reminder.Id, false) && !queueStore.Load().Items.Single().IsCompleted,
        "completed reminder can be reopened and audited");
    Check(queueStore.Delete(reminder.Id) && queueStore.Load().Items.Count == 0 &&
          queueStore.Load().AuditEvents.Last().Action == WorkQueueAction.Deleted,
        "personal reminder can be deleted with an audit event");

    foreach (var unsafeTitle in new[]
    {
        "Review ABCDE1234F", "Check GSTIN 03AAAAA0000A1Z5", "Follow up test@example.invalid",
        "Call +91 90000 00000", "Check 000000000000"
    })
    {
        var identifierRejected = false;
        try { PersonalWorkItemRules.NormalizeTitle(unsafeTitle); }
        catch (ArgumentException) { identifierRejected = true; }
        Check(identifierRejected, "personal reminders reject common client contact and tax identifiers");
    }

    var multilineRejected = false;
    try { PersonalWorkItemRules.NormalizeTitle("First line\nSecond line"); }
    catch (ArgumentException) { multilineRejected = true; }
    Check(multilineRejected, "personal reminder titles must stay on one line");

    var invalidPriorityRejected = false;
    try { queueStore.Add("Prepare weekly team rota", WorkItemCategory.Operations, (WorkItemPriority)99, null); }
    catch (ArgumentOutOfRangeException) { invalidPriorityRejected = true; }
    Check(invalidPriorityRejected, "unsupported reminder priority is rejected");

    var corruptFile = Path.Combine(queueTestDirectory, "corrupt.json");
    const string corruptJson = "{ not valid json";
    File.WriteAllText(corruptFile, corruptJson);
    var corruptDataRejected = false;
    try { new PersonalWorkQueueStore(corruptFile).Load(); }
    catch (InvalidDataException) { corruptDataRejected = true; }
    Check(corruptDataRejected && File.ReadAllText(corruptFile) == corruptJson,
        "malformed local reminder data fails closed without being overwritten");
    Check(!Directory.EnumerateFiles(queueTestDirectory, "*.tmp", SearchOption.TopDirectoryOnly).Any(),
        "atomic local reminder writes leave no temporary files behind");

    if (queueOnly)
    {
        Console.WriteLine($"PASS: all {checks} isolated personal reminder checks completed.");
        return;
    }

    // All credential entries below use random CI-only account/profile IDs and dummy values.
    vault.Save(familyId, profileA, "IncomeTax", null, dummyLogin, dummyPassword);
    credentialCleanupRequired = true;
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

    Console.WriteLine($"PASS: all {checks} local reminder, Windows and mocked-auth integration checks completed.");
}
finally
{
    // Ensure the ephemeral runner's dummy entries are cleaned up even if a check fails.
    if (credentialCleanupRequired)
    {
        try
        {
            vault.Remove(familyId, profileA, "IncomeTax");
            vault.Remove(familyId, profileA, "GST", "03AAAAA0000A1Z5");
        }
        catch (Exception)
        {
            Console.Error.WriteLine("FAIL: dummy Credential Manager entries could not be cleaned up.");
            Environment.ExitCode = 1;
        }
    }
    if (Directory.Exists(queueTestDirectory))
        Directory.Delete(queueTestDirectory, recursive: true);
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

sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
