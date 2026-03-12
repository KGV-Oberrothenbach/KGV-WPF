namespace KGV.ReleaseManager.Models;

public sealed class VersionInfo
{
    public VersionInfo(int major, int minor, int patch, int? build = null)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Build = build;
    }

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public int? Build { get; }

    public string DisplayVersion => $"{Major}.{Minor}.{Patch}";

    public string WpfProjectVersion => $"{Major}.{Minor}.{Patch}";

    public string WpfAssemblyVersion => $"{Major}.{Minor}.{Patch}.0";

    public int AndroidBuildVersion => Build ?? (Patch + 1);

    public VersionInfo IncrementPatch(int? nextBuild = null) => new(Major, Minor, Patch + 1, nextBuild);

    public static bool TryParse(string? value, out VersionInfo? versionInfo)
    {
        versionInfo = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor) || !int.TryParse(parts[2], out var patch))
        {
            return false;
        }

        int? build = null;
        if (parts.Length >= 4 && int.TryParse(parts[3], out var parsedBuild))
        {
            build = parsedBuild;
        }

        versionInfo = new VersionInfo(major, minor, patch, build);
        return true;
    }
}
