using System;
using System.Windows.Controls;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.ViewModels;

namespace KGV.Infrastructure.Services
{
    public class NavigationService : INavigationService
    {
        private readonly ISupabaseService _supabaseService;

        public NavigationService(ISupabaseService supabaseService)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        }

        /// <summary>
        /// LEGACY: View-basierte Navigation (Altbestand).
        /// Wird perspektivisch entfernt, bleibt aber kompatibel.
        /// </summary>
        public object? NavigateTo(Type viewType)
        {
            if (viewType == null) return null;

            var view = Activator.CreateInstance(viewType);

            if (view is UserControl uc)
                return uc;

            return view;
        }

        /// <summary>
        /// ViewModel-first Factory.
        /// Unterstützt gezielt die ViewModels, die wir gerade nutzen:
        /// - MemberSearchViewModel(ISupabaseService, MainWindowViewModel)
        /// - MemberDetailViewModel(ISupabaseService, MemberDTO)
        /// - Fallback: parameterloser Konstruktor
        /// </summary>
        public object? CreateViewModel(Type viewModelType, object shell, object? parameter = null)
        {
            if (viewModelType == null) return null;

            if (shell is not MainWindowViewModel mainVm)
                throw new ArgumentException("shell muss MainWindowViewModel sein.", nameof(shell));

            // Suche
            if (viewModelType == typeof(MemberSearchViewModel))
            {
                return new MemberSearchViewModel(_supabaseService, mainVm);
            }

            // Detail
            if (viewModelType == typeof(MemberDetailViewModel))
            {
                if (parameter is not MemberDTO member)
                    return null;

                return new MemberDetailViewModel(_supabaseService, member);
            }

            // Fallback: default ctor
            return Activator.CreateInstance(viewModelType);
        }
    }
}