// File: Infrastructure/Services/SupabaseService.cs
using KGV.Core.Interfaces;
using KGV.Core.Helpers;
using KGV.Core.Models;
using KGV.Core.Security;
using KGV.Infrastructure.Models;
using KGV.Infrastructure.Supabase;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Supabase;
using Supabase.Postgrest.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KGV.Infrastructure.Services
{
    public class SupabaseService : ISupabaseService
    {
        private const string DokumenteBucket = "dokumente";
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        private HttpClient? _http;
        private const short ZaehlerTypStrom = 1;
        private const short ZaehlerTypWasser = 2;
        private readonly ISupabaseClientFactory _clientFactory;
        private readonly IAuthService _authService;
        private readonly ILogger<SupabaseService>? _logger;
        private readonly Func<UserContext?>? _userContextAccessor;
        private readonly bool _enableLegacyRoleBefreiung;
        private Client? _client;

        public SupabaseService(
            ISupabaseClientFactory clientFactory,
            IAuthService authService,
            ILogger<SupabaseService>? logger = null,
            Func<UserContext?>? userContextAccessor = null,
            IConfiguration? configuration = null)
        {
            _clientFactory = clientFactory;
            _authService = authService;
            _logger = logger;
            _userContextAccessor = userContextAccessor;

            // Übergangsregel (Legacy): kann später per Konfiguration abgeschaltet werden,
            // ohne dass WPF/MAUI dafür Sonderlogik brauchen.
            _enableLegacyRoleBefreiung = configuration?.GetValue("Workhours:EnableLegacyRoleBefreiung", true) ?? true;
        }

        public async Task<List<AppUserDTO>> GetAppUsersAsync()
        {
            var list = new List<AppUserDTO>();

            try
            {
                await InitializeAsync();
                if (_client == null) return list;

                var resp = await _client
                    .From<AppUserRecord>()
                    .Get();

                if (resp?.Models == null) return list;

                foreach (var r in resp.Models)
                {
                    list.Add(new AppUserDTO
                    {
                        UserId = r.UserId,
                        MitgliedId = r.MitgliedId,
                        Role = r.Role ?? string.Empty
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetAppUsersAsync failed");
            }

            return list;
        }

        // =========================
        // Saison / Wartungsverträge / Pflichtstunden
        // =========================
        public async Task<SaisonRecord?> SaveSaisonAsync(SaisonRecord saison)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return null;
                if (saison == null) return null;

                if (saison.Id > 0)
                {
                    var resp = await _client
                        .From<SaisonRecord>()
                        .Where(x => x.Id == saison.Id)
                        .Update(saison);

                    return resp?.Models?.FirstOrDefault();
                }

                var insertResp = await _client
                    .From<SaisonRecord>()
                    .Insert(saison);

                return insertResp?.Models?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "SaveSaisonAsync failed");
                return null;
            }
        }

        public async Task<List<WartungsvertragRecord>> GetWartungsvertraegeAsync()
        {
            var list = new List<WartungsvertragRecord>();

            try
            {
                await InitializeAsync();
                if (_client == null) return list;

                var resp = await _client.From<WartungsvertragRecord>().Get();
                if (resp?.Models != null) list.AddRange(resp.Models);

                return list
                    .OrderByDescending(x => x.Aktiv)
                    .ThenBy(x => x.Bereich)
                    .ThenBy(x => x.Titel)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetWartungsvertraegeAsync failed");
                return list;
            }
        }

        public async Task<WartungsvertragRecord?> SaveWartungsvertragAsync(WartungsvertragRecord wartungsvertrag)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return null;
                if (wartungsvertrag == null) return null;

                if (wartungsvertrag.Id > 0)
                {
                    var resp = await _client
                        .From<WartungsvertragRecord>()
                        .Where(x => x.Id == wartungsvertrag.Id)
                        .Update(wartungsvertrag);

                    return resp?.Models?.FirstOrDefault();
                }

                var insertResp = await _client
                    .From<WartungsvertragRecord>()
                    .Insert(wartungsvertrag);

                return insertResp?.Models?.FirstOrDefault();
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (PostgrestException ex)
            {
                _logger?.LogError(ex, "SaveWartungsvertragAsync failed");
                throw new InvalidOperationException(BuildUserFacingSaveError(ex), ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "SaveWartungsvertragAsync failed");
                throw new InvalidOperationException(BuildUserFacingSaveError(ex), ex);
            }
        }

        public async Task<List<WartungsvertragZuordnungRecord>> GetWartungsvertragZuordnungenAsync(int hauptmitgliedId)
        {
            var list = new List<WartungsvertragZuordnungRecord>();

            try
            {
                await InitializeAsync();
                if (_client == null) return list;
                if (hauptmitgliedId <= 0) return list;

                var resp = await _client
                    .From<WartungsvertragZuordnungRecord>()
                    .Where(x => x.HauptmitgliedId == hauptmitgliedId)
                    .Get();

                if (resp?.Models != null) list.AddRange(resp.Models);

                return list
                    .OrderByDescending(x => x.GueltigAb)
                    .ThenByDescending(x => x.Id)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetWartungsvertragZuordnungenAsync failed");
                return list;
            }
        }

        public async Task<WartungsvertragZuordnungRecord?> SaveWartungsvertragZuordnungAsync(WartungsvertragZuordnungRecord zuordnung)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return null;
                if (zuordnung == null) return null;

                if (zuordnung.HauptmitgliedId <= 0)
                    throw new InvalidOperationException("HauptmitgliedId fehlt.");

                if (zuordnung.WartungsvertragId <= 0)
                    throw new InvalidOperationException("WartungsvertragId fehlt.");

                var contract = await _client
                    .From<WartungsvertragRecord>()
                    .Where(x => x.Id == zuordnung.WartungsvertragId)
                    .Single();

                if (contract == null)
                    throw new InvalidOperationException("Wartungsvertrag existiert nicht.");

                if (!contract.Aktiv)
                    throw new InvalidOperationException($"Wartungsvertrag '{contract.Titel}' ist deaktiviert und kann nicht zugeordnet werden.");

                var when = (zuordnung.GueltigAb == default ? DateTime.Today : zuordnung.GueltigAb.Date);
                if (when < DateTime.Today) when = DateTime.Today;

                static bool IsActiveAt(WartungsvertragZuordnungRecord x, DateTime at)
                {
                    if (x.GueltigAb.Date > at) return false;
                    if (!x.GueltigBis.HasValue) return true;
                    return x.GueltigBis.Value.Date >= at;
                }

                // Regel 1: Duplikatschutz (kein gleicher Vertrag gleichzeitig aktiv für dasselbe Mitglied)
                var existingForMember = await _client
                    .From<WartungsvertragZuordnungRecord>()
                    .Where(x => x.HauptmitgliedId == zuordnung.HauptmitgliedId)
                    .Where(x => x.WartungsvertragId == zuordnung.WartungsvertragId)
                    .Get();

                if (existingForMember?.Models?.Any(x => x.Id != zuordnung.Id && IsActiveAt(x, when)) == true)
                    throw new InvalidOperationException($"Der Wartungsvertrag '{contract.Titel}' ist für dieses Mitglied bereits aktiv zugeordnet.");

                // Regel 2: Kapazität (MaxAktiveZuordnungen pro Vertrag)
                if (contract.MaxAktiveZuordnungen > 0)
                {
                    var allForContract = await _client
                        .From<WartungsvertragZuordnungRecord>()
                        .Where(x => x.WartungsvertragId == zuordnung.WartungsvertragId)
                        .Get();

                    var activeCount = allForContract?.Models?.Count(x => x.Id != zuordnung.Id && IsActiveAt(x, when)) ?? 0;
                    if (activeCount >= contract.MaxAktiveZuordnungen)
                        throw new InvalidOperationException($"Kapazität erreicht: '{contract.Titel}' erlaubt max. {contract.MaxAktiveZuordnungen} aktive Zuordnung(en). Aktuell aktiv: {activeCount}.");
                }

                if (zuordnung.Id > 0)
                {
                    var resp = await _client
                        .From<WartungsvertragZuordnungRecord>()
                        .Where(x => x.Id == zuordnung.Id)
                        .Update(zuordnung);

                    return resp?.Models?.FirstOrDefault();
                }

                var insertResp = await _client
                    .From<WartungsvertragZuordnungRecord>()
                    .Insert(zuordnung);

                return insertResp?.Models?.FirstOrDefault();
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (PostgrestException ex)
            {
                var msg = ex.Message ?? string.Empty;

                if (msg.Contains("Kapazität erreicht", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(msg, ex);

                if (msg.Contains("wartungsvertrag_zuordnungen_no_overlap", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("exclusion constraint", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Diese Zuordnung überschneidet sich mit einer bestehenden Zuordnung (Duplikat/Überlappung).", ex);

                _logger?.LogError(ex, "SaveWartungsvertragZuordnungAsync failed");
                throw new InvalidOperationException(BuildUserFacingSaveError(ex), ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "SaveWartungsvertragZuordnungAsync failed");
                throw new InvalidOperationException(BuildUserFacingSaveError(ex), ex);
            }
        }

        public async Task<bool> EndWartungsvertragZuordnungAsync(long zuordnungId, DateTime gueltigBis, string? bemerkung)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;
                if (zuordnungId <= 0) return false;

                var rec = await _client
                    .From<WartungsvertragZuordnungRecord>()
                    .Where(x => x.Id == zuordnungId)
                    .Single();

                if (rec == null) return false;

                rec.GueltigBis = DateTime.SpecifyKind(gueltigBis.Date.AddHours(12), DateTimeKind.Unspecified);
                if (!string.IsNullOrWhiteSpace(bemerkung))
                    rec.Bemerkung = bemerkung;

                await _client
                    .From<WartungsvertragZuordnungRecord>()
                    .Where(x => x.Id == zuordnungId)
                    .Update(rec);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "EndWartungsvertragZuordnungAsync failed");
                return false;
            }
        }

        public async Task<PflichtstundenUebersichtRecord?> GetPflichtstundenUebersichtAsync(int hauptmitgliedId, int saisonId)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return null;
                if (hauptmitgliedId <= 0) return null;
                if (saisonId <= 0) return null;

                var resp = await _client
                    .From<PflichtstundenUebersichtRecord>()
                    .Where(x => x.HauptmitgliedId == hauptmitgliedId)
                    .Where(x => x.SaisonId == saisonId)
                    .Get();

                return resp?.Models?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetPflichtstundenUebersichtAsync failed");
                return null;
            }
        }

        public async Task<PflichtstundenEvaluationResult?> GetPflichtstundenEvaluationAsync(int hauptmitgliedId, int saisonId, DateTime? asOfDate = null)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return null;
                if (hauptmitgliedId <= 0) return null;
                if (saisonId <= 0) return null;

                var saisonen = await GetSaisonRecordsAsync();
                var saison = saisonen?.FirstOrDefault(x => x.Id == saisonId);
                if (saison == null) return null;

                var rec = await GetPflichtstundenUebersichtAsync(hauptmitgliedId, saisonId);
                if (rec == null) return null;

                var when = (asOfDate ?? DateTime.Today).Date;

                // Priorität 1: Befreiung über aktiven Wartungsvertrag (beliebig viele aktiv -> ein befreiender reicht)
                WartungsvertragRecord? befreitVertrag = null;
                WartungsvertragZuordnungRecord? befreitZuordnung = null;

                var contracts = await GetWartungsvertraegeAsync();
                if (contracts != null && contracts.Count > 0)
                {
                    var contractById = contracts.Where(x => x != null).ToDictionary(x => x.Id, x => x);
                    var z = await GetWartungsvertragZuordnungenAsync(hauptmitgliedId);

                    if (z != null && z.Count > 0)
                    {
                        bool IsActiveAt(WartungsvertragZuordnungRecord x)
                        {
                            if (x.GueltigAb.Date > when) return false;
                            if (!x.GueltigBis.HasValue) return true;
                            return x.GueltigBis.Value.Date >= when;
                        }

                        var candidates = z
                            .Where(x => x != null)
                            .Where(IsActiveAt)
                            .OrderByDescending(x => x.GueltigAb)
                            .ToList();

                        foreach (var one in candidates)
                        {
                            if (!contractById.TryGetValue(one.WartungsvertragId, out var c))
                                continue;

                            if (!c.Aktiv)
                                continue;

                            if (!c.BefreitVonPflichtstunden)
                                continue;

                            befreitZuordnung = one;
                            befreitVertrag = c;
                            break;
                        }
                    }
                }

                // Priorität 2: Übergangsregel (Legacy) über Rolle (zentral, später abschaltbar)
                string? legacyRole = null;
                var isLegacyRoleBefreit = false;
                if (befreitVertrag == null && _enableLegacyRoleBefreiung)
                {
                    legacyRole = (await GetAppUserRoleForMitgliedAsync(hauptmitgliedId) ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(legacyRole))
                    {
                        var m = await GetMitgliedByIdAsync(hauptmitgliedId);
                        legacyRole = (m?.Role ?? string.Empty).Trim();
                    }

                    isLegacyRoleBefreit = legacyRole.Equals("admin", StringComparison.OrdinalIgnoreCase)
                                          || legacyRole.Equals("vorstand", StringComparison.OrdinalIgnoreCase);
                }

                var istBefreit = befreitVertrag != null || isLegacyRoleBefreit;
                var quelle = befreitVertrag != null
                    ? PflichtstundenBefreiungsQuelle.Wartungsvertrag
                    : (isLegacyRoleBefreit ? PflichtstundenBefreiungsQuelle.LegacyRole : PflichtstundenBefreiungsQuelle.None);

                // Wichtig: Befreiung darf NICHT die Summe der geleisteten Stunden auf 0 setzen.
                // Wir ermitteln Geleistet zentral über die tatsächlich erfassten Arbeitsstunden,
                // damit Startseite und "Meine Arbeitsstunden" identische Fachlogik nutzen.
                var geleistet = rec.Geleistet;
                try
                {
                    var ids = new List<int> { hauptmitgliedId };
                    var neben = await GetNebenmitgliedByHauptmitgliedIdAsync(hauptmitgliedId);
                    if (neben != null && neben.Id > 0)
                        ids.Add(neben.Id);

                    var hours = await GetArbeitsstundenAsync(ids.ToArray());
                    if (hours != null && hours.Count > 0)
                    {
                        geleistet = hours
                            .Where(x => x != null)
                            .Where(x => x.SaisonId == saisonId)
                            .Where(x => x.Datum.Date <= when)
                            .Where(x => x.Freigegeben)
                            .Sum(x => x.Stunden);
                    }
                }
                catch
                {
                    // Fallback: falls die Detailabfrage scheitert, nutzen wir die View-Auswertung.
                    geleistet = rec.Geleistet;
                }

                var baseOffen = rec.Sollstunden - geleistet;
                if (baseOffen < 0m) baseOffen = 0m;

                var soll = istBefreit ? 0m : rec.Sollstunden;
                var offen = istBefreit ? 0m : baseOffen;

                var euro = saison.EuroProFehlstunde;
                var fehl = istBefreit ? 0m : offen * euro;
                if (fehl < 0m) fehl = 0m;

                var grund = string.Empty;
                if (istBefreit)
                {
                    if (befreitVertrag != null)
                        grund = $"Befreit durch Wartungsvertrag: {befreitVertrag.Titel}";
                    else
                        grund = "Befreit (Übergangsregel)";
                }
                else
                {
                    grund = !string.IsNullOrWhiteSpace(rec.Befreiungsgrund)
                        ? rec.Befreiungsgrund!
                        : (rec.Regelgrund ?? string.Empty);
                }

                return new PflichtstundenEvaluationResult
                {
                    HauptmitgliedId = hauptmitgliedId,
                    SaisonId = saisonId,
                    Jahr = saison.Jahr,
                    Sollstunden = soll,
                    Geleistet = geleistet,
                    OffeneStunden = offen,
                    Fehlbetrag = fehl,
                    EuroProFehlstunde = euro,
                    IstBefreit = istBefreit,
                    BefreiungsQuelle = quelle,
                    Grund = grund,
                    BefreienderWartungsvertragId = befreitVertrag?.Id,
                    BefreienderWartungsvertragTitel = befreitVertrag?.Titel,
                    BefreienderWartungsvertragBereich = befreitVertrag?.Bereich,
                    LegacyRole = isLegacyRoleBefreit ? legacyRole : null
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetPflichtstundenEvaluationAsync failed");
                return null;
            }
        }

        public async Task<List<PflichtstundenUebersichtRecord>> GetPflichtstundenUebersichtForSaisonAsync(int saisonId)
        {
            var list = new List<PflichtstundenUebersichtRecord>();

            try
            {
                await InitializeAsync();
                if (_client == null) return list;
                if (saisonId <= 0) return list;

                var resp = await _client
                    .From<PflichtstundenUebersichtRecord>()
                    .Where(x => x.SaisonId == saisonId)
                    .Get();

                if (resp?.Models != null) list.AddRange(resp.Models);
                return list.OrderBy(x => x.HauptmitgliedId).ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetPflichtstundenUebersichtForSaisonAsync failed");
                return list;
            }
        }

        // =========================
        // app_user (Rollenquelle)
        // =========================
        public async Task<bool> HasAppUserForMitgliedAsync(long mitgliedId)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;
                if (mitgliedId <= 0) return false;

                var resp = await _client
                    .From<AppUserRecord>()
                    .Where(x => x.MitgliedId == mitgliedId)
                    .Get();

                return resp?.Models?.Any() == true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string?> GetAppUserRoleForMitgliedAsync(long mitgliedId)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return null;
                if (mitgliedId <= 0) return null;

                var resp = await _client
                    .From<AppUserRecord>()
                    .Where(x => x.MitgliedId == mitgliedId)
                    .Get();

                var one = resp?.Models?.FirstOrDefault();
                return one?.Role;
            }

            catch
            {
                return null;
            }
        }

        public async Task<bool> UpdateMitgliedRoleForMitgliedAsync(int mitgliedId, string role, string userId)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;
                if (mitgliedId <= 0) return false;

                if (!Guid.TryParse(userId, out var userGuid))
                    return false;

                role = (role ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(role))
                    return false;

                var record = await _client
                    .From<MitgliedRecord>()
                    .Where(m => m.Id == mitgliedId)
                    .Single();

                if (record == null) return false;
                if (record.LockedByUserId != userGuid) return false;

                // Übergangs-Phase: vorbereitete Rolle bleibt in `mitglied.role`, solange kein app_user existiert.
                record.Role = role;

                await _client
                    .From<MitgliedRecord>()
                    .Where(m => m.Id == mitgliedId)
                    .Update(record);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "UpdateMitgliedRoleForMitgliedAsync failed");
                return false;
            }
        }

        public async Task<bool> UpdateAppUserRoleForMitgliedAsync(long mitgliedId, string role)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;
                if (mitgliedId <= 0) return false;

                role = (role ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(role))
                    return false;

                var existing = await _client
                    .From<AppUserRecord>()
                    .Where(x => x.MitgliedId == mitgliedId)
                    .Single();

                if (existing == null)
                    return false;

                existing.Role = role;

                await _client
                    .From<AppUserRecord>()
                    .Where(x => x.UserId == existing.UserId)
                    .Update(existing);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "UpdateAppUserRoleForMitgliedAsync failed");
                return false;
            }
        }

        private async Task TryApplyAppUserRoleAsync(MitgliedRecord member)
        {
            try
            {
                if (_client == null) return;
                if (member == null) return;

                // Übergangs-Phase:
                // - app_user.role ist führend, falls vorhanden
                // - sonst bleibt mitglied.role als vorbereitete Zielrolle erhalten
                var role = (await GetAppUserRoleForMitgliedAsync(member.Id) ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(role))
                    member.Role = role;
            }
            catch
            {
                // ignore (role stays as-is)
            }
        }

        private async Task TryApplyAppUserRolesAsync(List<MitgliedRecord> members)
        {
            try
            {
                if (_client == null) return;
                if (members == null || members.Count == 0) return;

                // Fetch roles in one request.
                var resp = await _client.From<AppUserRecord>().Get();
                var appUsers = resp?.Models;
                if (appUsers == null) return;

                var roleByMitgliedId = appUsers
                    .Where(x => x.MitgliedId.HasValue)
                    .GroupBy(x => x.MitgliedId!.Value)
                    .ToDictionary(g => g.Key, g => g.FirstOrDefault()?.Role);

                foreach (var m in members)
                {
                    // Übergangs-Phase: nur überschreiben, wenn app_user.role existiert.
                    if (roleByMitgliedId.TryGetValue(m.Id, out var role))
                    {
                        var trimmed = (role ?? string.Empty).Trim();
                        if (!string.IsNullOrWhiteSpace(trimmed))
                            m.Role = trimmed;
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        // =========================
        // Nutzerverwaltung (Admin)
        // =========================
        public async Task<PrepareAddUserResult> PrepareAddUserForMitgliedAsync(int mitgliedId, string role)
        {
            try
            {
                if (mitgliedId <= 0)
                    return new PrepareAddUserResult(PrepareAddUserOutcome.Error, "Ungültige Mitglieds-ID.", mitgliedId, string.Empty, string.Empty);

                role = (role ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(role) || !UserRoles.All.Contains(role))
                    return new PrepareAddUserResult(PrepareAddUserOutcome.InvalidRole, "Ungültige Rolle.", mitgliedId, string.Empty, role);

                var rec = await GetMitgliedByIdAsync(mitgliedId);
                if (rec == null)
                    return new PrepareAddUserResult(PrepareAddUserOutcome.NotFound, "Mitglied nicht gefunden.", mitgliedId, string.Empty, role);

                // Wenn bereits ein Auth-User verknüpft ist, darf nicht erneut eingeladen werden.
                if (rec.AuthUserId.HasValue)
                    return new PrepareAddUserResult(PrepareAddUserOutcome.UserAlreadyExists, "Für dieses Mitglied existiert bereits ein Nutzerkonto.", mitgliedId, (rec.Email ?? string.Empty).Trim(), role);

                var email = (rec.Email ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(email))
                    return new PrepareAddUserResult(PrepareAddUserOutcome.MissingEmail, "Keine E-Mail-Adresse vorhanden.", mitgliedId, string.Empty, role);

                var hasAppUser = await HasAppUserForMitgliedAsync(mitgliedId);
                if (hasAppUser)
                    return new PrepareAddUserResult(PrepareAddUserOutcome.UserAlreadyExists, "Für dieses Mitglied existiert bereits ein Nutzerkonto.", mitgliedId, email, role);

                // Kein echter Invite in diesem Schritt – nur die Vorbedingungen bündeln.
                return PrepareAddUserResult.Ready(mitgliedId, email, role);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "PrepareAddUserForMitgliedAsync failed");
                return PrepareAddUserResult.Error($"Fehler: {ex.Message}", mitgliedId);
            }
        }

        public async Task<InviteUserAccountResult> InviteUserAccountForMitgliedAsync(int mitgliedId, string role)
        {
            try
            {
                await InitializeAsync();
                if (_client == null || _http == null)
                    return new InviteUserAccountResult(InviteUserAccountOutcome.Error, "Supabase ist nicht initialisiert.", MitgliedId: mitgliedId);

                if (mitgliedId <= 0)
                    return new InviteUserAccountResult(InviteUserAccountOutcome.Error, "Ungültige Mitglieds-ID.", MitgliedId: mitgliedId);

                role = (role ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(role))
                    return new InviteUserAccountResult(InviteUserAccountOutcome.InvalidRole, "Bitte Rolle auswählen.", MitgliedId: mitgliedId);

                var urlBase = _clientFactory.Url.TrimEnd('/');
                if (string.IsNullOrWhiteSpace(urlBase))
                    return new InviteUserAccountResult(InviteUserAccountOutcome.Error, "Supabase-URL fehlt.", MitgliedId: mitgliedId);

                var requestUrl = $"{urlBase}/functions/v1/kgv-invite-user";

                using var req = new HttpRequestMessage(HttpMethod.Post, requestUrl);

                // Access Token aus der aktuellen Supabase-Session (JWT) ermitteln.
                var token = await TryGetCurrentAccessTokenAsync();
                if (string.IsNullOrWhiteSpace(token))
                {
                    return new InviteUserAccountResult(
                        InviteUserAccountOutcome.Unauthorized,
                        "Kein gültiger AccessToken in der aktuellen Supabase-Session. Bitte erneut einloggen.",
                        MitgliedId: mitgliedId);
                }

                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var anonKey = _clientFactory.Key;
                if (!string.IsNullOrWhiteSpace(anonKey))
                    req.Headers.Add("apikey", anonKey);

                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var payload = JsonSerializer.Serialize(new { mitgliedId, role });
                req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                using var resp = await _http.SendAsync(req);

                var body = await resp.Content.ReadAsStringAsync();
                var statusCode = resp.StatusCode;
                var reasonPhrase = resp.ReasonPhrase;

                // Minimaler Selbstheilungsversuch: wenn der Server explizit "Invalid JWT" meldet,
                // einmal Session refreshen und Request erneut senden.
                if ((resp.StatusCode == System.Net.HttpStatusCode.Unauthorized || resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    && !string.IsNullOrWhiteSpace(body)
                    && body.Contains("Invalid JWT", StringComparison.OrdinalIgnoreCase))
                {
                    await _authService.EnsureValidSessionAsync(forceRefresh: true);

                    var refreshedToken = _client?.Auth?.CurrentSession?.AccessToken;
                    if (!string.IsNullOrWhiteSpace(refreshedToken) && !string.Equals(refreshedToken, token, StringComparison.Ordinal))
                    {
                        using var retryReq = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                        retryReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedToken);

                        if (!string.IsNullOrWhiteSpace(anonKey))
                            retryReq.Headers.Add("apikey", anonKey);

                        retryReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        retryReq.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                        using var retryResp = await _http.SendAsync(retryReq);
                        statusCode = retryResp.StatusCode;
                        reasonPhrase = retryResp.ReasonPhrase;
                        body = await retryResp.Content.ReadAsStringAsync();

                        // Wenn Retry weiterhin fehlschlägt, läuft es unten in die normale Fehlerbehandlung.
                        // (Statuscode wird über retryResp genutzt)
                        if (retryResp.IsSuccessStatusCode)
                        {
                            var retryDto = JsonSerializer.Deserialize<InviteUserAccountResultDto>(body, JsonOpts);
                            var retryModel = retryDto?.ToModel() ?? new InviteUserAccountResult(InviteUserAccountOutcome.Error, "Unerwartete Antwort vom Server.");
                            return retryModel with { MitgliedId = mitgliedId };
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(body))
                {
                    return new InviteUserAccountResult(
                        InviteUserAccountOutcome.Error,
                        $"Leere Antwort vom Server. HTTP {(int)statusCode} {reasonPhrase}",
                        MitgliedId: mitgliedId);
                }

                if ((int)statusCode < 200 || (int)statusCode >= 300)
                {
                    // try to parse structured error
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<InviteUserAccountResultDto>(body, JsonOpts);
                        if (parsed != null)
                        {
                            var parsedModel = parsed.ToModel() with { MitgliedId = mitgliedId };
                            // minimale Diagnose: HTTP Status + Response Body
                            return parsedModel with
                            {
                                Message = $"HTTP {(int)statusCode} {reasonPhrase}: {parsedModel.Message} | Body: {body}"
                            };
                        }
                    }
                    catch
                    {
                    }

                    return new InviteUserAccountResult(
                        InviteUserAccountOutcome.Error,
                        $"HTTP {(int)statusCode} {reasonPhrase} | Body: {body}",
                        MitgliedId: mitgliedId);
                }

                var dto = JsonSerializer.Deserialize<InviteUserAccountResultDto>(body, JsonOpts);
                var model = dto?.ToModel() ?? new InviteUserAccountResult(InviteUserAccountOutcome.Error, "Unerwartete Antwort vom Server.");
                return model with { MitgliedId = mitgliedId };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "InviteUserAccountForMitgliedAsync failed");
                return new InviteUserAccountResult(InviteUserAccountOutcome.Error, $"Fehler: {ex.Message}", MitgliedId: mitgliedId);
            }
        }

        public async Task<DeleteUserAccountResult> DeleteUserAccountForMitgliedAsync(int mitgliedId)
        {
            try
            {
                await InitializeAsync();
                if (_client == null || _http == null)
                    return new DeleteUserAccountResult(DeleteUserAccountOutcome.Error, "Supabase ist nicht initialisiert.", MitgliedId: mitgliedId);

                if (mitgliedId <= 0)
                    return new DeleteUserAccountResult(DeleteUserAccountOutcome.Error, "Ungültige Mitglieds-ID.", MitgliedId: mitgliedId);

                var urlBase = _clientFactory.Url.TrimEnd('/');
                if (string.IsNullOrWhiteSpace(urlBase))
                    return new DeleteUserAccountResult(DeleteUserAccountOutcome.Error, "Supabase-URL fehlt.", MitgliedId: mitgliedId);

                var requestUrl = $"{urlBase}/functions/v1/kgv-delete-user";

                Debug.WriteLine($"[kgv-delete-user] RequestUrl={requestUrl} mitgliedId={mitgliedId}");

                using var req = new HttpRequestMessage(HttpMethod.Post, requestUrl);

                var token = await TryGetCurrentAccessTokenAsync();
                if (string.IsNullOrWhiteSpace(token))
                {
                    return new DeleteUserAccountResult(
                        DeleteUserAccountOutcome.Unauthorized,
                        "Kein gültiger AccessToken in der aktuellen Supabase-Session. Bitte erneut einloggen.",
                        MitgliedId: mitgliedId);
                }

                Debug.WriteLine($"[kgv-delete-user] HasAccessToken=true len={token.Length}");

                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var anonKey = _clientFactory.Key;
                if (!string.IsNullOrWhiteSpace(anonKey))
                {
                    req.Headers.Add("apikey", anonKey);
                    Debug.WriteLine("[kgv-delete-user] apikey header set");
                }

                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var payload = JsonSerializer.Serialize(new { mitgliedId });
                req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                using var resp = await _http.SendAsync(req);

                var body = await resp.Content.ReadAsStringAsync();
                var statusCode = resp.StatusCode;
                var reasonPhrase = resp.ReasonPhrase;

                Debug.WriteLine($"[kgv-delete-user] HTTP {(int)statusCode} {reasonPhrase}");
                Debug.WriteLine($"[kgv-delete-user] RawBody={body}");

                if ((resp.StatusCode == System.Net.HttpStatusCode.Unauthorized || resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    && !string.IsNullOrWhiteSpace(body)
                    && body.Contains("Invalid JWT", StringComparison.OrdinalIgnoreCase))
                {
                    await _authService.EnsureValidSessionAsync(forceRefresh: true);

                    var refreshedToken = _client?.Auth?.CurrentSession?.AccessToken;
                    if (!string.IsNullOrWhiteSpace(refreshedToken) && !string.Equals(refreshedToken, token, StringComparison.Ordinal))
                    {
                        using var retryReq = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                        retryReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedToken);

                        if (!string.IsNullOrWhiteSpace(anonKey))
                            retryReq.Headers.Add("apikey", anonKey);

                        retryReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        retryReq.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                        using var retryResp = await _http.SendAsync(retryReq);
                        statusCode = retryResp.StatusCode;
                        reasonPhrase = retryResp.ReasonPhrase;
                        body = await retryResp.Content.ReadAsStringAsync();

                        if (retryResp.IsSuccessStatusCode)
                        {
                            var retryDto = JsonSerializer.Deserialize<DeleteUserAccountResultDto>(body, JsonOpts);
                            if (retryDto == null)
                                return new DeleteUserAccountResult(DeleteUserAccountOutcome.Error, "Unerwartete Antwort vom Server.", MitgliedId: mitgliedId);

                            // Schutz: wenn die Function nicht das erwartete Contract-JSON liefert (z.B. alte Stub-/Hello-Antwort),
                            // darf diese Meldung nicht ungeprüft als UI-Text durchgereicht werden.
                            if (string.IsNullOrWhiteSpace(retryDto.Outcome))
                            {
                                _logger?.LogWarning("Unexpected kgv-delete-user response (missing outcome). Body: {Body}", body);
                                Debug.WriteLine($"[kgv-delete-user] DTO missing outcome. DTO.Message='{retryDto.Message}' AuthUserId='{retryDto.AuthUserId}' ExtraKeys={retryDto.ExtraKeys}");
                                return new DeleteUserAccountResult(DeleteUserAccountOutcome.Error, "Unerwartete Antwort vom Server.", MitgliedId: mitgliedId);
                            }

                            Debug.WriteLine($"[kgv-delete-user] DTO outcome='{retryDto.Outcome}' message='{retryDto.Message}' authUserId='{retryDto.AuthUserId}' ExtraKeys={retryDto.ExtraKeys}");
                            var retryModel = retryDto.ToModel();
                            return retryModel with { MitgliedId = mitgliedId };
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(body))
                {
                    return new DeleteUserAccountResult(
                        DeleteUserAccountOutcome.Error,
                        $"Leere Antwort vom Server. HTTP {(int)statusCode} {reasonPhrase}",
                        MitgliedId: mitgliedId);
                }

                if ((int)statusCode < 200 || (int)statusCode >= 300)
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<DeleteUserAccountResultDto>(body, JsonOpts);
                        if (parsed != null)
                        {
                            if (string.IsNullOrWhiteSpace(parsed.Outcome))
                            {
                                _logger?.LogWarning("Unexpected kgv-delete-user error response (missing outcome). HTTP {StatusCode} {ReasonPhrase} Body: {Body}", (int)statusCode, reasonPhrase, body);
                                Debug.WriteLine($"[kgv-delete-user] ErrorDTO missing outcome. DTO.Message='{parsed.Message}' AuthUserId='{parsed.AuthUserId}' ExtraKeys={parsed.ExtraKeys}");
                                return new DeleteUserAccountResult(
                                    DeleteUserAccountOutcome.Error,
                                    $"HTTP {(int)statusCode} {reasonPhrase} | Unerwartete Antwort vom Server.",
                                    MitgliedId: mitgliedId);
                            }

                            Debug.WriteLine($"[kgv-delete-user] ErrorDTO outcome='{parsed.Outcome}' message='{parsed.Message}' authUserId='{parsed.AuthUserId}' ExtraKeys={parsed.ExtraKeys}");
                            var parsedModel = parsed.ToModel() with { MitgliedId = mitgliedId };
                            return parsedModel with
                            {
                                Message = $"HTTP {(int)statusCode} {reasonPhrase}: {parsedModel.Message} | Body: {body}"
                            };
                        }
                    }
                    catch
                    {
                    }

                    return new DeleteUserAccountResult(
                        DeleteUserAccountOutcome.Error,
                        $"HTTP {(int)statusCode} {reasonPhrase} | Body: {body}",
                        MitgliedId: mitgliedId);
                }

                var dto = JsonSerializer.Deserialize<DeleteUserAccountResultDto>(body, JsonOpts);
                if (dto == null)
                    return new DeleteUserAccountResult(DeleteUserAccountOutcome.Error, "Unerwartete Antwort vom Server.", MitgliedId: mitgliedId);

                // Schutz: Contract muss mindestens `outcome` enthalten. Andernfalls nicht blind `message` in der UI anzeigen.
                if (string.IsNullOrWhiteSpace(dto.Outcome))
                {
                    _logger?.LogWarning("Unexpected kgv-delete-user response (missing outcome). Body: {Body}", body);
                    Debug.WriteLine($"[kgv-delete-user] DTO missing outcome. DTO.Message='{dto.Message}' AuthUserId='{dto.AuthUserId}' ExtraKeys={dto.ExtraKeys}");
                    return new DeleteUserAccountResult(DeleteUserAccountOutcome.Error, "Unerwartete Antwort vom Server.", MitgliedId: mitgliedId);
                }

                Debug.WriteLine($"[kgv-delete-user] DTO outcome='{dto.Outcome}' message='{dto.Message}' authUserId='{dto.AuthUserId}' ExtraKeys={dto.ExtraKeys}");
                var model = dto.ToModel();
                return model with { MitgliedId = mitgliedId };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "DeleteUserAccountForMitgliedAsync failed");
                return new DeleteUserAccountResult(DeleteUserAccountOutcome.Error, $"Fehler: {ex.Message}", MitgliedId: mitgliedId);
            }
        }

        private async Task<string?> TryGetCurrentAccessTokenAsync()
        {
            try
            {
                await _authService.EnsureValidSessionAsync(forceRefresh: false);

                var token = _client?.Auth?.CurrentSession?.AccessToken;
                if (!string.IsNullOrWhiteSpace(token))
                    return token;

                // Fallback: je nach Supabase .NET Version existieren unterschiedliche Session-APIs.
                // Wir versuchen defensiv, die Session zu holen/zu refreshen, ohne harte Compile-Abhängigkeit.
                if (_client?.Auth == null)
                    return null;

                try
                {
                    await _authService.EnsureValidSessionAsync(forceRefresh: true);
                    token = _client?.Auth?.CurrentSession?.AccessToken;
                    if (!string.IsNullOrWhiteSpace(token))
                        return token;
                }
                catch
                {
                }

                // Fallback: falls refresh nicht verfügbar/fehlschlägt, evtl. Session noch einmal holen
                try
                {
                    dynamic auth = _client.Auth;
                    dynamic session = await auth.RetrieveSessionAsync();
                    token = session?.AccessToken;
                    if (!string.IsNullOrWhiteSpace(token))
                        return token;
                }
                catch
                {
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private sealed class InviteUserAccountResultDto
        {
            public string? Outcome { get; set; }
            public string? Message { get; set; }
            public string? AuthUserId { get; set; }

            public InviteUserAccountResult ToModel()
            {
                var outcome = (Outcome ?? string.Empty).Trim().ToLowerInvariant();
                var mapped = outcome switch
                {
                    "invited" => InviteUserAccountOutcome.Invited,
                    "already_linked" => InviteUserAccountOutcome.AlreadyLinked,
                    "missing_email" => InviteUserAccountOutcome.MissingEmail,
                    "not_found" => InviteUserAccountOutcome.NotFound,
                    "unauthorized" => InviteUserAccountOutcome.Unauthorized,
                    "user_already_exists" => InviteUserAccountOutcome.UserAlreadyExists,
                    "invalid_role" => InviteUserAccountOutcome.InvalidRole,
                    _ => InviteUserAccountOutcome.Error
                };

                Guid? authUserId = null;
                if (!string.IsNullOrWhiteSpace(AuthUserId) && Guid.TryParse(AuthUserId, out var g))
                    authUserId = g;

                return new InviteUserAccountResult(mapped, Message ?? string.Empty, authUserId);
            }
        }

        private sealed class DeleteUserAccountResultDto
        {
            public string? Outcome { get; set; }
            public string? Message { get; set; }
            public string? AuthUserId { get; set; }

            [JsonExtensionData]
            public Dictionary<string, JsonElement>? Extra { get; set; }

            [JsonIgnore]
            public string ExtraKeys => Extra == null || Extra.Count == 0 ? "(none)" : string.Join(",", Extra.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

            public DeleteUserAccountResult ToModel()
            {
                var outcome = (Outcome ?? string.Empty).Trim().ToLowerInvariant();
                var mapped = outcome switch
                {
                    "deleted" => DeleteUserAccountOutcome.Deleted,
                    "no_user_account" => DeleteUserAccountOutcome.NoUserAccount,
                    "not_found" => DeleteUserAccountOutcome.NotFound,
                    "unauthorized" => DeleteUserAccountOutcome.Unauthorized,
                    _ => DeleteUserAccountOutcome.Error
                };

                Guid? authUserId = null;
                if (!string.IsNullOrWhiteSpace(AuthUserId) && Guid.TryParse(AuthUserId, out var g))
                    authUserId = g;

                return new DeleteUserAccountResult(mapped, Message ?? string.Empty, authUserId);
            }
        }

        private bool IsRestrictedToOwnMember(out int mitgliedId)
        {
            mitgliedId = 0;
            var ctx = _userContextAccessor?.Invoke();
            if (ctx == null) return false;
            if (!ctx.Has(PermissionFlags.CanSeeOwnDataOnly)) return false;

            if (!ctx.MitgliedId.HasValue) return true;
            if (ctx.MitgliedId.Value > int.MaxValue) return true;

            mitgliedId = (int)ctx.MitgliedId.Value;
            return true;
        }

        public async Task<bool> DeleteArbeitsstundeAsync(int arbeitsstundeId)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                await _client
                    .From<ArbeitsstundeRecord>()
                    .Where(a => a.Id == arbeitsstundeId)
                    .Delete();

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "DeleteArbeitsstundeAsync failed");
                return false;
            }
        }

        // =========================
        // Startseite (Lesen)
        // =========================
        public async Task<List<StartseiteBekanntmachungRecord>> GetStartseiteBekanntmachungenAsync()
        {
            var list = new List<StartseiteBekanntmachungRecord>();

            try
            {
                await InitializeAsync();
                if (_client == null) return list;

                var resp = await _client
                    .From<StartseiteBekanntmachungRecord>()
                    .Get();

                if (resp?.Models == null) return list;

                foreach (var r in resp.Models
                             .Where(x => x != null)
                             .OrderBy(x => x.SortOrder ?? int.MaxValue)
                             .ThenByDescending(x => x.SichtbarAb ?? DateTime.MinValue))
                {
                    list.Add(new StartseiteBekanntmachungRecord
                    {
                        Id = r.Id,
                        Titel = (r.Titel ?? string.Empty).Trim(),
                        InhaltHtml = r.InhaltHtml ?? string.Empty,
                        SichtbarAb = r.SichtbarAb,
                        SichtbarBis = r.SichtbarBis,
                        SortOrder = r.SortOrder
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetStartseiteBekanntmachungenAsync failed");
            }

            return list;
        }

        // =========================
        // Startseite (Verwaltung)
        // =========================
        public async Task<StartseiteBekanntmachungRecord?> SaveStartseiteBekanntmachungAsync(StartseiteBekanntmachungRecord record)
        {
            await InitializeAsync();
            if (_client == null) return null;
            if (record == null) return null;

            try
            {
                var write = new StartseiteBekanntmachungWriteRecord
                {
                    Id = record.Id > 0 ? record.Id : null,
                    Titel = record.Titel,
                    InhaltHtml = record.InhaltHtml,
                    SichtbarAb = record.SichtbarAb,
                    SichtbarBis = record.SichtbarBis,
                    SortOrder = record.SortOrder
                };

                long id;

                if (record.Id > 0)
                {
                    var resp = await _client
                        .From<StartseiteBekanntmachungWriteRecord>()
                        .Where(x => x.Id == (long?)record.Id)
                        .Update(write);

                    var updated = resp?.Models?.FirstOrDefault();
                    if (updated == null)
                        throw new InvalidOperationException("Speichern fehlgeschlagen (kein Datensatz zurückgegeben).");

                    id = updated.Id ?? 0;
                }
                else
                {
                    var insertResp = await _client
                        .From<StartseiteBekanntmachungWriteRecord>()
                        .Insert(write);

                    var inserted = insertResp?.Models?.FirstOrDefault();
                    if (inserted == null)
                        throw new InvalidOperationException("Speichern fehlgeschlagen (kein Datensatz zurückgegeben).");

                    id = inserted.Id ?? 0;
                }

                if (id <= 0)
                    throw new InvalidOperationException("Speichern fehlgeschlagen (keine ID zurückgegeben). Prüfe DB-ID-Erzeugung (Identity/Sequence/Trigger).");

                return await _client
                    .From<StartseiteBekanntmachungRecord>()
                    .Where(x => x.Id == id)
                    .Single();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "SaveStartseiteBekanntmachungAsync failed");
                throw new InvalidOperationException(BuildUserFacingSaveError(ex), ex);
            }
        }

        public async Task<StartseiteTerminRecord?> SaveStartseiteTerminAsync(StartseiteTerminRecord record)
        {
            await InitializeAsync();
            if (_client == null) return null;
            if (record == null) return null;

            try
            {
                // DB erwartet time-Felder -> nur gültiges HH:mm oder null senden (keine leeren Strings / Escape-Zeichen)
                var startRaw = (record.StartUhrzeit ?? string.Empty).Trim();
                if (!TimeText.TryNormalize(startRaw, out var startNorm))
                    startNorm = null;

                var endRaw = (record.EndUhrzeit ?? string.Empty).Trim();
                if (!TimeText.TryNormalize(endRaw, out var endNorm))
                    endNorm = null;

                var write = new StartseiteTerminWriteRecord
                {
                    Id = record.Id > 0 ? record.Id : null,
                    Titel = record.Titel,
                    Beschreibung = record.Beschreibung,
                    Datum = record.Datum,
                    StartUhrzeit = startNorm,
                    EndUhrzeit = endNorm,
                    SichtbarAb = record.SichtbarAb,
                    SichtbarBis = record.SichtbarBis
                };

                long id;

                if (record.Id > 0)
                {
                    var resp = await _client
                        .From<StartseiteTerminWriteRecord>()
                        .Where(x => x.Id == (long?)record.Id)
                        .Update(write);

                    var updated = resp?.Models?.FirstOrDefault();
                    if (updated == null)
                        throw new InvalidOperationException("Speichern fehlgeschlagen (kein Datensatz zurückgegeben).");

                    id = updated.Id ?? 0;
                }
                else
                {
                    var insertResp = await _client
                        .From<StartseiteTerminWriteRecord>()
                        .Insert(write);

                    var inserted = insertResp?.Models?.FirstOrDefault();
                    if (inserted == null)
                        throw new InvalidOperationException("Speichern fehlgeschlagen (kein Datensatz zurückgegeben).");

                    id = inserted.Id ?? 0;
                }

                if (id <= 0)
                    throw new InvalidOperationException("Speichern fehlgeschlagen (keine ID zurückgegeben). Prüfe DB-ID-Erzeugung (Identity/Sequence/Trigger).");

                return await _client
                    .From<StartseiteTerminRecord>()
                    .Where(x => x.Id == id)
                    .Single();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "SaveStartseiteTerminAsync failed");
                throw new InvalidOperationException(BuildUserFacingSaveError(ex), ex);
            }
        }

        public async Task<StartseiteArbeitseinsatzRecord?> SaveStartseiteArbeitseinsatzAsync(StartseiteArbeitseinsatzRecord record)
        {
            await InitializeAsync();
            if (_client == null) return null;
            if (record == null) return null;

            try
            {
                // DB erwartet time-Felder -> nur gültiges HH:mm oder null senden (keine leeren Strings)
                var startRaw = (record.StartUhrzeit ?? string.Empty).Trim();
                if (!TimeText.TryNormalize(startRaw, out var startNorm))
                    startNorm = null;

                var endRaw = (record.EndUhrzeit ?? string.Empty).Trim();
                if (!TimeText.TryNormalize(endRaw, out var endNorm))
                    endNorm = null;

                var write = new StartseiteArbeitseinsatzWriteRecord
                {
                    Id = record.Id > 0 ? record.Id : null,
                    Titel = record.Titel,
                    Beschreibung = record.Beschreibung,
                    Datum = record.Datum,
                    StartUhrzeit = startNorm,
                    EndUhrzeit = endNorm,
                    Treffpunkt = record.Treffpunkt,
                    MaxTeilnehmer = record.MaxTeilnehmer,
                    StundenWert = record.StundenWert ?? 0m,
                    SichtbarAb = record.SichtbarAb,
                    SichtbarBis = record.SichtbarBis,
                    AnmeldungBis = record.AnmeldungBis
                };

                long id;

                if (record.Id > 0)
                {
                    var resp = await _client
                        .From<StartseiteArbeitseinsatzWriteRecord>()
                        .Where(x => x.Id == (long?)record.Id)
                        .Update(write);

                    var updated = resp?.Models?.FirstOrDefault();
                    if (updated == null)
                        throw new InvalidOperationException("Speichern fehlgeschlagen (kein Datensatz zurückgegeben).");

                    id = updated.Id ?? 0;
                }
                else
                {
                    var insertResp = await _client
                        .From<StartseiteArbeitseinsatzWriteRecord>()
                        .Insert(write);

                    var inserted = insertResp?.Models?.FirstOrDefault();
                    if (inserted == null)
                        throw new InvalidOperationException("Speichern fehlgeschlagen (kein Datensatz zurückgegeben).");

                    id = inserted.Id ?? 0;
                }

                if (id <= 0)
                    throw new InvalidOperationException("Speichern fehlgeschlagen (keine ID zurückgegeben). Prüfe DB-ID-Erzeugung (Identity/Sequence/Trigger).");

                // Re-load from the view, so computed/read-only columns (z.B. angemeldet_count) are correct.
                return await _client
                    .From<StartseiteArbeitseinsatzRecord>()
                    .Where(x => x.Id == id)
                    .Single();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "SaveStartseiteArbeitseinsatzAsync failed");
                throw new InvalidOperationException(BuildUserFacingSaveError(ex), ex);
            }
        }

        public async Task<bool> DeleteStartseiteBekanntmachungAsync(long id)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;
                if (id <= 0) return false;

                await _client
                    .From<StartseiteBekanntmachungWriteRecord>()
                    .Where(x => x.Id == (long?)id)
                    .Delete();

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "DeleteStartseiteBekanntmachungAsync failed");
                return false;
            }
        }

        public async Task<bool> DeleteStartseiteTerminAsync(long id)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;
                if (id <= 0) return false;

                await _client
                    .From<StartseiteTerminWriteRecord>()
                    .Where(x => x.Id == (long?)id)
                    .Delete();

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "DeleteStartseiteTerminAsync failed");
                return false;
            }
        }

        public async Task<bool> DeleteStartseiteArbeitseinsatzAsync(long id)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;
                if (id <= 0) return false;

                await _client
                    .From<StartseiteArbeitseinsatzWriteRecord>()
                    .Where(x => x.Id == (long?)id)
                    .Delete();

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "DeleteStartseiteArbeitseinsatzAsync failed");
                return false;
            }
        }

        private static string BuildUserFacingSaveError(Exception ex)
        {
            try
            {
                var msg = (ex.Message ?? string.Empty).Trim();

                // Häufige Postgres-Fehler (fachlich lesbar, ohne JSON-Rohpayload)
                if (msg.Contains("invalid input syntax for type time", StringComparison.OrdinalIgnoreCase))
                    return "Uhrzeit ist ungültig. Bitte HH:mm angeben oder Feld leer lassen.";

                if (msg.Contains("duplicate key value violates unique constraint", StringComparison.OrdinalIgnoreCase)
                    || msg.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                    return "Speichern fehlgeschlagen (ID-Konflikt). Bitte erneut versuchen.";

                // Falls das Exception-Objekt ein JSON-Content enthält, nicht direkt ins UI geben.
                var contentProp = ex.GetType().GetProperty("Content");
                var content = (contentProp?.GetValue(ex) as string ?? string.Empty).Trim();
                if (content.StartsWith("{", StringComparison.Ordinal) || content.StartsWith("[", StringComparison.Ordinal))
                    content = string.Empty;

                // Wenn keine klar map-bare Ursache, defensiv nur eine kurze Message durchreichen.
                // (Verhindert, dass komplette JSON/Stack-Infos in der UI landen.)
                if (string.IsNullOrWhiteSpace(msg))
                    return "Speichern fehlgeschlagen.";

                // Einzeilige Messages sind i.d.R. ok, Multi-Line eher nicht.
                if (msg.Contains('\n') || msg.Contains('\r'))
                    return "Speichern fehlgeschlagen.";

                return msg;
            }
            catch
            {
                return "Speichern fehlgeschlagen.";
            }
        }

        public async Task<List<StartseiteTerminRecord>> GetStartseiteTermineAsync()
        {
            var list = new List<StartseiteTerminRecord>();

            try
            {
                await InitializeAsync();
                if (_client == null) return list;

                var resp = await _client
                    .From<StartseiteTerminRecord>()
                    .Get();

                if (resp?.Models == null) return list;

                foreach (var r in resp.Models
                             .Where(x => x != null)
                             .OrderBy(x => x.Datum ?? DateTime.MaxValue)
                             .ThenBy(x => x.StartUhrzeit ?? string.Empty))
                {
                    list.Add(new StartseiteTerminRecord
                    {
                        Id = r.Id,
                        Titel = (r.Titel ?? string.Empty).Trim(),
                        Beschreibung = r.Beschreibung ?? string.Empty,
                        Datum = r.Datum,
                        StartUhrzeit = r.StartUhrzeit,
                        EndUhrzeit = r.EndUhrzeit,
                        SichtbarAb = r.SichtbarAb,
                        SichtbarBis = r.SichtbarBis
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetStartseiteTermineAsync failed");
            }

            return list;
        }

        public async Task<List<StartseiteArbeitseinsatzRecord>> GetStartseiteArbeitseinsaetzeAsync()
        {
            var list = new List<StartseiteArbeitseinsatzRecord>();

            try
            {
                await InitializeAsync();
                if (_client == null) return list;

                var resp = await _client
                    .From<StartseiteArbeitseinsatzRecord>()
                    .Get();

                if (resp?.Models == null) return list;

                foreach (var r in resp.Models
                             .Where(x => x != null)
                             .OrderBy(x => x.Datum ?? DateTime.MaxValue)
                             .ThenBy(x => x.StartUhrzeit ?? string.Empty))
                {
                    list.Add(new StartseiteArbeitseinsatzRecord
                    {
                        Id = r.Id,
                        Titel = (r.Titel ?? string.Empty).Trim(),
                        Beschreibung = r.Beschreibung ?? string.Empty,
                        Datum = r.Datum,
                        StartUhrzeit = r.StartUhrzeit,
                        EndUhrzeit = r.EndUhrzeit,
                        Treffpunkt = (r.Treffpunkt ?? string.Empty).Trim(),
                        MaxTeilnehmer = r.MaxTeilnehmer,
                        StundenWert = r.StundenWert,
                        SichtbarAb = r.SichtbarAb,
                        SichtbarBis = r.SichtbarBis,
                        AnmeldungBis = r.AnmeldungBis,
                        AngemeldetCount = r.AngemeldetCount ?? 0,
                        FreiePlaetze = r.FreiePlaetze
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetStartseiteArbeitseinsaetzeAsync failed");
            }

            return list;
        }

        public async Task<HashSet<long>> GetMyArbeitseinsatzAnmeldungenAsync()
        {
            var set = new HashSet<long>();

            try
            {
                await InitializeAsync();
                if (_client == null) return set;

                var ctx = _userContextAccessor?.Invoke();
                if (ctx?.MitgliedId == null || ctx.MitgliedId.Value <= 0 || ctx.MitgliedId.Value > int.MaxValue)
                    return set;

                var mitgliedId = (int)ctx.MitgliedId.Value;

                var resp = await _client
                    .From<ArbeitseinsatzAnmeldungRecord>()
                    .Where(x => x.MitgliedId == mitgliedId)
                    .Get();

                if (resp?.Models == null) return set;

                foreach (var r in resp.Models.Where(x => x != null))
                {
                    if (r.ArbeitseinsatzId > 0)
                        set.Add(r.ArbeitseinsatzId);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetMyArbeitseinsatzAnmeldungenAsync failed");
            }

            return set;
        }

        public Task<bool> SignUpForArbeitseinsatzAsync(long arbeitseinsatzId)
            => CallArbeitseinsatzRpcAsync("sign_up_for_arbeitseinsatz", arbeitseinsatzId);

        public Task<bool> SignOffFromArbeitseinsatzAsync(long arbeitseinsatzId)
            => CallArbeitseinsatzRpcAsync("sign_off_from_arbeitseinsatz", arbeitseinsatzId);

        // =========================
        // Impressum (Funktionsslots)
        // =========================
        public async Task<List<ImpressumFunktionSlotRecord>> GetImpressumFunktionSlotsAsync()
        {
            var list = new List<ImpressumFunktionSlotRecord>();

            try
            {
                await InitializeAsync();
                if (_client == null) return list;

                var resp = await _client
                    .From<ImpressumFunktionSlotRecord>()
                    .Get();

                if (resp?.Models != null)
                    list.AddRange(resp.Models.Where(x => x != null));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetImpressumFunktionSlotsAsync failed");
            }

            return list;
        }

        public async Task<bool> SaveImpressumFunktionSlotsAsync(IEnumerable<ImpressumFunktionSlotRecord> slots)
        {
            await InitializeAsync();
            if (_client == null) return false;
            if (slots == null) return false;

            try
            {
                foreach (var slot in slots.Where(x => x != null))
                {
                    var write = new ImpressumFunktionSlotRecord
                    {
                        Id = slot.Id,
                        SlotKey = slot.SlotKey,
                        Funktion = slot.Funktion,
                        SortOrder = slot.SortOrder,
                        MitgliedId = slot.MitgliedId
                    };

                    long id;

                    if (slot.Id > 0)
                    {
                        var resp = await _client
                            .From<ImpressumFunktionSlotRecord>()
                            .Where(x => x.Id == slot.Id)
                            .Update(write);

                        var updated = resp?.Models?.FirstOrDefault();
                        if (updated == null)
                            throw new InvalidOperationException("Speichern fehlgeschlagen (kein Datensatz zurückgegeben).");

                        id = updated.Id;
                    }
                    else
                    {
                        var insertResp = await _client
                            .From<ImpressumFunktionSlotRecord>()
                            .Insert(write);

                        var inserted = insertResp?.Models?.FirstOrDefault();
                        if (inserted == null)
                            throw new InvalidOperationException("Speichern fehlgeschlagen (kein Datensatz zurückgegeben).");

                        id = inserted.Id;
                    }

                    if (id <= 0)
                        throw new InvalidOperationException("Speichern fehlgeschlagen (keine ID zurückgegeben). Prüfe DB-ID-Erzeugung (Identity/Sequence/Trigger).");

                    slot.Id = id;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "SaveImpressumFunktionSlotsAsync failed");
                throw new InvalidOperationException(BuildUserFacingSaveError(ex), ex);
            }
        }

        public async Task<RfidScanContextRecord?> GetRfidScanContextAsync(string rfidTagUid)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return null;

                rfidTagUid = (rfidTagUid ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(rfidTagUid))
                    return null;

                var resp = await _client
                    .From<RfidScanContextRecord>()
                    .Where(x => x.RfidTagUid == rfidTagUid)
                    .Limit(1)
                    .Get();

                return resp?.Models?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetRfidScanContextAsync failed");
                return null;
            }
        }

        private async Task<bool> CallArbeitseinsatzRpcAsync(string functionName, long arbeitseinsatzId)
        {
            if (string.IsNullOrWhiteSpace(functionName))
                return false;

            if (arbeitseinsatzId <= 0)
                return false;

            // Parameternamen sind DB-seitig nicht garantiert (p_... vs. ...).
            // Wir versuchen defensiv 2 übliche Varianten.
            var payloads = new object[]
            {
                new Dictionary<string, object> { ["arbeitseinsatz_id"] = arbeitseinsatzId },
                new Dictionary<string, object> { ["p_arbeitseinsatz_id"] = arbeitseinsatzId }
            };

            foreach (var payload in payloads)
            {
                var (ok, shouldRetryWithOtherPayload) = await TryPostRpcAsync(functionName, payload);
                if (ok)
                    return true;

                if (!shouldRetryWithOtherPayload)
                    return false;
            }

            return false;
        }

        private async Task<(bool Ok, bool RetryWithOtherPayload)> TryPostRpcAsync(string functionName, object payload)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return (false, false);

                _http ??= new HttpClient();

                var token = await TryGetCurrentAccessTokenAsync();
                if (string.IsNullOrWhiteSpace(token))
                    return (false, false);

                var url = _clientFactory.Url.TrimEnd('/') + "/rest/v1/rpc/" + functionName;

                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var anonKey = _clientFactory.Key;
                if (!string.IsNullOrWhiteSpace(anonKey))
                    req.Headers.Add("apikey", anonKey);

                req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                using var resp = await _http.SendAsync(req);
                var body = await resp.Content.ReadAsStringAsync();

                if (resp.IsSuccessStatusCode)
                    return (true, false);

                // Häufig: falscher Parametername -> 400 mit Hinweis auf Argument/Parameter.
                if ((int)resp.StatusCode == 400 && !string.IsNullOrWhiteSpace(body)
                    && (body.Contains("parameter", StringComparison.OrdinalIgnoreCase)
                        || body.Contains("argument", StringComparison.OrdinalIgnoreCase)
                        || body.Contains("function", StringComparison.OrdinalIgnoreCase)))
                {
                    return (false, true);
                }

                // Invalid JWT: einmal Refresh und Retry mit gleichem Payload
                if ((resp.StatusCode == System.Net.HttpStatusCode.Unauthorized || resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    && !string.IsNullOrWhiteSpace(body)
                    && body.Contains("Invalid JWT", StringComparison.OrdinalIgnoreCase))
                {
                    await _authService.EnsureValidSessionAsync(forceRefresh: true);
                    var token2 = await TryGetCurrentAccessTokenAsync();
                    if (string.IsNullOrWhiteSpace(token2) || string.Equals(token2, token, StringComparison.Ordinal))
                        return (false, false);

                    using var req2 = new HttpRequestMessage(HttpMethod.Post, url);
                    req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token2);
                    req2.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    if (!string.IsNullOrWhiteSpace(anonKey))
                        req2.Headers.Add("apikey", anonKey);
                    req2.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                    using var resp2 = await _http.SendAsync(req2);
                    return (resp2.IsSuccessStatusCode, false);
                }

                _logger?.LogWarning("RPC {FunctionName} failed. HTTP {Status}. Body: {Body}", functionName, (int)resp.StatusCode, body);
                return (false, false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RPC {FunctionName} failed", functionName);
                return (false, false);
            }
        }

        // =========================
        // Nebenmitglied
        // =========================
        public async Task<MitgliedRecord?> GetNebenmitgliedByHauptmitgliedIdAsync(int hauptmitgliedId)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return null;

                var resp = await _client
                    .From<MitgliedRecord>()
                    .Where(m => m.HauptmitgliedId == hauptmitgliedId)
                    .Get();

                var one = resp?.Models?.FirstOrDefault();
                if (one != null)
                    await TryApplyAppUserRoleAsync(one);

                return one;
            }
            catch
            {
                return null;
            }
        }

        public async Task<MitgliedRecord?> CreateNebenmitgliedAsync(int hauptmitgliedId, string vorname, string nachname, bool adresseUebernehmen)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return null;

                if (hauptmitgliedId <= 0)
                    throw new InvalidOperationException("HauptmitgliedId fehlt.");

                if (string.IsNullOrWhiteSpace(vorname) || string.IsNullOrWhiteSpace(nachname))
                    throw new InvalidOperationException("Vorname/Nachname fehlen.");

                vorname = vorname.Trim();
                nachname = nachname.Trim();

                var main = await _client
                    .From<MitgliedRecord>()
                    .Where(m => m.Id == hauptmitgliedId)
                    .Single();

                if (main == null) return null;

                var rec = new MitgliedRecord
                {
                    Vorname = vorname,
                    Name = nachname,
                    HauptmitgliedId = hauptmitgliedId,
                    WhatsappEinwilligung = false,
                    EmailInfoEinwilligung = false,
                    EmailRechnungEinwilligung = false,
                    Aktiv = true,
                    Role = "user",
                    MitgliedSeit = DateTime.SpecifyKind(DateTime.Today.AddHours(12), DateTimeKind.Unspecified)
                };

                if (adresseUebernehmen)
                {
                    rec.Adresse = main.Adresse;
                    rec.Plz = main.Plz;
                    rec.Ort = main.Ort;
                    rec.Telefon = main.Telefon;
                    rec.Handy = main.Handy;
                }

                var insertResp = await _client.From<MitgliedRecord>().Insert(rec);

                var created = insertResp?.Models?.FirstOrDefault();
                if (created == null)
                    throw new InvalidOperationException("Nebenmitglied konnte nicht angelegt werden (Insert lieferte keinen Datensatz zurück).");

                return created;
            }
            catch (PostgrestException pex)
            {
                _logger?.LogError(pex, "CreateNebenmitgliedAsync failed: {Message}", pex.Message);
                TryAppendErrorLog("CreateNebenmitgliedAsync", pex);
                throw new InvalidOperationException(BuildUserFacingSaveError(pex), pex);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "CreateNebenmitgliedAsync failed");
                TryAppendErrorLog("CreateNebenmitgliedAsync", ex);
                throw new InvalidOperationException("Nebenmitglied konnte nicht angelegt werden.", ex);
            }
        }

        // =========================
        // Arbeitsstunden
        // =========================
        public async Task<List<SaisonRecord>> GetSaisonRecordsAsync()
        {
            var list = new List<SaisonRecord>();
            try
            {
                await InitializeAsync();
                if (_client == null) return list;

                var resp = await _client.From<SaisonRecord>().Get();
                if (resp?.Models != null) list.AddRange(resp.Models);

                return list.OrderByDescending(x => x.Jahr).ToList();
            }
            catch
            {
                return list;
            }
        }

        public async Task<MitgliedRecord?> GetMitgliedByAuthUserIdAsync(string authUserId)
        {
            try
            {
                if (!Guid.TryParse(authUserId, out var guid))
                    return null;

                return await GetMitgliedByAuthUserIdAsync(guid);
            }
            catch
            {
                return null;
            }
        }

        public async Task<MitgliedRecord?> GetMitgliedByAuthUserIdAsync(Guid authUserId)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return null;
                if (authUserId == Guid.Empty) return null;

                var resp = await _client
                    .From<MitgliedRecord>()
                    .Where(m => m.AuthUserId == authUserId)
                    .Get();

                var one = resp?.Models?.FirstOrDefault();
                if (one != null)
                    await TryApplyAppUserRoleAsync(one);

                return one;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> UpdateOwnContactAsync(int mitgliedId, string? telefon, string? handy, string? adresse, string? plz, string? ort)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                if (IsRestrictedToOwnMember(out var ownId))
                {
                    if (ownId <= 0) return false;
                    if (mitgliedId != ownId)
                    {
                        // Erlaube im Own-only Modus auch das Nebenmitglied des eigenen Hauptmitglieds.
                        var target = await _client
                            .From<MitgliedRecord>()
                            .Where(m => m.Id == mitgliedId)
                            .Single();

                        if (target?.HauptmitgliedId != ownId)
                        {
                            _logger?.LogWarning("Denied UpdateOwnContactAsync for MitgliedId {MitgliedId} (own-only mode; own MitgliedId {OwnId})", mitgliedId, ownId);
                            return false;
                        }
                    }
                }

                var record = await _client
                    .From<MitgliedRecord>()
                    .Where(m => m.Id == mitgliedId)
                    .Single();

                if (record == null) return false;

                record.Telefon = telefon;
                record.Handy = handy;
                record.Adresse = adresse;
                record.Plz = plz;
                record.Ort = ort;

                await _client
                    .From<MitgliedRecord>()
                    .Where(m => m.Id == mitgliedId)
                    .Update(record);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "UpdateOwnContactAsync failed");
                return false;
            }
        }

        public async Task<List<ArbeitsstundeDTO>> GetArbeitsstundenAsync(params int[] mitgliedIds)
        {
            var result = new List<ArbeitsstundeDTO>();
            try
            {
                await InitializeAsync();
                if (_client == null) return result;

                var ids = (mitgliedIds ?? Array.Empty<int>()).Distinct().ToArray();

                if (IsRestrictedToOwnMember(out var ownId))
                {
                    if (ownId <= 0) return result;

                    // Own-only Mode: erlaube zusätzlich Nebenmitglied (falls vorhanden)
                    var allowed = new List<int> { ownId };
                    try
                    {
                        var neben = await GetNebenmitgliedByHauptmitgliedIdAsync(ownId);
                        if (neben != null)
                            allowed.Add(neben.Id);
                    }
                    catch
                    {
                    }

                    ids = allowed.Distinct().ToArray();
                }
                if (ids.Length == 0) return result;

                var arbeitsResp = await _client.From<ArbeitsstundeRecord>().Get();
                var arbeits = arbeitsResp?.Models?.Where(a => ids.Contains(a.MitgliedId)).ToList() ?? new List<ArbeitsstundeRecord>();

                var saisonen = await GetSaisonRecordsAsync();
                var saisonById = saisonen.ToDictionary(x => x.Id, x => x.Jahr);

                var mitgliederResp = await _client.From<MitgliedRecord>().Get();
                var mitglieder = mitgliederResp?.Models?.ToList() ?? new List<MitgliedRecord>();
                var mitgliedById = mitglieder.ToDictionary(m => m.Id, m => m);

                foreach (var a in arbeits)
                {
                    mitgliedById.TryGetValue(a.MitgliedId, out var m);

                    string? genehmigtVonName = null;
                    if (a.GenehmigtVon.HasValue && mitgliedById.TryGetValue(a.GenehmigtVon.Value, out var gv))
                        genehmigtVonName = $"{gv.Name} {gv.Vorname}".Trim();

                    result.Add(new ArbeitsstundeDTO
                    {
                        Id = a.Id,
                        MitgliedId = a.MitgliedId,
                        Vorname = m?.Vorname ?? string.Empty,
                        Nachname = m?.Name ?? string.Empty,
                        Datum = a.Datum.Date,
                        SaisonId = a.SaisonId,
                        SaisonJahr = saisonById.TryGetValue(a.SaisonId, out var jahr) ? jahr : 0,
                        Stunden = a.Stunden,
                        Beschreibung = a.ArtDerArbeit,
                        Status = a.Status,
                        Freigegeben = a.Freigegeben,
                        FreigegebenAm = a.GenehmigtAm,
                        FreigegebenVonId = a.GenehmigtVon,
                        FreigegebenVonName = genehmigtVonName
                    });
                }

                return result
                    .OrderByDescending(x => x.Datum)
                    .ThenBy(x => x.Nachname)
                    .ThenBy(x => x.Vorname)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetArbeitsstundenAsync failed");
                return result;
            }
        }

        public async Task<bool> AddArbeitsstundeAsync(ArbeitsstundeRecord record)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;
                await _client.From<ArbeitsstundeRecord>().Insert(record);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "AddArbeitsstundeAsync failed");
                return false;
            }
        }

        public async Task<bool> UpdateArbeitsstundeAsync(ArbeitsstundeRecord record)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                var rec = await _client
                    .From<ArbeitsstundeRecord>()
                    .Where(a => a.Id == record.Id)
                    .Single();

                if (rec == null) return false;

                rec.MitgliedId = record.MitgliedId;
                rec.SaisonId = record.SaisonId;
                rec.Datum = record.Datum.Date;
                rec.Stunden = record.Stunden;
                rec.ArtDerArbeit = record.ArtDerArbeit;
                rec.Status = record.Status;
                rec.Freigegeben = record.Freigegeben;
                rec.GenehmigtAm = record.GenehmigtAm;
                rec.GenehmigtVon = record.GenehmigtVon;

                await _client
                    .From<ArbeitsstundeRecord>()
                    .Where(a => a.Id == record.Id)
                    .Update(rec);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "UpdateArbeitsstundeAsync failed");
                return false;
            }
        }

        public async Task<List<(int MitgliedId, string Vorname, string Nachname, int Count)>> GetUnapprovedArbeitsstundenByMitgliedAsync()
        {
            var result = new List<(int MitgliedId, string Vorname, string Nachname, int Count)>();
            try
            {
                await InitializeAsync();
                if (_client == null) return result;

                if (IsRestrictedToOwnMember(out _))
                    return result;

                var arbeitsResp = await _client.From<ArbeitsstundeRecord>().Get();
                var unapproved = arbeitsResp?.Models?.Where(a => !a.Freigegeben).ToList() ?? new List<ArbeitsstundeRecord>();
                if (unapproved.Count == 0) return result;

                var mitgliederResp = await _client.From<MitgliedRecord>().Get();
                var mitglieder = mitgliederResp?.Models?.ToList() ?? new List<MitgliedRecord>();
                var mitgliedById = mitglieder.ToDictionary(m => m.Id, m => m);

                foreach (var g in unapproved.GroupBy(x => x.MitgliedId))
                {
                    mitgliedById.TryGetValue(g.Key, out var m);
                    result.Add((g.Key, m?.Vorname ?? string.Empty, m?.Name ?? string.Empty, g.Count()));
                }

                return result
                    .OrderBy(x => x.Nachname)
                    .ThenBy(x => x.Vorname)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetUnapprovedArbeitsstundenByMitgliedAsync failed");
                return result;
            }
        }

        public async Task<List<ZaehlerAblesungDTO>> GetStromAblesungenAsync(int parzelleId)
        {
            var result = new List<ZaehlerAblesungDTO>();
            try
            {
                await InitializeAsync();
                if (_client == null) return result;

                var metersResp = await _client
                    .From<StromzaehlerRecord>()
                    .Where(x => x.ParzelleId == parzelleId)
                    .Get();

                var meters = metersResp?.Models?.ToList() ?? new List<StromzaehlerRecord>();
                if (meters.Count == 0) return result;

                foreach (var m in meters)
                {
                    var ablesResp = await _client
                        .From<AblesungRecord>()
                        .Where(a => a.ZaehlerTyp == ZaehlerTypStrom && a.ZaehlerId == m.Id)
                        .Get();

                    if (ablesResp?.Models == null) continue;

                    foreach (var a in ablesResp.Models)
                    {
                        result.Add(new ZaehlerAblesungDTO
                        {
                            AblesungId = a.Id,
                            ZaehlerId = a.ZaehlerId,
                            Ablesedatum = a.Ablesedatum,
                            Stand = a.Stand,
                            Zaehlernummer = m.Zaehlernummer,
                            Eichdatum = m.Eichdatum,
                            FotoPfad = a.FotoPfad
                        });
                    }
                }

                return result
                    .OrderByDescending(x => x.Ablesedatum)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetStromAblesungenAsync failed");
                return result;
            }
        }

        public async Task<List<ZaehlerAblesungDTO>> GetWasserAblesungenAsync(int parzelleId)
        {
            var result = new List<ZaehlerAblesungDTO>();
            try
            {
                await InitializeAsync();
                if (_client == null) return result;

                var metersResp = await _client
                    .From<WasserzaehlerRecord>()
                    .Where(x => x.ParzelleId == parzelleId)
                    .Get();

                var meters = metersResp?.Models?.ToList() ?? new List<WasserzaehlerRecord>();
                if (meters.Count == 0) return result;

                foreach (var m in meters)
                {
                    var ablesResp = await _client
                        .From<AblesungRecord>()
                        .Where(a => a.ZaehlerTyp == ZaehlerTypWasser && a.ZaehlerId == m.Id)
                        .Get();

                    if (ablesResp?.Models == null) continue;

                    foreach (var a in ablesResp.Models)
                    {
                        result.Add(new ZaehlerAblesungDTO
                        {
                            AblesungId = a.Id,
                            ZaehlerId = a.ZaehlerId,
                            Ablesedatum = a.Ablesedatum,
                            Stand = a.Stand,
                            Zaehlernummer = m.Zaehlernummer,
                            Eichdatum = m.Eichdatum,
                            FotoPfad = a.FotoPfad
                        });
                    }
                }

                return result
                    .OrderByDescending(x => x.Ablesedatum)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetWasserAblesungenAsync failed");
                return result;
            }
        }

        public async Task<StromzaehlerRecord?> GetActiveStromzaehlerAsync(int parzelleId, DateTime onDate)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return null;

                var resp = await _client
                    .From<StromzaehlerRecord>()
                    .Where(x => x.ParzelleId == parzelleId)
                    .Get();

                var meters = resp?.Models?.ToList() ?? new List<StromzaehlerRecord>();
                var d = onDate.Date;
                return meters
                    .Where(m => m.EingebautAm.Date <= d && (m.AusgebautAm == null || m.AusgebautAm.Value.Date >= d))
                    .OrderByDescending(m => m.EingebautAm)
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetActiveStromzaehlerAsync failed");
                return null;
            }
        }

        public async Task<WasserzaehlerRecord?> GetActiveWasserzaehlerAsync(int parzelleId, DateTime onDate)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return null;

                var resp = await _client
                    .From<WasserzaehlerRecord>()
                    .Where(x => x.ParzelleId == parzelleId)
                    .Get();

                var meters = resp?.Models?.ToList() ?? new List<WasserzaehlerRecord>();
                var d = onDate.Date;
                return meters
                    .Where(m => m.EingebautAm.Date <= d && (m.AusgebautAm == null || m.AusgebautAm.Value.Date >= d))
                    .OrderByDescending(m => m.EingebautAm)
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetActiveWasserzaehlerAsync failed");
                return null;
            }
        }

        public async Task<bool> AddStromzaehlerAsync(int parzelleId, string zaehlernummer, DateTime eichdatum, DateTime eingebautAm)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                var rec = new StromzaehlerRecord
                {
                    ParzelleId = parzelleId,
                    Zaehlernummer = zaehlernummer,
                    Eichdatum = eichdatum.Date,
                    EingebautAm = eingebautAm.Date,
                    AusgebautAm = null
                };

                await _client.From<StromzaehlerRecord>().Insert(rec);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "AddStromzaehlerAsync failed");
                return false;
            }
        }

        public async Task<bool> AddWasserzaehlerAsync(int parzelleId, string zaehlernummer, DateTime eichdatum, DateTime eingebautAm)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                var rec = new WasserzaehlerRecord
                {
                    ParzelleId = parzelleId,
                    Zaehlernummer = zaehlernummer,
                    Eichdatum = eichdatum.Date,
                    EingebautAm = eingebautAm.Date,
                    AusgebautAm = null
                };

                await _client.From<WasserzaehlerRecord>().Insert(rec);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "AddWasserzaehlerAsync failed");
                return false;
            }
        }

        public async Task<bool> SetStromzaehlerAusgebautAmAsync(long stromzaehlerId, DateTime ausgebautAm)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                var rec = await _client
                    .From<StromzaehlerRecord>()
                    .Where(x => x.Id == stromzaehlerId)
                    .Single();

                if (rec == null) return false;
                rec.AusgebautAm = ausgebautAm.Date;

                await _client
                    .From<StromzaehlerRecord>()
                    .Where(x => x.Id == stromzaehlerId)
                    .Update(rec);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "SetStromzaehlerAusgebautAmAsync failed");
                return false;
            }
        }

        public async Task<bool> SetWasserzaehlerAusgebautAmAsync(long wasserzaehlerId, DateTime ausgebautAm)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                var rec = await _client
                    .From<WasserzaehlerRecord>()
                    .Where(x => x.Id == wasserzaehlerId)
                    .Single();

                if (rec == null) return false;
                rec.AusgebautAm = ausgebautAm.Date;

                await _client
                    .From<WasserzaehlerRecord>()
                    .Where(x => x.Id == wasserzaehlerId)
                    .Update(rec);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "SetWasserzaehlerAusgebautAmAsync failed");
                return false;
            }
        }

        public async Task<bool> AddAblesungAsync(short zaehlerTyp, long zaehlerId, DateTime ablesedatum, decimal stand, string? fotoPfad)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                var rec = new AblesungRecord
                {
                    ZaehlerTyp = zaehlerTyp,
                    ZaehlerId = zaehlerId,
                    Ablesedatum = DateTime.SpecifyKind(ablesedatum, DateTimeKind.Unspecified),
                    Stand = stand,
                    FotoPfad = fotoPfad,
                    Freigegeben = false
                };

                await _client.From<AblesungRecord>().Insert(rec);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "AddAblesungAsync failed");
                return false;
            }
        }

        public async Task<SaveAblesungResult> AddAblesungResultAsync(short zaehlerTyp, long zaehlerId, DateTime ablesedatum, decimal stand, string? fotoPfad)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return SaveAblesungResult.Error("Supabase ist nicht initialisiert.");

                var rec = new AblesungRecord
                {
                    ZaehlerTyp = zaehlerTyp,
                    ZaehlerId = zaehlerId,
                    Ablesedatum = DateTime.SpecifyKind(ablesedatum, DateTimeKind.Unspecified),
                    Stand = stand,
                    FotoPfad = fotoPfad,
                    Freigegeben = false
                };

                await _client.From<AblesungRecord>().Insert(rec);
                return SaveAblesungResult.Success("Ablesung gespeichert.");
            }
            catch (PostgrestException pg)
            {
                _logger?.LogWarning(pg, "AddAblesungResultAsync failed");
                return SaveAblesungResult.Error(pg.Message);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "AddAblesungResultAsync failed");
                return SaveAblesungResult.Error(ex.Message);
            }
        }

        public async Task<bool> UpdateAblesungAsync(long ablesungId, DateTime ablesedatum, decimal stand, string? fotoPfad)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                var rec = await _client
                    .From<AblesungRecord>()
                    .Where(x => x.Id == ablesungId)
                    .Single();

                if (rec == null) return false;

                rec.Ablesedatum = DateTime.SpecifyKind(ablesedatum, DateTimeKind.Unspecified);
                rec.Stand = stand;
                rec.FotoPfad = fotoPfad;

                await _client
                    .From<AblesungRecord>()
                    .Where(x => x.Id == ablesungId)
                    .Update(rec);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "UpdateAblesungAsync failed");
                return false;
            }
        }

        public Client Client => _client ?? throw new InvalidOperationException("Client not initialized. Call InitializeAsync() first.");

        public async Task InitializeAsync()
        {
            if (_client == null)
            {
                _client = await _clientFactory.CreateAsync();
                _http ??= new HttpClient();
            }

            // Zentraler Punkt: vor geschützten Requests sicherstellen, dass die Session (JWT) noch gültig ist.
            // Damit gibt es genau eine Session-Quelle (AuthService) und keine Doppelzuständigkeiten.
            await _authService.EnsureValidSessionAsync(forceRefresh: false);
        }

        // =========================
        // Dokumente (Supabase Storage)
        // =========================
        public async Task<List<DocumentInfo>> GetMitgliedDokumenteAsync(int mitgliedId)
        {
            if (IsRestrictedToOwnMember(out var ownId))
            {
                if (ownId <= 0) return new List<DocumentInfo>();
                if (mitgliedId != ownId)
                {
                    _logger?.LogWarning("Denied GetMitgliedDokumenteAsync for MitgliedId {MitgliedId} (own-only mode; own MitgliedId {OwnId})", mitgliedId, ownId);
                    return new List<DocumentInfo>();
                }
            }

            return await ListDokumenteFromTableAsync(mitgliedId: mitgliedId, parzelleId: null);
        }

        public async Task<List<DocumentInfo>> GetParzelleDokumenteAsync(int parzelleId)
        {
            return await ListDokumenteFromTableAsync(mitgliedId: null, parzelleId: parzelleId);
        }

        public async Task<string?> CreateDokumentSignedUrlAsync(string storagePath, int expiresInSeconds = 3600)
        {
            try
            {
                await InitializeAsync();
                if (_client == null || _http == null) return null;

                var urlBase = _clientFactory.Url.TrimEnd('/');
                if (string.IsNullOrWhiteSpace(urlBase))
                    return null;

                var requestUrl = $"{urlBase}/storage/v1/object/sign/{DokumenteBucket}/{EscapeStoragePath(storagePath)}";

                using var req = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                AddSupabaseAuthHeaders(req);

                var payload = JsonSerializer.Serialize(new { expiresIn = expiresInSeconds });
                req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                using var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return null;

                var json = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("signedURL", out var signedUrlEl))
                    return null;

                var signedUrl = signedUrlEl.GetString();
                if (string.IsNullOrWhiteSpace(signedUrl))
                    return null;

                // Supabase returns a relative URL
                if (signedUrl.StartsWith("/"))
                    return urlBase + signedUrl;

                return signedUrl;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "CreateDokumentSignedUrlAsync failed");
                return null;
            }
        }

        private async Task<List<DocumentInfo>> ListDokumenteFromTableAsync(int? mitgliedId, int? parzelleId)
        {
            var result = new List<DocumentInfo>();
            try
            {
                await InitializeAsync();
                if (_client == null) return result;

                var resp = await _client.From<DokumentRecord>().Get();
                var models = resp?.Models;
                if (models == null) return result;

                if (mitgliedId.HasValue)
                    models = models.Where(d => d.MitgliedId == mitgliedId.Value).ToList();
                if (parzelleId.HasValue)
                    models = models.Where(d => d.ParzelleId == parzelleId.Value).ToList();

                foreach (var r in models)
                {
                    if (string.IsNullOrWhiteSpace(r.StoragePath))
                        continue;

                    result.Add(new DocumentInfo
                    {
                        Name = r.Dateiname ?? r.Titel ?? r.StoragePath,
                        StoragePath = r.StoragePath,
                        Size = r.SizeBytes,
                        UpdatedAt = r.UpdatedAt
                    });
                }

                // stable ordering
                result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ListDokumenteFromTableAsync failed");
                return result;
            }
        }

        private void AddSupabaseAuthHeaders(HttpRequestMessage req)
        {
            // Prefer user access token if available; fall back to anon key.
            var token = _client?.Auth?.CurrentSession?.AccessToken;

            if (!string.IsNullOrWhiteSpace(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var anonKey = _clientFactory.Key;
            if (!string.IsNullOrWhiteSpace(anonKey))
                req.Headers.Add("apikey", anonKey);
        }

        private static string EscapeStoragePath(string storagePath)
        {
            // Escape each segment, keep '/'
            var parts = storagePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join("/", parts.Select(Uri.EscapeDataString));
        }

        private static DateTime? NormalizeDate(DateTime? value)
        {
            if (!value.HasValue) return null;
            var noon = value.Value.Date.AddHours(12);
            return DateTime.SpecifyKind(noon, DateTimeKind.Unspecified);
        }

        private static bool Overlaps(ParzellenBelegungRecord r, DateTime date)
        {
            var d = date.Date;
            var von = (r.VonDatum ?? DateTime.MinValue).Date;
            var bis = r.BisDatum?.Date;
            return von <= d && (bis == null || bis >= d);
        }

        // =========================
        // Seasons / Mitglieder
        // =========================
        public async Task<List<string>> GetSeasonsAsync()
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return new List<string>();

                var resp = await _client.From<SaisonRecord>().Get();
                var years = resp?.Models?.Select(s => s.Jahr).ToList() ?? new List<int>();
                years.Sort();
                return years.Select(x => x.ToString()).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public async Task<List<MitgliedRecord>> GetMitgliederAsync()
        {
            var result = new List<MitgliedRecord>();
            try
            {
                await InitializeAsync();
                if (_client == null) return result;

                if (IsRestrictedToOwnMember(out var ownId))
                {
                    if (ownId <= 0) return result;

                    var one = await GetMitgliedByIdAsync(ownId);
                    if (one != null) result.Add(one);
                    return result;
                }

                var resp = await _client.From<MitgliedRecord>().Get();
                if (resp?.Models != null) result.AddRange(resp.Models);

                await TryApplyAppUserRolesAsync(result);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetMitgliederAsync failed");
            }
            return result;
        }

        public async Task<MitgliedRecord?> GetMitgliedByIdAsync(int mitgliedId)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return null;

                if (IsRestrictedToOwnMember(out var ownId))
                {
                    if (ownId <= 0) return null;
                    if (mitgliedId != ownId)
                    {
                        // Own-only Mode: erlaube auch das Nebenmitglied des eigenen Hauptmitglieds.
                        var targetResp = await _client
                            .From<MitgliedRecord>()
                            .Where(m => m.Id == mitgliedId)
                            .Get();

                        var target = targetResp?.Models?.FirstOrDefault();

                        if (target?.HauptmitgliedId != ownId)
                        {
                            _logger?.LogWarning("Denied GetMitgliedByIdAsync for MitgliedId {MitgliedId} (own-only mode; own MitgliedId {OwnId})", mitgliedId, ownId);
                            return null;
                        }
                    }
                }

                var rec = await _client
                    .From<MitgliedRecord>()
                    .Where(m => m.Id == mitgliedId)
                    .Get();

                var one = rec?.Models?.FirstOrDefault();

                if (one != null)
                {
                    one.Geburtsdatum = NormalizeDate(one.Geburtsdatum);
                    one.MitgliedSeit = NormalizeDate(one.MitgliedSeit);
                    one.MitgliedEnde = NormalizeDate(one.MitgliedEnde);

                    await TryApplyAppUserRoleAsync(one);
                }

                return one;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetMitgliedByIdAsync failed");
                return null;
            }
        }

        public async Task<bool> UpdateMitgliedAsync(MemberDTO dto, string userId)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                if (IsRestrictedToOwnMember(out var ownId))
                {
                    if (ownId <= 0) return false;
                    if (dto.Id != ownId)
                    {
                        _logger?.LogWarning("Denied UpdateMitgliedAsync for MitgliedId {MitgliedId} (own-only mode; own MitgliedId {OwnId})", dto.Id, ownId);
                        return false;
                    }
                }

                if (!Guid.TryParse(userId, out var userGuid))
                    return false;

                var record = await _client
                    .From<MitgliedRecord>()
                    .Where(m => m.Id == dto.Id)
                    .Single();

                if (record == null) return false;
                if (record.LockedByUserId != userGuid) return false;

                record.Vorname = dto.Vorname;
                record.Name = dto.Nachname;
                // E-Mail: fachlich differenzieren
                // - kein Nutzerzugang (`auth_user_id` == null): Admin darf Kontakt-Mail normal über Stammdaten ändern
                // - Nutzerzugang vorhanden: Änderung nur über separaten OTP-Flow „Mailadresse ändern“
                if (record.AuthUserId == null)
                    record.Email = dto.Email;

                record.Geburtsdatum = NormalizeDate(dto.Geburtsdatum);
                record.Adresse = dto.Strasse;
                record.Plz = dto.PLZ;
                record.Ort = dto.Ort;
                record.Telefon = dto.Telefon;
                record.Handy = dto.Mobilnummer;
                record.Bemerkung = dto.Bemerkungen;
                record.WhatsappEinwilligung = dto.WhatsappEinwilligung;
                record.EmailInfoEinwilligung = dto.EmailInfoEinwilligung;
                record.EmailRechnungEinwilligung = dto.EmailRechnungEinwilligung;

                // Pflichtstunden-/Altersregel läuft fachlich über das Hauptmitglied.
                // UI steuert, ob das Feld beim Nebenmitglied editierbar ist; Service mappt es nur durch.
                record.ArbeitsstundenAltersregelTyp = string.IsNullOrWhiteSpace(dto.ArbeitsstundenAltersregelTyp)
                    ? "keine"
                    : dto.ArbeitsstundenAltersregelTyp;

                record.MitgliedSeit = NormalizeDate(dto.MitgliedSeit);
                record.MitgliedEnde = NormalizeDate(dto.MitgliedEnde);
                record.Aktiv = dto.MitgliedEnde == null;

                await _client
                    .From<MitgliedRecord>()
                    .Where(m => m.Id == dto.Id)
                    .Update(record);

                return true;
            }
            catch (PostgrestException pex)
            {
                _logger?.LogError(pex, "UpdateMitgliedAsync failed: {Message}", pex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "UpdateMitgliedAsync failed");
                return false;
            }
        }

        public async Task<bool> UpdateMitgliedEmailAsync(int mitgliedId, string newEmail, string userId)
        {
            if (mitgliedId <= 0)
                return false;

            if (string.IsNullOrWhiteSpace(newEmail) || string.IsNullOrWhiteSpace(userId))
                return false;

            newEmail = newEmail.Trim();

            if (!Guid.TryParse(userId, out var userGuid))
                return false;

            var locked = false;

            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                if (IsRestrictedToOwnMember(out var ownId))
                {
                    if (ownId <= 0) return false;
                    if (mitgliedId != ownId)
                    {
                        _logger?.LogWarning("Denied UpdateMitgliedEmailAsync for MitgliedId {MitgliedId} (own-only mode; own MitgliedId {OwnId})", mitgliedId, ownId);
                        return false;
                    }
                }

                locked = await TryLockMitgliedAsync(mitgliedId, userId, timeoutMinutes: 2);
                if (!locked)
                    return false;

                var record = await _client
                    .From<MitgliedRecord>()
                    .Where(m => m.Id == mitgliedId)
                    .Single();

                if (record == null)
                    return false;

                // Fachliche Absicherung: Auth-E-Mail-Änderung betrifft immer nur den aktuell eingeloggten User.
                // Daher darf `mitglied.email` hier nur für den eigenen Datensatz angepasst werden.
                if (record.AuthUserId != userGuid)
                    throw new InvalidOperationException("Mailadresse kann nur für das eigene Konto geändert werden.");

                record.Email = newEmail;

                await _client
                    .From<MitgliedRecord>()
                    .Where(m => m.Id == mitgliedId)
                    .Update(record);

                return true;
            }
            catch (PostgrestException pex)
            {
                _logger?.LogError(pex, "UpdateMitgliedEmailAsync failed: {Message}", pex.Message);
                TryAppendErrorLog("UpdateMitgliedEmailAsync", pex);
                throw new InvalidOperationException(BuildUserFacingSaveError(pex), pex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "UpdateMitgliedEmailAsync failed");
                TryAppendErrorLog("UpdateMitgliedEmailAsync", ex);
                throw;
            }
            finally
            {
                if (locked)
                {
                    try
                    {
                        await ReleaseLockMitgliedAsync(mitgliedId, userId, force: false);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void TryAppendErrorLog(string context, Exception ex)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KGV");
                Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, "error.log");

                var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}\n{ex}\n\n";
                File.AppendAllText(file, text);
            }
            catch
            {
            }
        }

        // =========================
        // Parzellen
        // =========================
        public async Task<ParzelleRecord?> GetParzelleByNumberAsync(string gartenNr)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return null;

                var resp = await _client.From<ParzelleRecord>().Get();
                return resp?.Models?.FirstOrDefault(p =>
                    string.Equals(p.GartenNr, gartenNr, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<ParzelleRecord>> GetAllParzellenAsync()
        {
            var list = new List<ParzelleRecord>();
            try
            {
                await InitializeAsync();
                if (_client == null) return list;

                var resp = await _client.From<ParzelleRecord>().Get();
                if (resp?.Models != null) list.AddRange(resp.Models);
            }
            catch
            {
            }
            return list;
        }

        public async Task<List<ParzelleRecord>> GetParzellenForRfidSetupAsync()
        {
            // Minimaler Wrapper, damit der neue Flow eine klar benannte Service-Methode hat.
            var list = await GetAllParzellenAsync();

            // stabile Sortierung nach GartenNr (numerisch, falls möglich)
            return list
                .OrderBy(p => int.TryParse((p.GartenNr ?? string.Empty).Trim(), out var n) ? n : int.MaxValue)
                .ThenBy(p => (p.GartenNr ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => (p.Anlage ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<RfidUidAssignmentInfo?> FindRfidUidAssignmentAsync(string uid)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return null;

                uid = (uid ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(uid)) return null;

                var stromResp = await _client
                    .From<ParzelleRecord>()
                    .Where(x => x.RfidStrom == uid)
                    .Limit(1)
                    .Get();

                var strom = stromResp?.Models?.FirstOrDefault();
                if (strom != null)
                {
                    return new RfidUidAssignmentInfo(
                        strom.Id,
                        (strom.GartenNr ?? string.Empty).Trim(),
                        (strom.Anlage ?? string.Empty).Trim(),
                        ZaehlerTyp: 1,
                        FeldName: "rfid_strom");
                }

                var wasserResp = await _client
                    .From<ParzelleRecord>()
                    .Where(x => x.RfidWasser == uid)
                    .Limit(1)
                    .Get();

                var wasser = wasserResp?.Models?.FirstOrDefault();
                if (wasser != null)
                {
                    return new RfidUidAssignmentInfo(
                        wasser.Id,
                        (wasser.GartenNr ?? string.Empty).Trim(),
                        (wasser.Anlage ?? string.Empty).Trim(),
                        ZaehlerTyp: 2,
                        FeldName: "rfid_wasser");
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "FindRfidUidAssignmentAsync failed");
                return null;
            }
        }

        public async Task<bool> SetParzelleRfidAsync(int parzelleId, short zaehlerTyp, string uid)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                if (parzelleId <= 0) return false;
                uid = (uid ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(uid)) return false;

                if (zaehlerTyp is not (1 or 2))
                    return false;

                var rec = await _client
                    .From<ParzelleRecord>()
                    .Where(x => x.Id == parzelleId)
                    .Single();

                if (rec == null) return false;

                if (zaehlerTyp == 1)
                {
                    if (!string.IsNullOrWhiteSpace((rec.RfidStrom ?? string.Empty).Trim()))
                        return false;

                    rec.RfidStrom = uid;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace((rec.RfidWasser ?? string.Empty).Trim()))
                        return false;

                    rec.RfidWasser = uid;
                }

                await _client
                    .From<ParzelleRecord>()
                    .Where(x => x.Id == parzelleId)
                    .Update(rec);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "SetParzelleRfidAsync failed");
                return false;
            }
        }

        public async Task<List<ZaehlerEichstatusRecord>> GetZaehlerEichstatusAsync()
        {
            var list = new List<ZaehlerEichstatusRecord>();

            try
            {
                await InitializeAsync();
                if (_client == null) return list;

                var resp = await _client
                    .From<ZaehlerEichstatusRecord>()
                    .Get();

                if (resp?.Models != null)
                    list.AddRange(resp.Models);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetZaehlerEichstatusAsync failed");
            }

            return list;
        }

        // =========================
        // Belegungen
        // =========================
        public async Task<ParzellenBelegungRecord?> GetCurrentBelegungForParzelleAsync(int parzelleId)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return null;

                var resp = await _client
                    .From<ParzellenBelegungRecord>()
                    .Where(b => b.ParzelleId == parzelleId)
                    .Get();

                if (resp?.Models == null) return null;

                var today = DateTime.Today;
                return resp.Models
                    .Where(b => Overlaps(b, today))
                    .OrderByDescending(b => b.VonDatum ?? DateTime.MinValue)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<ParzellenBelegungRecord>> GetBelegungenForMitgliedAsync(int mitgliedId)
        {
            var list = new List<ParzellenBelegungRecord>();
            try
            {
                await InitializeAsync();
                if (_client == null) return list;

                if (IsRestrictedToOwnMember(out var ownId))
                {
                    if (ownId <= 0) return list;
                    if (mitgliedId != ownId)
                    {
                        _logger?.LogWarning("Denied GetBelegungenForMitgliedAsync for MitgliedId {MitgliedId} (own-only mode; own MitgliedId {OwnId})", mitgliedId, ownId);
                        return list;
                    }
                }

                var resp = await _client
                    .From<ParzellenBelegungRecord>()
                    .Where(b => b.MitgliedId == mitgliedId)
                    .Get();

                if (resp?.Models != null) list.AddRange(resp.Models);
            }
            catch
            {
            }
            return list;
        }

        public async Task<List<ParzellenBelegungRecord>> GetAllParzellenBelegungenAsync()
        {
            var list = new List<ParzellenBelegungRecord>();
            try
            {
                await InitializeAsync();
                if (_client == null) return list;

                if (IsRestrictedToOwnMember(out _))
                    return list;

                var resp = await _client.From<ParzellenBelegungRecord>().Get();
                if (resp?.Models != null) list.AddRange(resp.Models);
            }
            catch
            {
            }
            return list;
        }

        // ✅ Zuweisung: schreibt einen Datensatz in parzellen_belegung (für Verlauf über VonDatum/BisDatum)
        public async Task<bool> AssignParzelleToMitgliedAsync(int mitgliedId, int parzelleId, DateTime startDatum)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                if (IsRestrictedToOwnMember(out var ownId))
                {
                    _logger?.LogWarning("Denied AssignParzelleToMitgliedAsync for MitgliedId {MitgliedId} (own-only mode)", mitgliedId);
                    return false;
                }

                var start = NormalizeDate(startDatum)!.Value;

                var resp = await _client
                    .From<ParzellenBelegungRecord>()
                    .Where(b => b.ParzelleId == parzelleId)
                    .Get();

                var belegungen = resp?.Models?.ToList() ?? new List<ParzellenBelegungRecord>();

                var currentAtStart = belegungen
                    .Where(r => Overlaps(r, start))
                    .OrderByDescending(r => r.VonDatum ?? DateTime.MinValue)
                    .FirstOrDefault();

                if (currentAtStart != null && currentAtStart.MitgliedId == mitgliedId)
                    return true;

                // Wenn am Startdatum bereits jemand belegt ist, wird trotzdem eine neue Belegung angelegt.
                // (Manuelle Datenbereinigung/Regeln werden außerhalb erzwungen.)

                var newRec = new ParzellenBelegungInsertRecord
                {
                    ParzelleId = parzelleId,
                    MitgliedId = mitgliedId,
                    VonDatum = start,
                    BisDatum = null
                };

                await _client.From<ParzellenBelegungInsertRecord>().Insert(newRec);
                return true;
            }
            catch (PostgrestException pex)
            {
                _logger?.LogError(pex, "AssignParzelleToMitgliedAsync failed: {Message}", pex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "AssignParzelleToMitgliedAsync failed");
                throw;
            }
        }

        public async Task<bool> EndParzellenBelegungAsync(int belegungId, DateTime bisDatum)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                var rec = await _client
                    .From<ParzellenBelegungRecord>()
                    .Where(b => b.Id == belegungId)
                    .Single();

                if (rec == null) return false;

                var bis = NormalizeDate(bisDatum)!.Value;
                if (rec.VonDatum.HasValue && bis.Date < rec.VonDatum.Value.Date)
                    return false;

                rec.BisDatum = bis;

                await _client
                    .From<ParzellenBelegungRecord>()
                    .Where(b => b.Id == belegungId)
                    .Update(rec);

                return true;
            }
            catch
            {
                return false;
            }
        }

        // =========================
        // Locking Mitglied
        // =========================
        public async Task<bool> TryLockMitgliedAsync(int mitgliedId, string userId, int timeoutMinutes = 10)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                if (IsRestrictedToOwnMember(out var ownId))
                {
                    if (ownId <= 0) return false;
                    if (mitgliedId != ownId)
                    {
                        _logger?.LogWarning("Denied TryLockMitgliedAsync for MitgliedId {MitgliedId} (own-only mode; own MitgliedId {OwnId})", mitgliedId, ownId);
                        return false;
                    }
                }

                if (!Guid.TryParse(userId, out var userGuid))
                    return false;

                var record = await _client
                    .From<MitgliedRecord>()
                    .Where(m => m.Id == mitgliedId)
                    .Single();

                if (record == null) return false;

                // Drift verhindern
                record.Geburtsdatum = NormalizeDate(record.Geburtsdatum);
                record.MitgliedSeit = NormalizeDate(record.MitgliedSeit);
                record.MitgliedEnde = NormalizeDate(record.MitgliedEnde);

                var now = DateTime.UtcNow;
                var lockExpired = record.LockedAt.HasValue && record.LockedAt.Value.AddMinutes(timeoutMinutes) < now;

                if (record.LockedByUserId == null || record.LockedByUserId == userGuid || lockExpired)
                {
                    record.LockedByUserId = userGuid;
                    record.LockedAt = now;

                    await _client
                        .From<MitgliedRecord>()
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

                if (IsRestrictedToOwnMember(out var ownId))
                {
                    if (ownId <= 0) return false;
                    if (mitgliedId != ownId)
                    {
                        _logger?.LogWarning("Denied ReleaseLockMitgliedAsync for MitgliedId {MitgliedId} (own-only mode; own MitgliedId {OwnId})", mitgliedId, ownId);
                        return false;
                    }
                }

                if (!Guid.TryParse(userId, out var userGuid))
                    return false;

                var record = await _client
                    .From<MitgliedRecord>()
                    .Where(m => m.Id == mitgliedId)
                    .Single();

                if (record == null) return false;

                record.Geburtsdatum = NormalizeDate(record.Geburtsdatum);
                record.MitgliedSeit = NormalizeDate(record.MitgliedSeit);
                record.MitgliedEnde = NormalizeDate(record.MitgliedEnde);

                if (!force && record.LockedByUserId != userGuid)
                    return false;

                record.LockedByUserId = null;
                record.LockedAt = null;

                await _client
                    .From<MitgliedRecord>()
                    .Where(m => m.Id == mitgliedId)
                    .Update(record);

                return true;
            }
            catch
            {
                return false;
            }
        }

        // =========================
        // Locking Arbeitsstunde (wie vorher)
        // =========================
        public async Task<bool> TryLockArbeitsstundeAsync(int arbeitsstundeId, string userId, int timeoutMinutes = 10)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                var record = await _client
                    .From<ArbeitsstundeRecord>()
                    .Where(a => a.Id == arbeitsstundeId)
                    .Single();

                if (record == null) return false;

                var now = DateTime.UtcNow;

                if (string.IsNullOrEmpty(record.LockedByUserId) ||
                    record.LockedByUserId == userId ||
                    (record.LockedAt.HasValue && record.LockedAt.Value.AddMinutes(timeoutMinutes) < now))
                {
                    record.LockedByUserId = userId;
                    record.LockedAt = now;

                    await _client
                        .From<ArbeitsstundeRecord>()
                        .Where(a => a.Id == arbeitsstundeId)
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

        public async Task<bool> ReleaseLockArbeitsstundeAsync(int arbeitsstundeId, string userId, bool force = false)
        {
            try
            {
                await InitializeAsync();
                if (_client == null) return false;

                var record = await _client
                    .From<ArbeitsstundeRecord>()
                    .Where(a => a.Id == arbeitsstundeId)
                    .Single();

                if (record == null) return false;

                if (!force && !string.Equals(record.LockedByUserId, userId, StringComparison.OrdinalIgnoreCase))
                    return false;

                record.LockedByUserId = null;
                record.LockedAt = null;

                await _client
                    .From<ArbeitsstundeRecord>()
                    .Where(a => a.Id == arbeitsstundeId)
                    .Update(record);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}