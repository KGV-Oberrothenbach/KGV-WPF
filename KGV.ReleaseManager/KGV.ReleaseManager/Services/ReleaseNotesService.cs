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

    public bool ReleaseEntryExists(string repoRoot, string version)
    {
        try
        {
            var path = GetReleasesJsonPath(repoRoot);
            if (!File.Exists(path))
                return false;

            var json = File.ReadAllText(path, Encoding.UTF8);
            var list = JsonSerializer.Deserialize<List<ReleaseNotesEntry>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

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
        if (string.IsNullOrWhiteSpace(changelogText))
            return string.Empty;

        const string marker = "## [Unreleased]";
        var start = changelogText.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return string.Empty;

        var next = changelogText.IndexOf("\n## [", start + marker.Length, StringComparison.Ordinal);
        if (next < 0)
            return changelogText[start..].Trim();

        return changelogText[start..next].Trim();
    }

    public string? TryReadLatestReleaseNotesSummary(string repoRoot)
    {
        try
        {
            var jsonPath = GetReleasesJsonPath(repoRoot);
            if (!File.Exists(jsonPath))
                return null;

            var text = File.ReadAllText(jsonPath, Encoding.UTF8);
            var entries = JsonSerializer.Deserialize<List<ReleaseNotesEntry>>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

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
        UpsertReleasesJson(repoRoot, entry);
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

    private void UpsertReleasesJson(string repoRoot, ReleaseNotesEntry entry)
    {
        var path = GetReleasesJsonPath(repoRoot);
        List<ReleaseNotesEntry> list;

        if (File.Exists(path))
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            try
            {
                list = JsonSerializer.Deserialize<List<ReleaseNotesEntry>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<ReleaseNotesEntry>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("releases.json ist ungültig und konnte nicht gelesen werden.", ex);
            }
        }
        else
        {
            list = new List<ReleaseNotesEntry>();
        }

        var index = list.FindIndex(x => string.Equals(x.Version, entry.Version, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            list[index] = entry;
        else
            list.Add(entry);

        // Sort newest first (string-based ISO date). Keep stable and easy for client-side HTML.
        list = list
            .OrderByDescending(x => x.ReleaseDate, StringComparer.Ordinal)
            .ThenByDescending(x => x.Version, StringComparer.Ordinal)
            .ToList();

        var outJson = JsonSerializer.Serialize(list, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(path, outJson + "\n", Encoding.UTF8);
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
