using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Infrastructure.Models;
using KGV.Infrastructure.Supabase;
using Supabase;
using Supabase.Gotrue.Exceptions;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace KGV.Infrastructure.Authentication
{
    public class AuthService : IAuthService
    {
        private readonly ISupabaseClientFactory _clientFactory;
        private readonly ILogger<AuthService>? _logger;
        private Client? _client;

        public AuthService(ISupabaseClientFactory clientFactory, ILogger<AuthService>? logger = null)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _logger = logger;
        }

        public bool IsVorstand { get; private set; } = false;
        public bool IsAdmin { get; private set; } = false;
        public string? CurrentUserId { get; private set; }

        /// <summary>
        /// Supabase Client initialisieren oder zurückgeben
        /// </summary>
        public async Task<Client> GetClientAsync()
        {
            if (_client == null)
            {
                _client = await _clientFactory.CreateAsync();
            }
            return _client;
        }

        public async Task<bool> LoginAsync(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                _logger?.LogWarning("Login attempt rejected: missing email or password.");
                return false;
            }

            email = email.Trim();

            try
            {
                _logger?.LogInformation("SignIn attempt for {EmailMasked}", MaskEmail(email));

                var client = await GetClientAsync();
                if (client == null)
                {
                    _logger?.LogError("Supabase client is null in LoginAsync.");
                    return false;
                }

                var session = await client.Auth.SignIn(email: email, password: password);
                if (session == null)
                {
                    _logger?.LogWarning("SignIn returned null session for {EmailMasked}", MaskEmail(email));
                    return false;
                }

                var user = session.User;
                if (user == null || string.IsNullOrEmpty(user.Id))
                {
                    _logger?.LogWarning("SignIn succeeded but session.User is null or has no Id for {EmailMasked}", MaskEmail(email));
                    return false;
                }

                _logger?.LogInformation("SignIn successful for {EmailMasked}", MaskEmail(email));

                CurrentUserId = user.Id;

                // Rollen setzen – app_user.role ist ab jetzt die führende Rollenquelle
                AppUserRecord? appUser = null;

                // user.Id ist string -> in Guid umwandeln, weil app_user.user_id = uuid
                if (!Guid.TryParse(user.Id, out var userGuid))
                {
                    _logger?.LogWarning("User.Id is not a valid Guid: {UserId}", user.Id);
                    IsVorstand = false;
                    IsAdmin = false;
                    return true; // Login ist trotzdem ok, nur keine Rollen
                }

                try
                {
                    appUser = await client
                        .From<AppUserRecord>()
                        .Where(x => x.UserId == userGuid)
                        .Single();
                }
                catch (Exception ex)
                {
                    _logger?.LogInformation(ex, "No app_user record found or error while querying for user {UserId}", user.Id);
                }

                // Übergangs-Phase: app_user.role ist führend; wenn app_user fehlt, defensiv auf mitglied.role fallbacken.
                var role = (appUser?.Role ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(role))
                {
                    try
                    {
                        var memberFallback = await client
                            .From<MitgliedRecord>()
                            .Where(m => m.AuthUserId == userGuid)
                            .Single();

                        role = (memberFallback?.Role ?? string.Empty).Trim();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                IsVorstand = string.Equals(role, "vorstand", StringComparison.OrdinalIgnoreCase);
                IsAdmin = string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);

                return true;
            }
            catch (GotrueException ex)
            {
                _logger?.LogError(ex, "GotrueException during SignIn for {EmailMasked}: {Message}", MaskEmail(email), ex.Message);
                throw new InvalidOperationException($"Login fehlgeschlagen: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error during SignIn for {EmailMasked}", MaskEmail(email));
                throw new InvalidOperationException($"Login fehlgeschlagen: {ex.Message}", ex);
            }
        }

        private static string MaskEmail(string? email)
        {
            if (string.IsNullOrEmpty(email))
                return "<empty>";

            var atIndex = email.IndexOf('@');
            if (atIndex > 1)
            {
                var domain = email.Substring(atIndex + 1);
                return $"{email[0]}***@{domain}";
            }

            if (email.Length > 3)
                return $"{email.Substring(0, 1)}***{email.Substring(email.Length - 1)}";

            return "***";
        }
    }
}