using System;

namespace KGV.Core.Models
{
    public enum InviteUserAccountOutcome
    {
        Invited = 0,
        AlreadyLinked = 1,
        MissingEmail = 2,
        NotFound = 3,
        Unauthorized = 4,
        UserAlreadyExists = 5,
        InvalidRole = 6,
        Error = 99
    }

    public sealed record InviteUserAccountResult(
        InviteUserAccountOutcome Outcome,
        string Message,
        Guid? AuthUserId = null,
        int? MitgliedId = null,
        string? Email = null)
    {
        public bool Success => Outcome == InviteUserAccountOutcome.Invited;

        // Für UI/Logging: stabiler Code ohne Text-Parsing
        public string ErrorCode => Outcome.ToString();
    }
}
