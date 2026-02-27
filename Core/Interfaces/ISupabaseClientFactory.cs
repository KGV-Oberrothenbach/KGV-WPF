using System.Threading.Tasks;
using Supabase;

namespace KGV.Core.Interfaces
{
    public interface ISupabaseClientFactory
    {
        Task<Client> CreateAsync();
    }
}