using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Negative_Client.Services;

/// <summary>
/// Centraliza la selección de paquetes de actualización compatibles con la plataforma.
/// </summary>
public static class LauncherReleaseService
{
    private static readonly Regex VersionPattern = new(@"\d+(?:\.\d+){1,3}", RegexOptions.Compiled);

    public static string[] GetCompatibleReleaseAssetNames(bool isLinux, Architecture architecture)
    {
        if (!isLinux)
            return Array.Empty<string>();

        string architectureName = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => string.Empty
        };

        return architectureName.Length > 0
            ? new[] { $"NegativeLauncher-linux-{architectureName}.tar.gz" }
            : Array.Empty<string>();
    }

    public static Version? ExtractVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        Match match = VersionPattern.Match(value);
        return match.Success && Version.TryParse(match.Value, out Version? version)
            ? version
            : null;
    }
}
