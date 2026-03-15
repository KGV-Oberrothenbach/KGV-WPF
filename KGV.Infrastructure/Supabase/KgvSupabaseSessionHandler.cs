using KGV.Core.Interfaces;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using System;

namespace KGV.Infrastructure.Supabase
{
    internal sealed class KgvSupabaseSessionHandler : IGotrueSessionPersistence<Session>
    {
        private readonly ISupabaseSessionStore _store;

        public KgvSupabaseSessionHandler(ISupabaseSessionStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public void SaveSession(Session session)
        {
            try
            {
                if (session == null) return;
                _store.Save(session);
            }
            catch
            {
                // Never throw from persistence handler.
            }
        }

        public void DestroySession()
        {
            try
            {
                _store.Clear();
            }
            catch
            {
                // Never throw from persistence handler.
            }
        }

        public Session? LoadSession()
        {
            try
            {
                return _store.Load();
            }
            catch
            {
                // Never throw from persistence handler.
                return null;
            }
        }
    }
}
