using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using KGV.ReleaseManager.Models;

namespace KGV.ReleaseManager.Services;

public sealed class ReleaseNotesService
{
    private const string DocumentationFolderName = "Documentation";
    private const string ChangelogFileName = "CHANGELOG.md";
    private const string ReleaseNotesHistoryFileName = "RELEASE_NOTES_HISTORY.md";
    private const string ReleasesJsonFileName = "releases.json";

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true
    };

    public string GetDocumentationRoot(string repoRoot)
        => Path.Combine(repoRoot, DocumentationFolderName);

    public string GetChangelogPath(string repoRoot)
        => Path.Combine(GetDocumentationRoot(repoRoot), ChangelogFileName);

    public string GetReleaseNotesHistoryPath(string repoRoot)
        => Path.Combine(GetDocumentationRoot(repoRoot), ReleaseNotesHistoryFileName);

    public string GetReleasesJsonPath(string repoRoot)
        => Path.Combine(GetDocumentationRoot(repoRoot), ReleasesJsonFileName);

    public string BuildClipboardPrompt(string version, DateTime releaseDate, string changelogContent, string? context)
    {
        var dateText = releaseDate.ToString("yyyy-MM-dd");

        var sb = new StringBuilder();
        sb.AppendLine("KGV RELEASE INPUT");
        sb.AppendLine($"Version: {version}");
        sb.AppendLine($"Datum: {dateText}");
        sb.AppendLine("Quelle: Documentation/CHANGELOG.md");
        sb.AppendLine("Modus: Release Notes aus Changelog formulieren");
        sb.AppendLine();
        sb.AppendLine("AUFGABE AN CHATGPT");
        sb.AppendLine("Erstelle aus den unten stehenden KGV-Änderungen einen einheitlichen, verständlichen Release-Text für Endnutzer.");
        sb.AppendLine("Wichtig:");
        sb.AppendLine("- auf Deutsch");
        sb.AppendLine("- sachlich, klar, knapp");
        sb.AppendLine("- keine Entwickler-Interna");
        sb.AppendLine("- keine Dateinamen");
        sb.AppendLine("- keine Klassen-/Methodennamen");
        sb.AppendLine("- gleiche oder ähnliche Punkte zusammenfassen");
        sb.AppendLine("- in verständliche Anwendersprache umformulieren");
        sb.AppendLine("- wenn sinnvoll in die Bereiche „Neu“, „Verbessert“, „Behoben“ gliedern");
        sb.AppendLine("- nur Aussagen verwenden, die durch den unten stehenden Inhalt gedeckt sind");
        sb.AppendLine("- nichts erfinden");
        sb.AppendLine();
        sb.AppendLine("AUSGABEFORMAT");
        sb.AppendLine("Titel: <kurzer Release-Titel>");
        sb.AppendLine("Kurztext: <1-3 Sätze>");
        sb.AppendLine("Details:");
        sb.AppendLine("Neu:");
        sb.AppendLine("- ...");
        sb.AppendLine("Verbessert:");
        sb.AppendLine("- ...");
        sb.AppendLine("Behoben:");
        sb.AppendLine("- ...");
        sb.AppendLine();
        sb.AppendLine("CHANGLEOG-INHALT");
        sb.AppendLine("<<<BEGIN_CHANGELOG>>>");
        sb.AppendLine((changelogContent ?? string.Empty).Trim());
        sb.AppendLine("<<<END_CHANGELOG>>>");
        sb.AppendLine();
        sb.AppendLine("OPTIONALE ZUSATZHINWEISE");
        sb.AppendLine("<<<BEGIN_CONTEXT>>>");
        sb.AppendLine((context ?? string.Empty).Trim());
        sb.AppendLine("<<<END_CONTEXT>>>");

        return sb.ToString();
    }

    public AndroidPlatformReleaseData? TryReadLatestAndroidPlatformDraft(string repoRoot)
    {
        try
        {
            var jsonPath = GetReleasesJsonPath(repoRoot);
            if (!File.Exists(jsonPath))
                return null;

            var text = File.ReadAllText(jsonPath, Encoding.UTF8);
            var entries = TryDeserializeMasterReleases(text)
                          ?? TryDeserializeLegacyReleases(text)?.Select(MapLegacyRelease).ToList();

            if (entries == null || entries.Count == 0)
                return null;

            var ordered = entries
                .Where(e => e is not null && !string.IsNullOrWhiteSpace(e.Version))
                .OrderByDescending(e => e.ReleaseDate, StringComparer.Ordinal)
                .ThenByDescending(e => e.Version, StringComparer.Ordinal)
                .ToList();

            foreach (var entry in ordered)
            {
                var android = entry.Platforms?.FirstOrDefault(p => string.Equals(p.Platform, "android", StringComparison.OrdinalIgnoreCase));
                var data = android?.Android;
                if (data == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(data.PackageName)
                    || !string.IsNullOrWhiteSpace(data.ReleaseName)
                    || !string.IsNullOrWhiteSpace(data.PlayTrack))
                {
                    return data;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public bool ReleaseEntryExists(string repoRoot, string version)
    {
        try
        {
            var path = GetReleasesJsonPath(repoRoot);
            if (!File.Exists(path))
                return false;

            var json = File.ReadAllText(path, Encoding.UTF8);

            var list = TryDeserializeMasterReleases(json)
                       ?? TryDeserializeLegacyReleases(json)?.Select(MapLegacyRelease).ToList();

            return list?.Any(x => string.Equals(x.Version, version, StringComparison.OrdinalIgnoreCase)) == true;
        }
        catch
        {
            return false;
        }
    }

    public string ReadChangelogOrEmpty(string repoRoot)
    {
        var path = GetChangelogPath(repoRoot);
        return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
    }

    public string ExtractUnreleasedBlock(string changelogText)
    {
        return ExtractChangelogBlock(changelogText, "## [Unreleased]");
    }

    public string ExtractChangelogBlock(string changelogText, string header)
    {
        if (string.IsNullOrWhiteSpace(changelogText) || string.IsNullOrWhiteSpace(header))
            return string.Empty;

        var start = changelogText.IndexOf(header, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return string.Empty;

        var next = changelogText.IndexOf("\n## [", start + header.Length, StringComparison.Ordinal);
        if (next < 0)
            return changelogText[start..].Trim();

        return changelogText[start..next].Trim();
    }

    public string EnsureChangelogSkeleton(string changelogText)
    {
        if (!string.IsNullOrWhiteSpace(changelogText)
            && changelogText.IndexOf("## [Unreleased]", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return changelogText;
        }

        var sb = new StringBuilder();
        sb.AppendLine("# Changelog");
        sb.AppendLine();
        sb.AppendLine("## [Unreleased]");
        sb.AppendLine();
        sb.AppendLine("### Hinzugefügt");
        sb.AppendLine("- (keine)");
        sb.AppendLine();
        sb.AppendLine("### Geändert");
        sb.AppendLine("- (keine)");
        sb.AppendLine();
        sb.AppendLine("### Behoben");
        sb.AppendLine("- (keine)");
        sb.AppendLine();
        sb.AppendLine("### Entfernt");
        sb.AppendLine("- (keine)");

        return sb.ToString().TrimEnd() + "\n";
    }

    public string UpsertChangelogBlock(string changelogText, string header, string newBlock)
    {
        changelogText = EnsureChangelogSkeleton(changelogText);

        header = (header ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(header))
            header = "## [Unreleased]";

        newBlock = (newBlock ?? string.Empty).Trim();
        if (!newBlock.StartsWith(header, StringComparison.OrdinalIgnoreCase))
            newBlock = header + "\n\n" + newBlock;

        newBlock = newBlock.TrimEnd() + "\n";

        var idx = changelogText.IndexOf(header, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            // Insert after main title if possible, else prepend.
            const string title = "# Changelog";
            var titleIdx = changelogText.IndexOf(title, StringComparison.OrdinalIgnoreCase);
            if (titleIdx >= 0)
            {
                var afterTitle = changelogText.IndexOf('\n', titleIdx + title.Length);
                if (afterTitle >= 0)
                {
                    return changelogText[..(afterTitle + 1)].TrimEnd()
                           + "\n\n"
                           + newBlock.TrimEnd()
                           + "\n\n"
                           + changelogText[(afterTitle + 1)..].TrimStart();
                }
            }

            return newBlock + "\n" + changelogText.TrimStart();
        }

        var next = changelogText.IndexOf("\n## [", idx + header.Length, StringComparison.Ordinal);
        if (next < 0)
            return changelogText[..idx] + newBlock;

        return changelogText[..idx] + newBlock + changelogText[next..];
    }

    public void WriteChangelog(string repoRoot, string changelogText)
    {
        if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
            throw new InvalidOperationException("RepoRoot ist ungültig.");

        var path = GetChangelogPath(repoRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, (changelogText ?? string.Empty).TrimEnd() + "\n", Encoding.UTF8);
    }

    public string? TryReadLatestReleaseNotesSummary(string repoRoot)
    {
        try
        {
            var jsonPath = GetReleasesJsonPath(repoRoot);
            if (!File.Exists(jsonPath))
                return null;

            var text = File.ReadAllText(jsonPath, Encoding.UTF8);

            var entries = TryDeserializeMasterReleases(text)
                          ?? TryDeserializeLegacyReleases(text)?.Select(MapLegacyRelease).ToList();

            var last = entries?
                .Where(e => e is not null && !string.IsNullOrWhiteSpace(e.Version))
                .OrderByDescending(e => e.ReleaseDate, StringComparer.Ordinal)
                .FirstOrDefault();

            if (last == null)
                return null;

            return $"Letzte bekannte Version: {last.Version} ({last.ReleaseDate})\nTitel: {last.Title}\nKurztext: {last.ShortText}";
        }
        catch
        {
            return null;
        }
    }

    private static List<MasterReleaseEntry>? TryDeserializeMasterReleases(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<MasterReleaseEntry>>(json, JsonReadOptions);
        }
        catch
        {
            return null;
        }
    }

    private static List<ReleaseNotesEntry>? TryDeserializeLegacyReleases(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<ReleaseNotesEntry>>(json, JsonReadOptions);
        }
        catch
        {
            return null;
        }
    }

    private static MasterReleaseEntry MapLegacyRelease(ReleaseNotesEntry legacy)
    {
        // Legacy-Format kann keine Plattformdaten enthalten. Default: Windows aktiviert, Android deaktiviert.
        return new MasterReleaseEntry(
            Version: legacy.Version,
            ReleaseDate: legacy.ReleaseDate,
            Title: legacy.Title,
            ShortText: legacy.ShortText,
            FullText: legacy.FullText,
            Categories: legacy.Categories,
            Status: "veröffentlicht",
            Platforms: new[]
            {
                PlatformReleaseDefaults.CreateWindows(enabled: true, status: "veröffentlicht"),
                PlatformReleaseDefaults.CreateAndroidPlayStore(enabled: false, status: "deaktiviert")
            });
    }

    public void OpenChangelogInEditor(string repoRoot)
    {
        var path = GetChangelogPath(repoRoot);
        if (!File.Exists(path))
            throw new FileNotFoundException("CHANGELOG.md nicht gefunden.", path);

        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true
        });
    }

    public (ReleaseNotesEntry Entry, string[] Categories) ParseReleaseNotesText(string version, DateTime releaseDate, string fullText)
    {
        fullText = (fullText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fullText))
            throw new InvalidOperationException("Release-Text ist leer.");

        var title = TryExtractLineValue(fullText, "Titel:");
        var shortText = TryExtractLineValue(fullText, "Kurztext:");

        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Im Release-Text fehlt 'Titel:'.");

        if (string.IsNullOrWhiteSpace(shortText))
            throw new InvalidOperationException("Im Release-Text fehlt 'Kurztext:'.");

        var categories = ExtractCategories(fullText);

        var entry = new ReleaseNotesEntry(
            Version: version,
            ReleaseDate: releaseDate.ToString("yyyy-MM-dd"),
            Title: title,
            ShortText: shortText,
            FullText: fullText,
            Categories: categories);

        return (entry, categories);
    }

    public void SaveReleaseNotes(string repoRoot, ReleaseNotesEntry entry)
    {
        if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
            throw new InvalidOperationException("RepoRoot ist ungültig.");

        var docRoot = GetDocumentationRoot(repoRoot);
        Directory.CreateDirectory(docRoot);

        UpdateReleaseNotesHistory(repoRoot, entry);
        // Legacy-Aufruf: Plattforminfos sind unbekannt. Default: Windows aktiviert, Android deaktiviert.
        var master = new MasterReleaseEntry(
            Version: entry.Version,
            ReleaseDate: entry.ReleaseDate,
            Title: entry.Title,
            ShortText: entry.ShortText,
            FullText: entry.FullText,
            Categories: entry.Categories,
            Status: "Entwurf",
            Platforms: new[]
            {
                PlatformReleaseDefaults.CreateWindows(enabled: true, status: "Entwurf"),
                PlatformReleaseDefaults.CreateAndroidPlayStore(enabled: false, status: "deaktiviert"),
            });

        UpsertReleasesJson(repoRoot, master);
    }

    public void SaveReleaseNotes(string repoRoot, ReleaseNotesEntry entry, PlatformReleaseEntry[] platforms, string masterStatus)
    {
        if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
            throw new InvalidOperationException("RepoRoot ist ungültig.");

        var docRoot = GetDocumentationRoot(repoRoot);
        Directory.CreateDirectory(docRoot);

        UpdateReleaseNotesHistory(repoRoot, entry);

        var master = new MasterReleaseEntry(
            Version: entry.Version,
            ReleaseDate: entry.ReleaseDate,
            Title: entry.Title,
            ShortText: entry.ShortText,
            FullText: entry.FullText,
            Categories: entry.Categories,
            Status: string.IsNullOrWhiteSpace(masterStatus) ? "Entwurf" : masterStatus.Trim(),
            Platforms: platforms ?? Array.Empty<PlatformReleaseEntry>());

        UpsertReleasesJson(repoRoot, master);
    }

    private void UpdateReleaseNotesHistory(string repoRoot, ReleaseNotesEntry entry)
    {
        var path = GetReleaseNotesHistoryPath(repoRoot);
        var existing = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "# Release Notes Historie\n";

        if (!existing.Contains("# Release Notes Historie", StringComparison.OrdinalIgnoreCase))
            existing = "# Release Notes Historie\n\n" + existing.Trim() + "\n";

        var header = $"## Version {entry.Version} - {entry.ReleaseDate}";
        var newBlock = header + "\n" + entry.FullText.Trim() + "\n\n";

        var updated = UpsertMarkdownSection(existing, header, newBlock);
        File.WriteAllText(path, updated, Encoding.UTF8);
    }

    private void UpsertReleasesJson(string repoRoot, MasterReleaseEntry entry)
    {
        var path = GetReleasesJsonPath(repoRoot);
        List<MasterReleaseEntry> list;

        if (File.Exists(path))
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            try
            {
                list = TryDeserializeMasterReleases(json)
                       ?? TryDeserializeLegacyReleases(json)?.Select(MapLegacyRelease).ToList()
                       ?? new List<MasterReleaseEntry>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("releases.json ist ungültig und konnte nicht gelesen werden.", ex);
            }
        }
        else
        {
            list = new List<MasterReleaseEntry>();
        }

        var index = list.FindIndex(x => string.Equals(x.Version, entry.Version, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            var existing = list[index];

            // Plattformdaten möglichst erhalten, wenn der neue Eintrag keine/zu wenige Plattformen mitbringt.
            var platforms = (entry.Platforms?.Length ?? 0) > 0 ? entry.Platforms : existing.Platforms;

            list[index] = entry with { Platforms = platforms };
        }
        else
            list.Add(entry);

        // Sort newest first (string-based ISO date). Keep stable and easy for client-side HTML.
        list = list
            .OrderByDescending(x => x.ReleaseDate, StringComparer.Ordinal)
            .ThenByDescending(x => x.Version, StringComparer.Ordinal)
            .ToList();

        var outJson = JsonSerializer.Serialize(list, JsonWriteOptions);

        File.WriteAllText(path, outJson + "\n", Encoding.UTF8);
    }

    public void UpdatePlatformRelease(string repoRoot, string version, PlatformReleaseEntry platformRelease)
    {
        if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
            throw new InvalidOperationException("RepoRoot ist ungültig.");

        if (string.IsNullOrWhiteSpace(version))
            throw new InvalidOperationException("Version fehlt.");

        var path = GetReleasesJsonPath(repoRoot);
        if (!File.Exists(path))
            return;

        var json = File.ReadAllText(path, Encoding.UTF8);
        var list = TryDeserializeMasterReleases(json)
                   ?? TryDeserializeLegacyReleases(json)?.Select(MapLegacyRelease).ToList();

        if (list == null)
            return;

        var idx = list.FindIndex(x => string.Equals(x.Version, version, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
            return;

        var existing = list[idx];
        var updatedPlatforms = (existing.Platforms ?? Array.Empty<PlatformReleaseEntry>()).ToList();
        var pIdx = updatedPlatforms.FindIndex(p => string.Equals(p.Platform, platformRelease.Platform, StringComparison.OrdinalIgnoreCase));
        if (pIdx >= 0)
            updatedPlatforms[pIdx] = platformRelease;
        else
            updatedPlatforms.Add(platformRelease);

        list[idx] = existing with { Platforms = updatedPlatforms.ToArray() };

        File.WriteAllText(path, JsonSerializer.Serialize(list, JsonWriteOptions) + "\n", Encoding.UTF8);
    }

    private static string UpsertMarkdownSection(string markdown, string sectionHeader, string newSection)
    {
        var idx = markdown.IndexOf(sectionHeader, StringComparison.Ordinal);
        if (idx < 0)
        {
            var trimmed = markdown.TrimEnd();
            return trimmed + "\n\n" + newSection.TrimEnd() + "\n";
        }

        var next = markdown.IndexOf("\n## Version ", idx + sectionHeader.Length, StringComparison.Ordinal);
        if (next < 0)
        {
            return markdown[..idx] + newSection;
        }

        return markdown[..idx] + newSection + markdown[next..];
    }

    private static string? TryExtractLineValue(string text, string prefix)
    {
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return trimmed[prefix.Length..].Trim();
        }

        return null;
    }

    private static string[] ExtractCategories(string text)
    {
        var found = new List<string>();

        // ChatGPT-Ausgabeformat nutzt diese Sektionen als Marker.
        AddIfPresent(found, text, "Neu:");
        AddIfPresent(found, text, "Verbessert:");
        AddIfPresent(found, text, "Behoben:");

        return found.ToArray();
    }

    private static void AddIfPresent(List<string> list, string text, string marker)
    {
        if (text.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var normalized = marker.TrimEnd(':');
            if (!list.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                list.Add(normalized);
        }
    }
}
