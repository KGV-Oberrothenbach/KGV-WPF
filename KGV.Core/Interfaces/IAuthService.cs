using Supabase;
using System.Threading.Tasks;

namespace KGV.Core.Interfaces
{
    public interface IAuthService
    {
        /// <summary>
        /// Login mit Email + Passwort
        /// </summary>
        Task<bool> LoginAsync(string email, string password);

        /// <summary>
        /// Supabase-Client, um weitere Abfragen zu machen
        /// </summary>
        Task<Client> GetClientAsync();

        /// <summary>
        /// Rollen des eingeloggten Users
        /// </summary>
        bool IsVorstand { get; }
        bool IsAdmin { get; }
        /// <summary>
        /// Current authenticated user's id (supabase auth user id)
        /// </summary>
        string? CurrentUserId { get; }
    }
}
