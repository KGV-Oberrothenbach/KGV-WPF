using System.Threading.Tasks;
using Supabase;

namespace KGV.Core.Interfaces
{
    public interface ISupabaseClientFactory
    {
        string Url { get; }
        string Key { get; }
        Task<Client> CreateAsync();
    }
}
