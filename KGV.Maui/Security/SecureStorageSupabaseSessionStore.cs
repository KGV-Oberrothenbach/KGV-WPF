using KGV.Core.Interfaces;
using Supabase.Gotrue;
using System;
using System.Text.Json;

namespace KGV.Maui.Security;

public sealed class SecureStorageSupabaseSessionStore : ISupabaseSessionStore
{
    private const string StorageKey = "kgv.supabase.session";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public Session? Load()
    {
        try
        {
            var json = SecureStorage.Default.GetAsync(StorageKey).ConfigureAwait(false).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<Session>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public void Save(Session session)
    {
        try
        {
            if (session == null)
                return;

            var json = JsonSerializer.Serialize(session, JsonOpts);
            SecureStorage.Default.SetAsync(StorageKey, json).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch
        {
        }
    }

    public void Clear()
    {
        try
        {
            SecureStorage.Default.Remove(StorageKey);
        }
        catch
        {
        }
    }
}
