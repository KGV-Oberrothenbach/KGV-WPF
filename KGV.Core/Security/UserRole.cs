using System.Collections.Generic;

namespace KGV.Core.Security
{
    public enum UserRole
    {
        User = 0,
        Vorstand = 1,
        Admin = 2
    }

    public static class UserRoles
    {
        public const string User = "user";
        public const string Vorstand = "vorstand";
        public const string Admin = "admin";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Admin,
            Vorstand,
            User
        };

        public static UserRole Parse(string? role)
        {
            return role?.Trim().ToLowerInvariant() switch
            {
                Admin => UserRole.Admin,
                Vorstand => UserRole.Vorstand,
                _ => UserRole.User
            };
        }

        public static string ToStorageValue(UserRole role)
        {
            return role switch
            {
                UserRole.Admin => Admin,
                UserRole.Vorstand => Vorstand,
                _ => User
            };
        }
    }
}
