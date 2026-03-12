using System.Diagnostics;

namespace KGV.ReleaseManager.Services;

public sealed class ProcessRunner
{
    public sealed record Result(int ExitCode, IReadOnlyList<string> Output);

    public async Task<int> RunAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        Action<string> log,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var exitSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                log(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                log("ERR: " + e.Data);
            }
        };

        process.Exited += (_, _) => exitSource.TrySetResult(process.ExitCode);

        if (!process.Start())
        {
            throw new InvalidOperationException($"Prozess konnte nicht gestartet werden: {fileName}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignorieren
            }
        });

        return await exitSource.Task.ConfigureAwait(false);
    }

    public async Task<Result> RunCaptureAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        Action<string> log,
        CancellationToken cancellationToken = default)
    {
        var output = new List<string>();

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var exitSource = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                output.Add(e.Data);
                log(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                var line = "ERR: " + e.Data;
                output.Add(line);
                log(line);
            }
        };

        process.Exited += (_, _) => exitSource.TrySetResult(process.ExitCode);

        if (!process.Start())
        {
            throw new InvalidOperationException($"Prozess konnte nicht gestartet werden: {fileName}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignorieren
            }
        });

        var exitCode = await exitSource.Task.ConfigureAwait(false);
        return new Result(exitCode, output);
    }
}
