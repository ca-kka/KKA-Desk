# Portal sync adapter and testing boundaries

KKA Desk now has a read-only adapter for the existing KKA Supabase project. It queries the portal's existing `profiles` and `client_account_members` relations, including the nested client profile fields used by the family-management UI. Requests use a user access token, the publishable key, HTTPS, and the portal's existing RLS policies. No service-role key is used.

## Important limitation

This adapter does not implement login, email OTP, TOTP/MFA challenge, session refresh, or sign-out. It deliberately requires a token from a separately reviewed sign-in flow and an explicit confirmation that the portal's verification gate has completed. Do not wire it to password-only login. The desktop callback/redirect and the existing portal OTP policy must be integrated and tested before live sync is enabled in the UI. Expired sessions and authorization failures must fail closed.

## Family model

- `account_id` is the family/account grouping key.
- `client_id` is the individual member profile key.
- Each active association remains a separate profile row; account ID is never substituted for profile ID.
- Tax settings and documents must be scoped to the selected member profile and later to each GST registration.
- The adapter is read-only and does not alter the portal or production database.

## CI safeguards

The Windows workflow treats compiler warnings as errors, parses XAML/project XML, rejects merge-conflict markers, and runs CodeQL C# security analysis. This reduces syntax and static-analysis regressions but cannot guarantee zero runtime defects. Sentry runtime monitoring is not connected yet because no Sentry project DSN/account has been configured for KKA Desk.
