using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Infrastructure.Models;
using KGV.Infrastructure.Supabase;
using Supabase;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

// Alias um Konflikt mit Supabase.Gotrue.Client zu vermeiden
using SupabaseClient = Supabase.Client;

namespace KGV.Infrastructure.Authentication
{
    public class AuthService : IAuthService
    {
        private readonly ISupabaseClientFactory _clientFactory;
        private readonly ILogger<AuthService>? _logger;
        private readonly ISupabaseSessionStore? _sessionStore;
        private SupabaseClient? _client;

        public AuthService(
            ISupabaseClientFactory clientFactory,
            ILogger<AuthService>? logger = null,
            ISupabaseSessionStore? sessionStore = null)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _logger = logger;
            _sessionStore = sessionStore;
        }

        public bool IsVorstand { get; private set; } = false;
        public bool IsAdmin { get; private set; } = false;
        public string? CurrentUserId { get; private set; }

        /// <summary>
        /// Supabase Client initialisieren oder zurückgeben
        /// </summary>
        public async Task<SupabaseClient> GetClientAsync()
        {
            if (_client == null)
            {
                _client = await _clientFactory.CreateAsync();
            }
            return _client;
        }

        public async Task<bool> TryRestoreSessionAsync()
        {
            try
            {
                var client = await GetClientAsync();

                if (client.Auth.CurrentSession == null)
                    return false;

                var ok = await EnsureValidSessionAsync(forceRefresh: false);
                if (!ok)
                {
                    await SignOutAsync();
                    return false;
                }

                var session = client.Auth.CurrentSession;
                if (session?.User?.Id == null)
                    return false;

                CurrentUserId = session.User.Id;

                if (Guid.TryParse(CurrentUserId, out var userGuid))
                    await ResolveRolesAsync(client, userGuid);
                else
                {
                    IsVorstand = false;
                    IsAdmin = false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogInformation(ex, "TryRestoreSessionAsync failed");
                return false;
            }
        }

        public async Task<OAuthSignInStartResult?> StartGoogleSignInAsync(string redirectUri)
        {
            if (string.IsNullOrWhiteSpace(redirectUri))
                return null;

            redirectUri = redirectUri.Trim();

            try
            {
                var client = await GetClientAsync();

                var state = await client.Auth.SignIn(
                    global::Supabase.Gotrue.Constants.Provider.Google,
                    new SignInOptions
                    {
                        RedirectTo = redirectUri,
                        FlowType = global::Supabase.Gotrue.Constants.OAuthFlowType.PKCE
                    });

                if (state?.Uri == null || string.IsNullOrWhiteSpace(state.PKCEVerifier))
                    return null;

                return new OAuthSignInStartResult(state.Uri, state.PKCEVerifier);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "StartGoogleSignInAsync failed");
                return null;
            }
        }

        public async Task<bool> CompleteGoogleSignInAsync(string authCode, string pkceVerifier)
        {
            if (string.IsNullOrWhiteSpace(authCode) || string.IsNullOrWhiteSpace(pkceVerifier))
                return false;

            authCode = authCode.Trim();
            pkceVerifier = pkceVerifier.Trim();

            try
            {
                var client = await GetClientAsync();
                var session = await client.Auth.ExchangeCodeForSession(pkceVerifier, authCode);
                if (session?.User?.Id == null)
                    return false;

                try
                {
                    _sessionStore?.Save(session);
                }
                catch
                {
                }

                CurrentUserId = session.User.Id;

                if (Guid.TryParse(CurrentUserId, out var userGuid))
                    await ResolveRolesAsync(client, userGuid);
                else
                {
                    IsVorstand = false;
                    IsAdmin = false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "CompleteGoogleSignInAsync failed");
                return false;
            }
        }

        private void SuppressSessionPersistence(SupabaseClient client)
        {
            try
            {
                // Verhindert, dass OTP-/Recovery-Verify eine Session persistent ablegt.
                client.Auth.SetPersistence(new global::Supabase.DefaultSupabaseSessionHandler());
            }
            catch
            {
            }
        }

        private void RestoreSessionPersistence(SupabaseClient client)
        {
            try
            {
                if (_sessionStore != null)
                    client.Auth.SetPersistence(new KgvSupabaseSessionHandler(_sessionStore));
                else
                    client.Auth.SetPersistence(new global::Supabase.DefaultSupabaseSessionHandler());
            }
            catch
            {
            }
        }

        public async Task<bool> RequestLoginOtpAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            email = email.Trim();

            try
            {
                var client = await GetClientAsync();

                await client.Auth.SignInWithOtp(new SignInWithPasswordlessEmailOptions(email)
                {
                    ShouldCreateUser = false
                });

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "RequestLoginOtpAsync failed for {EmailMasked}", MaskEmail(email));
                return false;
            }
        }

        public async Task<bool> RequestRecoveryOtpAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            email = email.Trim();

            try
            {
                var client = await GetClientAsync();
                await client.Auth.ResetPasswordForEmail(email);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "RequestRecoveryOtpAsync failed for {EmailMasked}", MaskEmail(email));
                return false;
            }
        }

        public async Task<bool> BeginPasswordResetFromLoginOtpAsync(string email, string otp)
        {
            return await BeginPasswordResetWithOtpAsync(email, otp, global::Supabase.Gotrue.Constants.EmailOtpType.MagicLink);
        }

        public async Task<bool> BeginPasswordResetFromRecoveryOtpAsync(string email, string otp)
        {
            return await BeginPasswordResetWithOtpAsync(email, otp, global::Supabase.Gotrue.Constants.EmailOtpType.Recovery);
        }

        private async Task<bool> BeginPasswordResetWithOtpAsync(string email, string otp, global::Supabase.Gotrue.Constants.EmailOtpType otpType)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
                return false;

            email = email.Trim();
            otp = otp.Trim();

            try
            {
                var client = await GetClientAsync();

                SuppressSessionPersistence(client);

                var session = await client.Auth.VerifyOTP(email, otp, otpType);
                if (session == null)
                {
                    RestoreSessionPersistence(client);
                    return false;
                }

                // Wichtig: Hier KEINE Rollenauflösung/CurrentUserId setzen.
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "BeginPasswordResetWithOtpAsync failed for {EmailMasked}", MaskEmail(email));

                try
                {
                    var client = await GetClientAsync();
                    await client.Auth.SignOut(global::Supabase.Gotrue.Constants.SignOutScope.Local);
                    RestoreSessionPersistence(client);
                }
                catch
                {
                }

                return false;
            }
        }

        public async Task<bool> CompletePasswordResetAsync(string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
                return false;

            newPassword = newPassword.Trim();

            try
            {
                var client = await GetClientAsync();

                if (client.Auth.CurrentSession == null)
                    return false;

                await client.Auth.Update(new UserAttributes { Password = newPassword });

                try
                {
                    await client.Auth.SignOut(global::Supabase.Gotrue.Constants.SignOutScope.Local);
                }
                catch
                {
                }

                try
                {
                    _sessionStore?.Clear();
                }
                catch
                {
                }

                RestoreSessionPersistence(client);

                CurrentUserId = null;
                IsVorstand = false;
                IsAdmin = false;

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "CompletePasswordResetAsync failed");
                return false;
            }
        }

        public async Task CancelPasswordResetSessionAsync()
        {
            try
            {
                var client = await GetClientAsync();

                try
                {
                    await client.Auth.SignOut(global::Supabase.Gotrue.Constants.SignOutScope.Local);
                }
                catch
                {
                }

                try
                {
                    _sessionStore?.Clear();
                }
                catch
                {
                }

                RestoreSessionPersistence(client);
            }
            catch
            {
            }

            CurrentUserId = null;
            IsVorstand = false;
            IsAdmin = false;
        }

        public async Task<bool> EnsureValidSessionAsync(bool forceRefresh)
        {
            try
            {
                var client = await GetClientAsync();
                var session = client.Auth.CurrentSession;

                if (session == null)
                    return false;

                var shouldRefresh = forceRefresh;

                try
                {
                    if (session.Expired())
                        shouldRefresh = true;
                }
                catch
                {
                }

                if (!shouldRefresh)
                {
                    try
                    {
                        var expiresAtUtc = session.ExpiresAt();
                        if (expiresAtUtc <= DateTime.UtcNow.AddMinutes(2))
                            shouldRefresh = true;
                    }
                    catch
                    {
                    }
                }

                if (!shouldRefresh)
                    return true;

                try
                {
                    var refreshed = await client.Auth.RefreshSession();
                    if (refreshed != null)
                    {
                        try
                        {
                            _sessionStore?.Save(refreshed);
                        }
                        catch
                        {
                        }
                    }

                    return client.Auth.CurrentSession != null && !(client.Auth.CurrentSession?.Expired() ?? false);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Supabase session refresh failed");
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task SignOutAsync()
        {
            try
            {
                var client = await GetClientAsync();
                await client.Auth.SignOut(global::Supabase.Gotrue.Constants.SignOutScope.Local);
            }
            catch
            {
            }

            try
            {
                _sessionStore?.Clear();
            }
            catch
            {
            }

            CurrentUserId = null;
            IsVorstand = false;
            IsAdmin = false;
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

                try
                {
                    _sessionStore?.Save(session);
                }
                catch
                {
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
                if (!Guid.TryParse(user.Id, out var userGuid))
                {
                    _logger?.LogWarning("User.Id is not a valid Guid: {UserId}", user.Id);
                    IsVorstand = false;
                    IsAdmin = false;
                    return true; // Login ist trotzdem ok, nur keine Rollen
                }

                await ResolveRolesAsync(client, userGuid);

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

        private async Task ResolveRolesAsync(SupabaseClient client, Guid userGuid)
        {
            AppUserRecord? appUser = null;

            try
            {
                appUser = await client
                    .From<AppUserRecord>()
                    .Where(x => x.UserId == userGuid)
                    .Single();
            }
            catch (Exception ex)
            {
                _logger?.LogInformation(ex, "No app_user record found or error while querying for user {UserId}", userGuid);
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