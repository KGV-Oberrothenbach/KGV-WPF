using System;

namespace KGV.Core.Interfaces
{
    public interface INavigationService
    {
        /// <summary>
        /// LEGACY: View-basierte Navigation (Altbestand).
        /// Nicht mehr empfohlen, bleibt aber für bestehende Stellen erhalten.
        /// </summary>
        object? NavigateTo(Type viewType);

        /// <summary>
        /// ViewModel-first: Erzeugt ein ViewModel (optional mit Parameter).
        /// 'shell' ist dein MainWindowViewModel, damit VMs (wie Suche) darauf zugreifen können.
        /// </summary>
        object? CreateViewModel(Type viewModelType, object shell, object? parameter = null);
    }
}
