using KGV.Core.Interfaces;
using KGV.Core.Security;
using KGV.Infrastructure.Authentication;
using KGV.Infrastructure.Services;
using KGV.Infrastructure.Supabase;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace KGV.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddKgvServices(this IServiceCollection services, IConfiguration configuration)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            services.AddSingleton<ISupabaseClientFactory>(_ => new SupabaseClientFactory(configuration));

            services.AddSingleton<IPermissionService, PermissionService>();

            services.AddSingleton<IUserContextService>(sp =>
                new UserContextService(
                    sp.GetRequiredService<ISupabaseClientFactory>(),
                    sp.GetRequiredService<IPermissionService>(),
                    sp.GetService<ILogger<UserContextService>>()));

            services.AddSingleton<IAuthService>(sp =>
                new AuthService(
                    sp.GetRequiredService<ISupabaseClientFactory>(),
                    sp.GetService<ILogger<AuthService>>()));

            services.AddSingleton<ISupabaseService>(sp =>
                new SupabaseService(
                    sp.GetRequiredService<ISupabaseClientFactory>(),
                    sp.GetService<ILogger<SupabaseService>>(),
                    () => sp.GetService<IUserContextAccessor>()?.CurrentUserContext));

            return services;
        }
    }
}
