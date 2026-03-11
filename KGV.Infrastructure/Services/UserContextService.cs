using KGV.Core.Security;
using KGV.Infrastructure.Models;
using Microsoft.Extensions.Logging;
using Supabase;
using System;
using System.Linq;
using System.Threading.Tasks;
using KGV.Core.Interfaces;

namespace KGV.Infrastructure.Services
{
    public interface IUserContextService
    {
        Task<UserContext> GetUserContextAsync(Guid userId);
    }

    public sealed class UserContextService : IUserContextService
    {
        private readonly ISupabaseClientFactory _clientFactory;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<UserContextService>? _logger;

        public UserContextService(
            ISupabaseClientFactory clientFactory,
            IPermissionService permissionService,
            ILogger<UserContextService>? logger = null)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            _logger = logger;
        }

        public async Task<UserContext> GetUserContextAsync(Guid userId)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("userId must not be empty", nameof(userId));

            try
            {
                Client client = await _clientFactory.CreateAsync();

                var resp = await client
                    .From<AppUserRecord>()
                    .Where(x => x.UserId == userId)
                    .Get();

                var list = resp?.Models?.ToList();
                var record = list?.FirstOrDefault();

                if (record == null)
                {
                    _logger?.LogWarning("No app_user record found for user {UserId}. Falling back to role 'user'.", userId);
                    return _permissionService.CreateContext(userId, UserRoles.User, mitgliedId: null);
                }

                if (list != null && list.Count > 1)
                    _logger?.LogWarning("Multiple app_user records found for user {UserId}. Using the first one.", userId);

                return _permissionService.CreateContext(userId, record.Role, record.MitgliedId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetUserContextAsync failed for user {UserId}", userId);
                throw;
            }
        }
    }
}
