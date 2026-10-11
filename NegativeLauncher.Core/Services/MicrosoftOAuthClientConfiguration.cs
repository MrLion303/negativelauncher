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
            // Una variable válida tiene prioridad. Si existe, pero está mal formada,
            // no debe bloquear un Client ID válido guardado en las preferencias.
            if (TryNormalizeClientId(environmentValue, out string normalizedEnvironmentValue))
                return normalizedEnvironmentValue;

            return TryNormalizeClientId(savedValue, out string normalizedSavedValue)
                ? normalizedSavedValue
                : null;
        }
    }
}
