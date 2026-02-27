using KGV.Core.Interfaces;
using KGV.Infrastructure.Supabase;
using Supabase;
using Supabase.Postgrest.Exceptions;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KGV.Infrastructure.Services
{
    public class SupabaseService : ISupabaseService
    {
        private readonly ISupabaseClientFactory _clientFactory;
        private readonly ILogger<SupabaseService>? _logger;
        private Client? _client;

        public SupabaseService(ISupabaseClientFactory clientFactory, ILogger<SupabaseService>? logger = null)
        {
            _clientFactory = clientFactory;
            _logger = logger;
        }

        public Client Client => _client ?? throw new InvalidOperationException(
            "Client not initialized. Call InitializeAsync() first."
        );

        public async Task InitializeAsync()
        {
            if (_client != null) return;
            _client = await _clientFactory.CreateAsync();
        }

        // =========================================================
        // LOCKING: Mitglied (DB-Spalten: lockedbyuserid / lockat)
        // =========================================================

        public async Task<bool> TryLockMitgliedAsync(int mitgliedId, string userId, int timeoutMinutes = 10)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                if (!Guid.TryParse(userId, out var userGuid))
                    return false;

                var record = await _client
                    .From<Core.Models.MitgliedRecord>()
                    .Where(m => m.Id == mitgliedId)
                    .Single();

                if (record == null) return false;

                var now = DateTime.UtcNow;

                var lockedBy = record.LockedByUserId;
                var lockAt = record.LockedAt;

                var lockExpired = lockAt.HasValue && lockAt.Value.AddMinutes(timeoutMinutes) < now;

                if (lockedBy == null || lockedBy == userGuid || lockExpired)
                {
                    record.LockedByUserId = userGuid;
                    record.LockedAt = now;

                    await _client
                        .From<Core.Models.MitgliedRecord>()
                        .Where(m => m.Id == mitgliedId)
                        .Update(record);

                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ReleaseLockMitgliedAsync(int mitgliedId, string userId, bool force = false)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                if (!Guid.TryParse(userId, out var userGuid))
                    return false;

                var record = await _client
                    .From<Core.Models.MitgliedRecord>()
                    .Where(m => m.Id == mitgliedId)
                    .Single();

                if (record == null) return false;

                if (!force && record.LockedByUserId != userGuid)
                    return false;

                record.LockedByUserId = null;
                record.LockedAt = null;

                await _client
                    .From<Core.Models.MitgliedRecord>()
                    .Where(m => m.Id == mitgliedId)
                    .Update(record);

                return true;
            }
            catch
            {
                return false;
            }
        }

        // =========================================================
        // (Arbeitsstunde Lock bleibt wie bei dir, unverändert)
        // =========================================================

        public async Task<bool> TryLockArbeitsstundeAsync(int arbeitsstundeId, string userId, int timeoutMinutes = 10)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                var record = await _client.From<Core.Models.ArbeitsstundeRecord>().Where(a => a.Id == arbeitsstundeId).Single();
                if (record == null) return false;

                var now = DateTime.UtcNow;
                if (string.IsNullOrEmpty(record.LockedByUserId) || record.LockedByUserId == userId || (record.LockedAt.HasValue && record.LockedAt.Value.AddMinutes(timeoutMinutes) < now))
                {
                    record.LockedByUserId = userId;
                    record.LockedAt = now;
                    try
                    {
                        await _client.From<Core.Models.ArbeitsstundeRecord>().Where(a => a.Id == arbeitsstundeId).Update(record);
                    }
                    catch
                    {
                    }
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ReleaseLockArbeitsstundeAsync(int arbeitsstundeId, string userId, bool force = false)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                var record = await _client.From<Core.Models.ArbeitsstundeRecord>().Where(a => a.Id == arbeitsstundeId).Single();
                if (record == null) return false;

                if (!force && !string.Equals(record.LockedByUserId, userId, StringComparison.OrdinalIgnoreCase))
                    return false;

                record.LockedByUserId = string.Empty;
                record.LockedAt = null;
                try
                {
                    await _client.From<Core.Models.ArbeitsstundeRecord>().Where(a => a.Id == arbeitsstundeId).Update(record);
                }
                catch
                {
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        // =========================================================
        // Seasons
        // =========================================================

        public async Task<System.Collections.Generic.List<string>> GetSeasonsAsync()
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return new System.Collections.Generic.List<string>();

                var resp = await _client.From<Core.Models.SaisonRecord>().Get();
                var list = new System.Collections.Generic.List<string>();

                if (resp?.Models != null)
                {
                    var temp = new System.Collections.Generic.List<int>();
                    foreach (var s in resp.Models)
                        temp.Add(s.Jahr);

                    temp.Sort();
                    foreach (var iv in temp)
                        list.Add(iv.ToString());
                }

                return list;
            }
            catch
            {
                return new System.Collections.Generic.List<string>();
            }
        }

        // =========================================================
        // Mitglieder (HIER war dein Problem)
        // =========================================================

        public async Task<System.Collections.Generic.List<Core.Models.MitgliedRecord>> GetMitgliederAsync()
        {
            var result = new System.Collections.Generic.List<Core.Models.MitgliedRecord>();
            try
            {
                await InitializeAsync();
                if (_client == null)
                {
                    _logger?.LogWarning("Supabase client is null in GetMitgliederAsync");
                    return result;
                }

                try
                {
                    var currentUser = _client.Auth?.CurrentUser?.Id;
                    _logger?.LogDebug("GetMitgliederAsync: supabase auth user id = {UserId}", currentUser ?? "<null>");
                }
                catch
                {
                }

                var resp = await _client.From<Core.Models.MitgliedRecord>().Get();

                if (resp?.Models != null)
                {
                    // HIER kommt jetzt alles an, was MitgliedRecord definiert
                    result.AddRange(resp.Models);
                }
            }
            catch (PostgrestException pex)
            {
                _logger?.LogError(pex, "Postgrest query failed when fetching Mitglieder: {Message}", pex.Message);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error when fetching Mitglieder");
            }

            return result;
        }

        // =========================================================
        // Der Rest bleibt wie bei dir (Parzellen / Belegung etc.)
        // =========================================================

        public async Task<Core.Models.ParzelleRecord?> GetParzelleByNumberAsync(string gartenNr)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return null;

                var resp = await _client.From<Core.Models.ParzelleRecord>().Get();
                if (resp?.Models != null)
                {
                    foreach (var pr in resp.Models)
                    {
                        if (string.Equals(pr.GartenNr, gartenNr, StringComparison.OrdinalIgnoreCase))
                            return pr;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public async Task<System.Collections.Generic.List<Core.Models.ParzelleRecord>> GetAllParzellenAsync()
        {
            var list = new System.Collections.Generic.List<Core.Models.ParzelleRecord>();
            try
            {
                await InitializeAsync();
                if (_client == null) return list;

                var resp = await _client.From<Core.Models.ParzelleRecord>().Get();
                if (resp?.Models != null)
                    list.AddRange(resp.Models);
            }
            catch
            {
            }

            return list;
        }

        public async Task<Core.Models.ParzellenBelegungRecord?> GetCurrentBelegungForParzelleAsync(int parzelleId)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return null;

                var resp = await _client.From<Core.Models.ParzellenBelegungRecord>().Get();
                if (resp?.Models != null)
                {
                    var now = DateTime.UtcNow;

                    foreach (var br in resp.Models)
                    {
                        if (br.ParzelleId != parzelleId) continue;

                        var von = br.VonDatum ?? DateTime.MinValue;
                        var bis = br.BisDatum ?? DateTime.MaxValue;

                        if (von <= now && bis >= now)
                            return br;
                    }
                }
            }
            catch
            {
            }

            return null;
        }
    }
}