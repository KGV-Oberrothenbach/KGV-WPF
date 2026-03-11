using KGV.Core.Security;
using KGV.Maui.Settings;

namespace KGV.Maui.State;

public sealed class UserContextState : IUserContextAccessor
{
    public Guid? CurrentUserId { get; set; }
    public long? CurrentMitgliedId { get; set; }
    public long? CurrentNebenMitgliedId { get; set; }

    public AppMode? CurrentAppMode { get; set; }
    public UserContext? CurrentUserContext { get; set; }
}
