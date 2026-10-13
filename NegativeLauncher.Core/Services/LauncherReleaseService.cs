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

    public static string[] GetCompatibleReleaseAssetNames(bool isLinux, Architecture architecture) =>
        GetCompatibleReleaseAssetNames(isLinux, OperatingSystem.IsMacOS(), architecture);

    public static string[] GetCompatibleReleaseAssetNames(
        bool isLinux,
        bool isMacOS,
        Architecture architecture)
    {
        if (architecture is not (Architecture.X64 or Architecture.Arm64))
            return Array.Empty<string>();

        if (isLinux)
            return new[] { "NegativeLauncher-linux-universal.tar.gz" };

        if (isMacOS)
            return new[] { "NegativeLauncher-macos-universal.zip" };

        return Array.Empty<string>();
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
