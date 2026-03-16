namespace KGV.ReleaseManager.Models;

public sealed class ReleaseManagerSettings
{
    public GeneralSettings General { get; set; } = new();
    public AndroidSettings Android { get; set; } = new();
    public WindowsSettings Windows { get; set; } = new();
    public UiSettings Ui { get; set; } = new();

    public static ReleaseManagerSettings CreateDefaults() => new();
}

public sealed class GeneralSettings
{
    public string? RepoRoot { get; set; }
    public string? PublishRoot { get; set; }
    public string? GitHubRoot { get; set; }

    public string? BaseUrl { get; set; }
    public string? GitRemoteUrl { get; set; }
    public string? GitCredentialUsername { get; set; }
    public string? GitUserName { get; set; }
    public string? GitUserEmail { get; set; }

    public int KeepCount { get; set; } = 3;

    public string? MauiProjectPath { get; set; }
    public string? WpfProjectPath { get; set; }
}

public sealed class AndroidSettings
{
    public string? DefaultPackageName { get; set; }
    public string? DefaultPlayTrack { get; set; } = "internal";
    public string? DefaultPublishingStatus { get; set; } = "draft";
    public string? DefaultStoreUrl { get; set; }
    public string? ReleaseNameStrategy { get; set; } = "version-track";

    public string? KeystorePath { get; set; }
    public string? KeystoreAlias { get; set; }
    public string? StorePasswordFile { get; set; }
    public string? KeyPasswordFile { get; set; }
    public bool RequireSigning { get; set; } = true;

    public string? OutputRoot { get; set; }
}

public sealed class WindowsSettings
{
    public string? OutputRoot { get; set; }
}

public sealed class UiSettings
{
    public double WindowWidth { get; set; } = 1500;
    public double WindowHeight { get; set; } = 680;
}
