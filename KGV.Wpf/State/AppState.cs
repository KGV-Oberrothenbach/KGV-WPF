using KGV.Core.Security;

namespace KGV.Wpf.State
{
    public static class AppState
    {
        public static UserContext? CurrentUserContext { get; internal set; }
    }
}
