using System;
using System.IO;
using System.Text.Json;

namespace UpdateManager.Core.Versioning
{
    /// <summary>Версия из JSON-манифестов Android-билда. Разные версии требуют ручного выбора.</summary>
    public static class AndroidVersionDetector
    {
        public static string Detect(string buildFolder)
        {
            if (string.IsNullOrWhiteSpace(buildFolder) || !Directory.Exists(buildFolder))
                return null;

            string version = null;
            try
            {
                foreach (var file in Directory.EnumerateFiles(buildFolder, "*.json", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                    IgnoreInaccessible = false,
                    MatchCasing = MatchCasing.CaseInsensitive
                }))
                {
                    string candidate;
                    try
                    {
                        using var json = JsonDocument.Parse(File.ReadAllText(file));
                        if (json.RootElement.ValueKind != JsonValueKind.Object ||
                            !json.RootElement.TryGetProperty("versionName", out var name) ||
                            name.ValueKind != JsonValueKind.String)
                            continue;
                        candidate = name.GetString()?.Trim();
                    }
                    catch (JsonException) { continue; }
                    catch (IOException) { continue; }
                    catch (UnauthorizedAccessException) { continue; }

                    if (!VersionRules.IsValid(candidate))
                        continue;
                    if (version != null && !string.Equals(version, candidate, StringComparison.Ordinal))
                        return null;
                    version = candidate;
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            return version;
        }
    }
}
