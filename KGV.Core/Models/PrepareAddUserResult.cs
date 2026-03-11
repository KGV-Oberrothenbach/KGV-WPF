using System;
using KGV.Core.Security;

namespace KGV.Core.Models
{
    public enum PrepareAddUserOutcome
    {
        Ready = 0,
        NotFound = 1,
        MissingEmail = 2,
        UserAlreadyExists = 3,
        InvalidRole = 4,
        Error = 5
    }

    public sealed record PrepareAddUserResult(
        PrepareAddUserOutcome Outcome,
        string Message,
        int MitgliedId,
        string Email,
        string Role)
    {
        public static PrepareAddUserResult Ready(int mitgliedId, string email, string role) =>
            new(PrepareAddUserOutcome.Ready, "OK", mitgliedId, email ?? string.Empty, (role ?? UserRoles.User).Trim().ToLowerInvariant());

        public static PrepareAddUserResult Error(string message, int mitgliedId = 0) =>
            new(PrepareAddUserOutcome.Error, message ?? "Fehler", mitgliedId, string.Empty, string.Empty);
    }
}
