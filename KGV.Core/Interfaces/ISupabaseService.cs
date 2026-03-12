// File: Core/Interfaces/ISupabaseService.cs
using Supabase;
using KGV.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KGV.Core.Interfaces
{
    public interface ISupabaseService
    {
        Client Client { get; }
        Task InitializeAsync();

        Task<List<string>> GetSeasonsAsync();
        Task<List<MitgliedRecord>> GetMitgliederAsync();

        // ✅ Vollständige Stammdaten
        Task<MitgliedRecord?> GetMitgliedByIdAsync(int mitgliedId);
        Task<bool> UpdateMitgliedAsync(MemberDTO dto, string userId);

        Task<ParzelleRecord?> GetParzelleByNumberAsync(string gartenNr);
        Task<List<ParzelleRecord>> GetAllParzellenAsync();

        // =========================
        // RFID einrichten (Verwaltung)
        // =========================
        Task<List<ParzelleRecord>> GetParzellenForRfidSetupAsync();
        Task<RfidUidAssignmentInfo?> FindRfidUidAssignmentAsync(string uid);
        Task<bool> SetParzelleRfidAsync(int parzelleId, short zaehlerTyp, string uid);

        // =========================
        // Fällige Zähler (Übersicht)
        // =========================
        Task<List<ZaehlerEichstatusRecord>> GetZaehlerEichstatusAsync();

        Task<ParzellenBelegungRecord?> GetCurrentBelegungForParzelleAsync(int parzelleId);

        // ✅ Belegungen für Parzellen-Zuweisung
        Task<List<ParzellenBelegungRecord>> GetBelegungenForMitgliedAsync(int mitgliedId);
        Task<List<ParzellenBelegungRecord>> GetAllParzellenBelegungenAsync();
        Task<bool> AssignParzelleToMitgliedAsync(int mitgliedId, int parzelleId, DateTime startDatum);
        Task<bool> EndParzellenBelegungAsync(int belegungId, DateTime bisDatum);

        Task<List<ZaehlerAblesungDTO>> GetStromAblesungenAsync(int parzelleId);
        Task<List<ZaehlerAblesungDTO>> GetWasserAblesungenAsync(int parzelleId);

        Task<StromzaehlerRecord?> GetActiveStromzaehlerAsync(int parzelleId, DateTime onDate);
        Task<WasserzaehlerRecord?> GetActiveWasserzaehlerAsync(int parzelleId, DateTime onDate);

        Task<bool> AddStromzaehlerAsync(int parzelleId, string zaehlernummer, DateTime eichdatum, DateTime eingebautAm);
        Task<bool> AddWasserzaehlerAsync(int parzelleId, string zaehlernummer, DateTime eichdatum, DateTime eingebautAm);

        Task<bool> SetStromzaehlerAusgebautAmAsync(long stromzaehlerId, DateTime ausgebautAm);
        Task<bool> SetWasserzaehlerAusgebautAmAsync(long wasserzaehlerId, DateTime ausgebautAm);

        Task<bool> AddAblesungAsync(short zaehlerTyp, long zaehlerId, DateTime ablesedatum, decimal stand, string? fotoPfad);
        Task<bool> UpdateAblesungAsync(long ablesungId, DateTime ablesedatum, decimal stand, string? fotoPfad);

        Task<SaveAblesungResult> AddAblesungResultAsync(short zaehlerTyp, long zaehlerId, DateTime ablesedatum, decimal stand, string? fotoPfad);

        // =========================
        // Nebenmitglied
        // =========================
        Task<MitgliedRecord?> GetNebenmitgliedByHauptmitgliedIdAsync(int hauptmitgliedId);
        Task<MitgliedRecord?> CreateNebenmitgliedAsync(int hauptmitgliedId, string vorname, string nachname, bool adresseUebernehmen);

        // =========================
        // Arbeitsstunden
        // =========================
        Task<List<SaisonRecord>> GetSaisonRecordsAsync();
        Task<SaisonRecord?> SaveSaisonAsync(SaisonRecord saison);

        Task<List<WartungsvertragRecord>> GetWartungsvertraegeAsync();
        Task<WartungsvertragRecord?> SaveWartungsvertragAsync(WartungsvertragRecord wartungsvertrag);

        Task<List<WartungsvertragZuordnungRecord>> GetWartungsvertragZuordnungenAsync(int hauptmitgliedId);
        Task<WartungsvertragZuordnungRecord?> SaveWartungsvertragZuordnungAsync(WartungsvertragZuordnungRecord zuordnung);
        Task<bool> EndWartungsvertragZuordnungAsync(long zuordnungId, DateTime gueltigBis, string? bemerkung);

        Task<PflichtstundenUebersichtRecord?> GetPflichtstundenUebersichtAsync(int hauptmitgliedId, int saisonId);
        Task<List<PflichtstundenUebersichtRecord>> GetPflichtstundenUebersichtForSaisonAsync(int saisonId);
        Task<MitgliedRecord?> GetMitgliedByAuthUserIdAsync(Guid authUserId);
        Task<MitgliedRecord?> GetMitgliedByAuthUserIdAsync(string authUserId);

        Task<bool> UpdateOwnContactAsync(int mitgliedId, string? telefon, string? handy, string? adresse, string? plz, string? ort);
        Task<List<ArbeitsstundeDTO>> GetArbeitsstundenAsync(params int[] mitgliedIds);
        Task<bool> AddArbeitsstundeAsync(ArbeitsstundeRecord record);
        Task<bool> UpdateArbeitsstundeAsync(ArbeitsstundeRecord record);
        Task<bool> DeleteArbeitsstundeAsync(int arbeitsstundeId);
        Task<List<(int MitgliedId, string Vorname, string Nachname, int Count)>> GetUnapprovedArbeitsstundenByMitgliedAsync();

        Task<bool> TryLockMitgliedAsync(int mitgliedId, string userId, int timeoutMinutes = 10);
        Task<bool> ReleaseLockMitgliedAsync(int mitgliedId, string userId, bool force = false);

        Task<bool> TryLockArbeitsstundeAsync(int arbeitsstundeId, string userId, int timeoutMinutes = 10);
        Task<bool> ReleaseLockArbeitsstundeAsync(int arbeitsstundeId, string userId, bool force = false);

        // =========================
        // Dokumente (Supabase Storage)
        // =========================
        Task<List<DocumentInfo>> GetMitgliedDokumenteAsync(int mitgliedId);
        Task<List<DocumentInfo>> GetParzelleDokumenteAsync(int parzelleId);
        Task<string?> CreateDokumentSignedUrlAsync(string storagePath, int expiresInSeconds = 3600);

        // =========================
        // app_user (Rollenquelle)
        // =========================
        Task<bool> HasAppUserForMitgliedAsync(long mitgliedId);
        Task<string?> GetAppUserRoleForMitgliedAsync(long mitgliedId);
        Task<bool> UpdateAppUserRoleForMitgliedAsync(long mitgliedId, string role);
        Task<List<AppUserDTO>> GetAppUsersAsync();

        // =========================
        // mitglied.role (Übergangs-Fallback / vorbereitete Zielrolle)
        // =========================
        Task<bool> UpdateMitgliedRoleForMitgliedAsync(int mitgliedId, string role, string userId);

        // =========================
        // Nutzerverwaltung (Admin)
        // =========================
        Task<PrepareAddUserResult> PrepareAddUserForMitgliedAsync(int mitgliedId, string role);
        Task<InviteUserAccountResult> InviteUserAccountForMitgliedAsync(int mitgliedId, string role);
        Task<DeleteUserAccountResult> DeleteUserAccountForMitgliedAsync(int mitgliedId);

        // =========================
        // Startseite (Lesen)
        // =========================
        Task<List<StartseiteBekanntmachungRecord>> GetStartseiteBekanntmachungenAsync();
        Task<List<StartseiteTerminRecord>> GetStartseiteTermineAsync();
        Task<List<StartseiteArbeitseinsatzRecord>> GetStartseiteArbeitseinsaetzeAsync();

        // =========================
        // Startseite (Verwaltung)
        // =========================
        Task<StartseiteBekanntmachungRecord?> SaveStartseiteBekanntmachungAsync(StartseiteBekanntmachungRecord record);
        Task<StartseiteTerminRecord?> SaveStartseiteTerminAsync(StartseiteTerminRecord record);
        Task<StartseiteArbeitseinsatzRecord?> SaveStartseiteArbeitseinsatzAsync(StartseiteArbeitseinsatzRecord record);

        Task<HashSet<long>> GetMyArbeitseinsatzAnmeldungenAsync();
        Task<bool> SignUpForArbeitseinsatzAsync(long arbeitseinsatzId);
        Task<bool> SignOffFromArbeitseinsatzAsync(long arbeitseinsatzId);

        Task<RfidScanContextRecord?> GetRfidScanContextAsync(string rfidTagUid);
    }
}
