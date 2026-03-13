namespace KGV.ReleaseManager.Models;

public sealed record ReleaseNotesEntry(
    string Version,
    string ReleaseDate,
    string Title,
    string ShortText,
    string FullText,
    string[]? Categories);
