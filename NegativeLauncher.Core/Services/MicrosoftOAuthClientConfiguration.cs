using System;

namespace Negative_Client.Services
{
    public static class MicrosoftOAuthClientConfiguration
    {
        public const string EnvironmentVariableName = "NEGATIVE_LAUNCHER_MSAL_CLIENT_ID";

        public static bool TryNormalizeClientId(string? value, out string normalized)
        {
            if (Guid.TryParse(value?.Trim(), out Guid parsed) && parsed != Guid.Empty)
            {
                normalized = parsed.ToString("D");
                return true;
            }

            normalized = string.Empty;
            return false;
        }

        public static string? ResolveClientId(string? environmentValue, string? savedValue)
        {
            string? candidate = string.IsNullOrWhiteSpace(environmentValue)
                ? savedValue
                : environmentValue;

            return TryNormalizeClientId(candidate, out string normalized)
                ? normalized
                : null;
        }
    }
}
