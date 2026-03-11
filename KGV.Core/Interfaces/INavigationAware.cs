using System.Threading.Tasks;

namespace KGV.Core.Interfaces
{
    public interface INavigationAware
    {
        Task OnNavigatedToAsync();
        Task OnNavigatedFromAsync();
    }
}
