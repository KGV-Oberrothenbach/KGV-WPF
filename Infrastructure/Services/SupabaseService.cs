// File: Infrastructure/Services/SupabaseService.cs
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Infrastructure.Supabase;
using Microsoft.Extensions.Logging;
using Supabase;
using Supabase.Postgrest.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
        private readonly ILogger<SupabaseService>? _logger;
        private Client? _client;

        public SupabaseService(ISupabaseClientFactory clientFactory, ILogger<SupabaseService>? logger = null)
        {
            _clientFactory = clientFactory;
            _logger = logger;
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

                return resp?.Models?.FirstOrDefault();
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
                    rec.Email = main.Email;
                }

                var insertResp = await _client.From<MitgliedRecord>().Insert(rec);
                return insertResp?.Models?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "CreateNebenmitgliedAsync failed");
                return null;
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
                await InitializeAsync();
                if (_client == null) return null;
                if (!Guid.TryParse(authUserId, out var guid)) return null;

                var resp = await _client
                    .From<MitgliedRecord>()
                    .Where(m => m.AuthUserId == guid)
                    .Get();

                return resp?.Models?.FirstOrDefault();
            }
            catch
            {
                return null;
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
            if (_client != null) return;
            _client = await _clientFactory.CreateAsync();
            _http ??= new HttpClient();
        }

        // =========================
        // Dokumente (Supabase Storage)
        // =========================
        public async Task<List<DocumentInfo>> GetMitgliedDokumenteAsync(int mitgliedId)
        {
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

                var resp = await _client.From<MitgliedRecord>().Get();
                if (resp?.Models != null) result.AddRange(resp.Models);
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

                var rec = await _client
                    .From<MitgliedRecord>()
                    .Where(m => m.Id == mitgliedId)
                    .Single();

                if (rec != null)
                {
                    rec.Geburtsdatum = NormalizeDate(rec.Geburtsdatum);
                    rec.MitgliedSeit = NormalizeDate(rec.MitgliedSeit);
                    rec.MitgliedEnde = NormalizeDate(rec.MitgliedEnde);
                }

                return rec;
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
                record.Email = dto.Email;
                record.Role = dto.Role;

                record.Geburtsdatum = NormalizeDate(dto.Geburtsdatum);
                record.Adresse = dto.Strasse;
                record.Plz = dto.PLZ;
                record.Ort = dto.Ort;
                record.Telefon = dto.Telefon;
                record.Bemerkung = dto.Bemerkungen;
                record.WhatsappEinwilligung = dto.WhatsappEinwilligung;

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