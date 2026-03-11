using System;

namespace KGV.Core.Models
{
    public enum DeleteUserAccountOutcome
    {
        Deleted = 0,
        NoUserAccount = 1,
        NotFound = 2,
        Unauthorized = 3,
        Error = 99
    }

    public sealed record DeleteUserAccountResult(
        DeleteUserAccountOutcome Outcome,
        string Message,
        Guid? AuthUserId = null,
        int? MitgliedId = null)
    {
        public bool Success => Outcome == DeleteUserAccountOutcome.Deleted;

        public string ErrorCode => Outcome.ToString();
    }
}
