namespace KGV.Maui.Settings;

public enum AppMode
{
    User = 0,
    Admin = 1
}

public static class AppModes
{
    public const string User = "user";
    public const string Admin = "admin";

    public static AppMode? Parse(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            Admin => AppMode.Admin,
            User => AppMode.User,
            _ => null
        };
    }

    public static string ToStorageValue(AppMode mode)
    {
        return mode switch
        {
            AppMode.Admin => Admin,
            _ => User
        };
    }
}
