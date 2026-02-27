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
        private SupabaseClient? _client;

        public SupabaseClientFactory(IConfiguration config)
        {
            _config = config;
        }

        public async Task<SupabaseClient> CreateAsync()
        {
            if (_client != null) return _client;

            var url = _config["Supabase:Url"]
                      ?? throw new InvalidOperationException("Supabase URL fehlt in appsettings.json");
            var key = _config["Supabase:Key"]
                      ?? throw new InvalidOperationException("Supabase Key fehlt in appsettings.json");

            _client = new SupabaseClient(url, key);
            await _client.InitializeAsync();

            return _client;
        }
    }
}