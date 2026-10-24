using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kka.Desk.Services;

/// <summary>
/// Stores tax-portal credentials in the signed-in Windows user's Credential Manager.
/// Values are never written to app files, logs, or the KKA portal database.
/// </summary>
public sealed class WindowsCredentialVault
{
    private const uint GenericCredential = 1;
    private const uint PersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const int MaximumCredentialBlobBytes = 2560;

    public void Save(
        string familyAccountId,
        string profileId,
        string taxSystem,
        string? registrationId,
        string login,
        string password)
    {
        if (string.IsNullOrWhiteSpace(login))
            throw new ArgumentException("Enter the portal login ID.", nameof(login));
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Enter the portal password.", nameof(password));

        var target = BuildTarget(familyAccountId, profileId, taxSystem, registrationId);
        var secretBytes = JsonSerializer.SerializeToUtf8Bytes(new CredentialSecret(login.Trim(), password));
        if (secretBytes.Length > MaximumCredentialBlobBytes)
        {
            CryptographicOperations.ZeroMemory(secretBytes);
            throw new InvalidOperationException("The credential entry exceeds the Windows Credential Manager size limit.");
        }

        var blob = Marshal.AllocHGlobal(secretBytes.Length);
        try
        {
            Marshal.Copy(secretBytes, 0, blob, secretBytes.Length);
            var credential = new NativeCredential
            {
                Flags = 0,
                Type = GenericCredential,
                TargetName = target,
                Comment = "KKA Desk tax portal credential",
                LastWritten = default,
                CredentialBlobSize = (uint)secretBytes.Length,
                CredentialBlob = blob,
                Persist = PersistLocalMachine,
                AttributeCount = 0,
                Attributes = IntPtr.Zero,
                TargetAlias = null,
                UserName = "KKA Desk protected credential"
            };

            if (!CredWriteW(ref credential, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "Windows Credential Manager could not save this credential.");
        }
        finally
        {
            Marshal.Copy(new byte[secretBytes.Length], 0, blob, secretBytes.Length);
            Marshal.FreeHGlobal(blob);
            CryptographicOperations.ZeroMemory(secretBytes);
        }
    }

    public bool Exists(
        string familyAccountId,
        string profileId,
        string taxSystem,
        string? registrationId = null)
    {
        var target = BuildTarget(familyAccountId, profileId, taxSystem, registrationId);
        if (!CredReadW(target, GenericCredential, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound) return false;
            throw new Win32Exception(error, "Windows Credential Manager could not be checked.");
        }

        CredFree(pointer);
        return true;
    }

    public bool Remove(
        string familyAccountId,
        string profileId,
        string taxSystem,
        string? registrationId = null)
    {
        var target = BuildTarget(familyAccountId, profileId, taxSystem, registrationId);
        if (CredDeleteW(target, GenericCredential, 0)) return true;
        var error = Marshal.GetLastWin32Error();
        if (error == ErrorNotFound) return false;
        throw new Win32Exception(error, "Windows Credential Manager could not remove this credential.");
    }

    private static string BuildTarget(
        string familyAccountId,
        string profileId,
        string taxSystem,
        string? registrationId)
    {
        if (string.IsNullOrWhiteSpace(familyAccountId))
            throw new ArgumentException("A family/account ID is required.", nameof(familyAccountId));
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("A member profile ID is required.", nameof(profileId));
        if (string.IsNullOrWhiteSpace(taxSystem))
            throw new ArgumentException("A tax system is required.", nameof(taxSystem));
        if (taxSystem.Equals("GST", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(registrationId))
            throw new ArgumentException("A GSTIN is required to scope GST credentials to a registration.", nameof(registrationId));

        var key = string.Join("|",
            familyAccountId.Trim(),
            profileId.Trim(),
            taxSystem.Trim().ToUpperInvariant(),
            registrationId?.Trim().ToUpperInvariant() ?? string.Empty);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return "KKA.Desk.TaxCredential." + hash;
    }

    private sealed record CredentialSecret(string Login, string Password);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWriteW(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDeleteW(string target, uint type, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree", SetLastError = false)]
    private static extern void CredFree(IntPtr credential);
}
