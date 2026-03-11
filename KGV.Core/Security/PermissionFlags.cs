using System;

namespace KGV.Core.Security
{
    [Flags]
    public enum PermissionFlags
    {
        None = 0,

        CanSearchMembers = 1 << 0,
        CanViewMembers = 1 << 1,
        CanEditAllMembers = 1 << 2,

        CanSeeOwnDataOnly = 1 << 3,

        CanManageDocuments = 1 << 4,
        CanManageReadings = 1 << 5,
        CanManageWorkHours = 1 << 6,

        CanManageRoles = 1 << 7,

        // Admin-/Vorstands-Aktionen rund um Nutzerkonten (Invite, Rollenpflege, Deaktivierung, ...)
        CanManageUsers = 1 << 8
    }
}
