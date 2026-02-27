using Supabase;
using KGV.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KGV.Core.Interfaces
{
    public interface ISupabaseService
    {
        /// <summary>
        /// Supabase Client für Auth, Realtime, Storage etc.
        /// </summary>
        Client Client { get; }

        /// <summary>
        /// Initialisiert den Supabase Client
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Liefert alle verfügbaren Saisons
        /// </summary>
        Task<List<string>> GetSeasonsAsync();

        /// <summary>
        /// Liefert alle Mitglieder
        /// </summary>
        Task<List<MitgliedRecord>> GetMitgliederAsync();

        /// <summary>
        /// Liefert eine Parzelle anhand der Gartennummer
        /// </summary>
        Task<ParzelleRecord?> GetParzelleByNumberAsync(string gartenNr);

        /// <summary>
        /// Liefert die aktuelle Belegung einer Parzelle
        /// </summary>
        Task<ParzellenBelegungRecord?> GetCurrentBelegungForParzelleAsync(int parzelleId);

        /// <summary>
        /// Liefert alle Parzellen
        /// </summary>
        Task<List<ParzelleRecord>> GetAllParzellenAsync();

        /// <summary>
        /// Versucht, ein Mitglied für Bearbeitung zu sperren
        /// </summary>
        Task<bool> TryLockMitgliedAsync(int mitgliedId, string userId, int timeoutMinutes = 10);

        /// <summary>
        /// Gibt die Sperre eines Mitglieds wieder frei
        /// </summary>
        Task<bool> ReleaseLockMitgliedAsync(int mitgliedId, string userId, bool force = false);

        /// <summary>
        /// Versucht, eine Arbeitsstunde für Bearbeitung zu sperren
        /// </summary>
        Task<bool> TryLockArbeitsstundeAsync(int arbeitsstundeId, string userId, int timeoutMinutes = 10);

        /// <summary>
        /// Gibt die Sperre einer Arbeitsstunde wieder frei
        /// </summary>
        Task<bool> ReleaseLockArbeitsstundeAsync(int arbeitsstundeId, string userId, bool force = false);
    }
}