# KKA Desk

## Download KKA Desk for Windows

**[Download the latest Windows x64 build from GitHub Actions](https://github.com/ca-kka/KKA-Desk/actions?query=branch%3Amain)** · [View the source and test history](https://github.com/ca-kka/KKA-Desk/actions)

This is a self-contained Windows preview. Open the newest successful **Build, test and package KKA Desk for Windows** run, then download `KKA-Desk-Windows-x64` from its Artifacts section and extract the ZIP. You do not need to download source code or install the .NET runtime. Because this repository is private, you must be signed in to GitHub with access to KKA-Desk.

Private Windows-first desktop workspace for Kamal Kuldeep & Associates (KKA).

## Current milestone

The repository contains the initial WPF solution and KKA-branded workspace shell, plus architecture, phased delivery, and security guardrail documents. The shell is still a **non-production foundation preview**. A portal-sync window supports the existing KKA sign-in/OTP gate and loads active family/profile associations for administrators. Selected profiles can be passed into the tax preview, where profile-specific Income Tax and GST credentials can be saved to Windows Credential Manager; government portal downloads and document punching remain disabled.

## Planned first release

- Dashboard with automatically synchronized important due dates from ca-kka.com
- Portal-compatible sign-in/OTP and administrator-only profile synchronization (initial integration; requires live acceptance testing)
- Documents, queries, directory, queue, and admin modules
- PDF viewing and drag-and-drop
- Windows installer with update notifications and user-confirmed installation

## Security boundaries

- Staff may access only assigned clients; admins may access all clients subject to policy.
- Client data requires an online connection.
- Never ship Supabase service-role credentials in the desktop application.
- Audit RLS, Edge Functions, role/active checks, assignment checks, and MFA/AAL requirements before connecting sensitive modules.
- Do not modify production portal or Supabase resources without explicit approval.

## Build

Open Kka.Desk.sln in Visual Studio on Windows with the .NET 10 SDK and Windows Desktop development components, or run:

```powershell
dotnet restore Kka.Desk.sln
dotnet build Kka.Desk.sln --configuration Release
```

GitHub Actions builds on a Windows runner with warnings-as-errors, XAML/XML parsing, merge-conflict checks, Windows Credential Manager isolation/cleanup tests using dummy credentials, mocked OTP and staff-role authorization tests, an executable startup smoke test, and CodeQL C# analysis. Successful main-branch builds publish the tested EXE and checksum as a GitHub Release.

## Not production-ready yet

The application compiles in GitHub's Windows CI and validates XAML/XML. The portal-sync path has not yet passed live credential/OTP acceptance testing on KKA's actual account, and credential-vault save/remove operations still need an authorized Windows pilot. Due-date synchronization, document handling, tax automation execution, and the remaining modules are not yet connected. Do not use it with production client data until security review and acceptance testing are complete.


## Download a ready-to-run Windows preview

Every push to `main` and manual workflow run attempts to create a self-contained Windows x64 EXE package. You do not need to install the .NET runtime to run that package.

1. Open [KKA Desk Actions](https://github.com/ca-kka/KKA-Desk/actions?query=branch%3Amain) while signed in to an account with access to this private repository.
2. Open the newest successful **Build, test and package KKA Desk for Windows** run.
3. Download `KKA-Desk-Windows-x64` from Artifacts, extract it, and run `Kka.Desk.exe`.
4. Optionally verify the EXE against `SHA256SUMS.txt` in the ZIP before running it.

GitHub Actions artifacts are retained for 14 days. A permanent GitHub Release could not be created by the current Actions token because GitHub returned a 403 permission error; the workflow does not attempt to bypass that restriction. Use the Actions link above for tested builds.

The workflow performs a warnings-as-errors build, XML/XAML validation, conflict-marker checks, dummy-only Windows Credential Manager save/isolation/removal tests, mocked OTP and role-gate tests, a self-contained single-file publish, an 8-second process-startup smoke test, and CodeQL analysis. Release publication waits for both the Windows test job and CodeQL job to pass. These checks do not replace live acceptance testing of portal OTP, Windows Credential Manager behavior, profile authorization, or tax portal downloads.
