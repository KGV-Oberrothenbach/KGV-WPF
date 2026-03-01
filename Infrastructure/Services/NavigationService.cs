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
        private readonly IAuthService _authService;

        public NavigationService(ISupabaseService supabaseService, IAuthService authService)
        {
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
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
         /// - MemberDetailViewModel(ISupabaseService, IAuthService, MemberDTO)
        /// - NebenmitgliedDetailViewModel(ISupabaseService, IAuthService, NebenmitgliedContext)
        /// - ArbeitsstundenViewModel(ISupabaseService, IAuthService, MemberDTO)
        /// - GartenStromViewModel(ParzellenBelegungDTO)
        /// - GartenWasserViewModel(ParzellenBelegungDTO)
        /// - GartenDokumenteViewModel(ParzellenBelegungDTO)
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

                return new MemberDetailViewModel(_supabaseService, _authService, member);
            }

            if (viewModelType == typeof(NebenmitgliedDetailViewModel))
            {
                if (parameter is not NebenmitgliedContext ctx)
                    return null;

                return new NebenmitgliedDetailViewModel(_supabaseService, _authService, ctx);
            }

            if (viewModelType == typeof(ArbeitsstundenViewModel))
            {
                if (parameter is not MemberDTO member)
                    return null;

                return new ArbeitsstundenViewModel(_supabaseService, _authService, member);
            }

            if (viewModelType == typeof(AdminRoleViewModel))
            {
                if (parameter is not MemberDTO member)
                    return null;

                return new AdminRoleViewModel(_supabaseService, _authService, member);
            }

            if (viewModelType == typeof(GartenStromViewModel))
            {
                if (parameter is not ParzellenBelegungDTO belegung)
                    return null;

                return new GartenStromViewModel(_supabaseService, belegung);
            }

            if (viewModelType == typeof(GartenWasserViewModel))
            {
                if (parameter is not ParzellenBelegungDTO belegung)
                    return null;

                return new GartenWasserViewModel(_supabaseService, belegung);
            }

            if (viewModelType == typeof(GartenDokumenteViewModel))
            {
                if (parameter is not ParzellenBelegungDTO belegung)
                    return null;

                return new GartenDokumenteViewModel(_supabaseService, belegung);
            }

            if (viewModelType == typeof(DokumenteViewModel))
            {
                if (parameter is not DokumenteContext ctx)
                    return null;

                return new DokumenteViewModel(_supabaseService, ctx);
            }

            // Fallback: default ctor
            return Activator.CreateInstance(viewModelType);
        }
    }
}