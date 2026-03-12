using System.IO;

namespace KGV.ReleaseManager.Services;

public sealed class InnoSetupService
{
    private readonly ProcessRunner _processRunner = new();

    public string LocateIsccExe()
    {
        // Prefer PATH if available.
        var fromPath = TryLocateFromPath();
        if (fromPath is not null)
        {
            return fromPath;
        }

        // Common install paths.
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var candidates = new[]
        {
            Path.Combine(localAppData, "Programs", "Inno Setup 6", "ISCC.exe"),
            Path.Combine(programFilesX86, "Inno Setup 6", "ISCC.exe"),
            Path.Combine(programFiles, "Inno Setup 6", "ISCC.exe"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("ISCC.exe nicht gefunden. Bitte Inno Setup 6 installieren.");
    }

    public async Task BuildInstallerAsync(
        string isccExe,
        string innoScriptPath,
        string publishDir,
        string installerOutDir,
        string workingDirectory,
        Action<string> log,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(installerOutDir);

        var args = $"/DPublishDir=\"{publishDir}\" /O\"{installerOutDir}\" \"{innoScriptPath}\"";
        var exit = await _processRunner.RunAsync(isccExe, args, workingDirectory, log, cancellationToken);
        if (exit != 0)
        {
            throw new InvalidOperationException("Inno Setup fehlgeschlagen.");
        }
    }

    private static string? TryLocateFromPath()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(dir, "ISCC.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // ignore invalid path entries
            }
        }

        return null;
    }
}
