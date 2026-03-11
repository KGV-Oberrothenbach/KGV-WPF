using System;

namespace KGV.Core.Security
{
    public interface IPermissionService
    {
        PermissionFlags GetPermissions(UserRole role);
        UserContext CreateContext(Guid userId, string? role, long? mitgliedId);
    }

    public sealed class PermissionService : IPermissionService
    {
        public PermissionFlags GetPermissions(UserRole role)
        {
            return role switch
            {
                UserRole.Admin =>
                    PermissionFlags.CanSearchMembers |
                    PermissionFlags.CanViewMembers |
                    PermissionFlags.CanEditAllMembers |
                    PermissionFlags.CanManageDocuments |
                    PermissionFlags.CanManageReadings |
                    PermissionFlags.CanManageWorkHours |
                    PermissionFlags.CanManageRoles |
                    PermissionFlags.CanManageUsers,

                UserRole.Vorstand =>
                    PermissionFlags.CanSearchMembers |
                    PermissionFlags.CanViewMembers |
                    PermissionFlags.CanEditAllMembers |
                    PermissionFlags.CanManageDocuments |
                    PermissionFlags.CanManageReadings |
                    PermissionFlags.CanManageWorkHours |
                    PermissionFlags.CanManageUsers,

                _ =>
                    PermissionFlags.CanViewMembers |
                    PermissionFlags.CanSeeOwnDataOnly |
                    PermissionFlags.CanManageDocuments |
                    PermissionFlags.CanManageReadings
            };
        }

        public UserContext CreateContext(Guid userId, string? role, long? mitgliedId)
        {
            var parsedRole = UserRoles.Parse(role);
            var permissions = GetPermissions(parsedRole);
            return new UserContext(userId, parsedRole, mitgliedId, permissions);
        }
    }
}
