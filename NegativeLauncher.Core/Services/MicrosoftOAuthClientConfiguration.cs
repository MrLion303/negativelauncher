using System;
using System.Linq;
using System.Reflection;

namespace Negative_Client.Services
{
    public static class MicrosoftOAuthClientConfiguration
    {
        private const string BuildClientIdMetadataKey =
            "NegativeLauncher.MicrosoftOAuthClientId";

        public static string? ClientId { get; } =
            ReadBuildClientId();

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

        private static string? ReadBuildClientId()
        {
            string? configuredValue = typeof(MicrosoftOAuthClientConfiguration)
                .Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute =>
                    string.Equals(
                        attribute.Key,
                        BuildClientIdMetadataKey,
                        StringComparison.Ordinal))
                ?.Value;

            return TryNormalizeClientId(configuredValue, out string normalized)
                ? normalized
                : null;
        }
    }
}
