using KGV.Core.Models;
using Supabase;
using System;
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

        /// <summary>
        /// Versucht eine persistierte Session wiederherzustellen (ohne UI).
        /// </summary>
        Task<bool> TryRestoreSessionAsync();

        /// <summary>
        /// Stellt sicher, dass eine vorhandene Session noch gültig ist (ggf. Refresh).
        /// </summary>
        Task<bool> EnsureValidSessionAsync(bool forceRefresh);

        /// <summary>
        /// Lokale Session verwerfen (Logout / "zurück zum Login").
        /// </summary>
        Task SignOutAsync();

        /// <summary>
        /// Sendet einen OTP-Code per E-Mail (MagicLink/OTP) für den Passwort-Neusetzen-Flow.
        /// </summary>
        Task<bool> RequestLoginOtpAsync(string email);

        /// <summary>
        /// Sendet einen Recovery-OTP per E-Mail ("Passwort vergessen").
        /// </summary>
        Task<bool> RequestRecoveryOtpAsync(string email);

        /// <summary>
        /// Verifiziert OTP (Login-OTP) und startet eine temporäre Reset-Session (darf nicht persistiert werden).
        /// </summary>
        Task<bool> BeginPasswordResetFromLoginOtpAsync(string email, string otp);

        /// <summary>
        /// Verifiziert OTP (Recovery) und startet eine temporäre Reset-Session (darf nicht persistiert werden).
        /// </summary>
        Task<bool> BeginPasswordResetFromRecoveryOtpAsync(string email, string otp);

        /// <summary>
        /// Setzt das neue Passwort in der aktiven Reset-Session und räumt die Session kontrolliert auf.
        /// </summary>
        Task<bool> CompletePasswordResetAsync(string newPassword);

        /// <summary>
        /// Bricht den Reset-Flow ab und räumt die temporäre Session kontrolliert auf.
        /// </summary>
        Task CancelPasswordResetSessionAsync();

        /// <summary>
        /// Startet den E-Mail-Änderungsprozess (Supabase sendet hierfür einen Code/OTP an die neue Adresse).
        /// </summary>
        Task<bool> RequestEmailChangeOtpAsync(string newEmail);

        /// <summary>
        /// Verifiziert die E-Mail-Änderung codebasiert (OTP) und aktualisiert Session/Persistenz.
        /// </summary>
        Task<bool> VerifyEmailChangeOtpAsync(string newEmail, string otp);

        /// <summary>
        /// Startet einen Google OAuth Sign-In (liefert Auth-URL + PKCE-Verifier für die Callback-Verarbeitung).
        /// </summary>
        Task<OAuthSignInStartResult?> StartGoogleSignInAsync(string redirectUri);

        /// <summary>
        /// Finalisiert Google OAuth Sign-In anhand Authorization Code + PKCE-Verifier.
        /// </summary>
        Task<bool> CompleteGoogleSignInAsync(string authCode, string pkceVerifier);
    }
}
