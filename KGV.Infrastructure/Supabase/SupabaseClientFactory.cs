using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Supabase;
using KGV.Core.Interfaces;

// Alias um Konflikt mit Supabase.Postgrest.Client zu vermeiden
using SupabaseClient = Supabase.Client;

namespace KGV.Infrastructure.Supabase
{
    public class SupabaseClientFactory : ISupabaseClientFactory
    {
        private readonly IConfiguration _config;
        private readonly ISupabaseSessionStore? _sessionStore;
        private SupabaseClient? _client;

        public string Url { get; }
        public string Key { get; }

        public SupabaseClientFactory(IConfiguration config, ISupabaseSessionStore? sessionStore = null)
        {
            _config = config;
            _sessionStore = sessionStore;

            Url = _config["Supabase:Url"]
                  ?? throw new InvalidOperationException("Supabase URL fehlt in appsettings.json");
            Key = _config["Supabase:Key"]
                  ?? throw new InvalidOperationException("Supabase Key fehlt in appsettings.json");
        }

        public async Task<SupabaseClient> CreateAsync()
        {
            if (_client != null) return _client;

            var options = new SupabaseOptions
            {
                AutoRefreshToken = true,
                SessionHandler = _sessionStore != null
                    ? new KgvSupabaseSessionHandler(_sessionStore)
                    : new DefaultSupabaseSessionHandler()
            };

            _client = new SupabaseClient(Url, Key, options);
            await _client.InitializeAsync();

            return _client;
        }
    }
}