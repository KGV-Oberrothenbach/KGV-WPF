using System.Xml.Linq;
using KGV.ReleaseManager.Models;

namespace KGV.ReleaseManager.Services;

public sealed class CsprojVersionService
{
    public VersionInfo ReadWpfVersion(string csprojPath)
    {
        var document = XDocument.Load(csprojPath, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidOperationException("WPF-csproj konnte nicht gelesen werden.");

        var versionText = FindFirstValue(root, "Version")
            ?? FindFirstValue(root, "FileVersion")
            ?? FindFirstValue(root, "AssemblyVersion")
            ?? throw new InvalidOperationException("In der WPF-csproj wurde keine Version gefunden.");

        if (!VersionInfo.TryParse(versionText, out var info) || info is null)
        {
            throw new InvalidOperationException($"WPF-Version ist ungültig: {versionText}");
        }

        return info;
    }

    public string? TryReadAndroidApplicationId(string csprojPath)
    {
        try
        {
            var document = XDocument.Load(csprojPath, LoadOptions.PreserveWhitespace);
            var root = document.Root;
            if (root is null)
                return null;

            // MAUI single-project: <ApplicationId>de.kgv.oberrothenbach</ApplicationId>
            var id = FindFirstValue(root, "ApplicationId")
                     ?? FindFirstValue(root, "PackageName")
                     ?? FindFirstValue(root, "AndroidPackageName");

            return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
        }
        catch
        {
            return null;
        }
    }

    public (VersionInfo DisplayVersion, int BuildVersion) ReadAndroidVersion(string csprojPath)
    {
        var document = XDocument.Load(csprojPath, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidOperationException("MAUI-csproj konnte nicht gelesen werden.");

        var displayVersionText = FindFirstValue(root, "ApplicationDisplayVersion")
            ?? throw new InvalidOperationException("ApplicationDisplayVersion wurde in der MAUI-csproj nicht gefunden.");

        if (!VersionInfo.TryParse(displayVersionText, out var info) || info is null)
        {
            throw new InvalidOperationException($"Android-Version ist ungültig: {displayVersionText}");
        }

        var buildText = FindFirstValue(root, "ApplicationVersion")
            ?? throw new InvalidOperationException("ApplicationVersion wurde in der MAUI-csproj nicht gefunden.");

        if (!int.TryParse(buildText, out var buildVersion))
        {
            throw new InvalidOperationException($"Android-Buildnummer ist ungültig: {buildText}");
        }

        return (info, buildVersion);
    }

    public void UpdateWpfVersion(string csprojPath, VersionInfo version)
    {
        var document = XDocument.Load(csprojPath, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidOperationException("WPF-csproj konnte nicht geladen werden.");

        SetOrCreateValue(root, "Version", version.WpfProjectVersion);
        SetOrCreateValue(root, "FileVersion", version.WpfAssemblyVersion);
        SetOrCreateValue(root, "AssemblyVersion", version.WpfAssemblyVersion);

        document.Save(csprojPath);
    }

    public void UpdateAndroidVersion(string csprojPath, VersionInfo version, int buildVersion)
    {
        var document = XDocument.Load(csprojPath, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidOperationException("MAUI-csproj konnte nicht geladen werden.");

        SetOrCreateValue(root, "ApplicationDisplayVersion", version.DisplayVersion);
        SetOrCreateValue(root, "ApplicationVersion", buildVersion.ToString());

        document.Save(csprojPath);
    }

    private static string? FindFirstValue(XElement root, string localName)
    {
        return root.Descendants().FirstOrDefault(e => e.Name.LocalName == localName)?.Value?.Trim();
    }

    private static void SetOrCreateValue(XElement root, string localName, string value)
    {
        var element = root.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);
        if (element is not null)
        {
            element.Value = value;
            return;
        }

        var propertyGroup = root.Elements().FirstOrDefault(e => e.Name.LocalName == "PropertyGroup")
                            ?? throw new InvalidOperationException("Keine PropertyGroup in der csproj gefunden.");

        propertyGroup.Add(new XElement(localName, value));
    }
}
