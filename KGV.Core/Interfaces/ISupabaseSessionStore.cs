using Supabase.Gotrue;

namespace KGV.Core.Interfaces
{
    public interface ISupabaseSessionStore
    {
        Session? Load();
        void Save(Session session);
        void Clear();
    }
}
