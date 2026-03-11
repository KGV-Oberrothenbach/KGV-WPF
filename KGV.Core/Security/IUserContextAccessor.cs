namespace KGV.Core.Security
{
    public interface IUserContextAccessor
    {
        UserContext? CurrentUserContext { get; }
    }
}
