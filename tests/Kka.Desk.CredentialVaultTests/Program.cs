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
    if (!condition) throw new InvalidOperationException("Credential vault test failed: " + name);
    checks++;
    Console.WriteLine("PASS: " + name);
}

try
{
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

    Console.WriteLine($"PASS: all {checks} Windows Credential Manager checks completed.");
}
finally
{
    // Ensure the ephemeral runner's dummy entries are cleaned up even if a check fails.
    vault.Remove(familyId, profileA, "IncomeTax");
    vault.Remove(familyId, profileA, "GST", "03AAAAA0000A1Z5");
}
