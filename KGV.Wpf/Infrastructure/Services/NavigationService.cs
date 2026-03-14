using System;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Wpf.ViewModels;

namespace KGV.Wpf.Infrastructure.Services
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

            return view as UserControl ?? view;
        }

        /// <summary>
        /// ViewModel-first Factory.
        /// Unterstützt gezielt die ViewModels, die wir nutzen.
        /// WICHTIG: Parameter können je nach Aufrufer fehlen oder falsch typisiert sein.
        /// Dann wird versucht, den Kontext aus MainWindowViewModel zu ermitteln (z.B. SelectedMember).
        /// </summary>
        public object? CreateViewModel(Type viewModelType, object shell, object? parameter = null)
        {
            if (viewModelType == null) return null;

            if (shell is not MainWindowViewModel mainVm)
                throw new ArgumentException("shell muss MainWindowViewModel sein.", nameof(shell));

            // Hilfsfunktionen, damit wir nicht 'silent null' zurückgeben
            MemberDTO? ResolveMember()
            {
                if (parameter is MemberDTO m) return m;

                // häufige Fallstricke: parameter ist ein Wrapper/VM/NavigationItem -> versuchen aus MainVM zu holen
                var fromMain = TryGetMemberFromMainWindow(mainVm);
                return fromMain;
            }

            NebenmitgliedContext? ResolveNebenmitgliedContext()
            {
                if (parameter is NebenmitgliedContext ctx) return ctx;
                return TryGetFromMainWindow<NebenmitgliedContext>(mainVm, "NebenmitgliedContext", "SelectedNebenmitgliedContext", "CurrentNebenmitgliedContext");
            }

            ParzellenBelegungDTO? ResolveBelegung()
            {
                if (parameter is ParzellenBelegungDTO b) return b;
                return TryGetFromMainWindow<ParzellenBelegungDTO>(mainVm, "SelectedBelegung", "CurrentBelegung", "ParzellenBelegung", "SelectedParzellenBelegung");
            }

            DokumenteContext? ResolveDokumenteContext()
            {
                if (parameter is DokumenteContext d) return d;
                return TryGetFromMainWindow<DokumenteContext>(mainVm, "DokumenteContext", "SelectedDokumenteContext", "CurrentDokumenteContext");
            }

            // Suche
            if (viewModelType == typeof(MemberSearchViewModel))
            {
                return new MemberSearchViewModel(_supabaseService, mainVm);
            }

            if (viewModelType == typeof(HomeViewModel))
            {
                return new HomeViewModel(_supabaseService, mainVm.UserContext);
            }

            if (viewModelType == typeof(BekanntmachungenVerwaltungViewModel))
            {
                return new BekanntmachungenVerwaltungViewModel(_supabaseService, mainVm.UserContext);
            }

            if (viewModelType == typeof(TermineVerwaltungViewModel))
            {
                return new TermineVerwaltungViewModel(_supabaseService, mainVm.UserContext);
            }

            if (viewModelType == typeof(ArbeitseinsaetzeVerwaltungViewModel))
            {
                return new ArbeitseinsaetzeVerwaltungViewModel(_supabaseService, mainVm.UserContext);
            }

            if (viewModelType == typeof(SaisonViewModel))
            {
                return new SaisonViewModel(_supabaseService);
            }

            if (viewModelType == typeof(WartungsvertraegeVerwaltungViewModel))
            {
                return new WartungsvertraegeVerwaltungViewModel(_supabaseService, mainVm.UserContext);
            }

            if (viewModelType == typeof(AblesungErfassenViewModel))
            {
                return new AblesungErfassenViewModel(_supabaseService);
            }

            if (viewModelType == typeof(RfidEinrichtenViewModel))
            {
                return new RfidEinrichtenViewModel(_supabaseService);
            }

            if (viewModelType == typeof(FaelligeZaehlerViewModel))
            {
                return new FaelligeZaehlerViewModel(_supabaseService);
            }

            if (viewModelType == typeof(ZaehlerwechselScanViewModel))
            {
                return new ZaehlerwechselScanViewModel(_supabaseService);
            }

            if (viewModelType == typeof(ZaehlerwechselAusbauViewModel))
            {
                if (parameter is not RfidScanContextRecord ctx)
                    throw new InvalidOperationException("Navigation zu ZaehlerwechselAusbauViewModel ohne RfidScanContextRecord.");

                return new ZaehlerwechselAusbauViewModel(_supabaseService, ctx);
            }

            if (viewModelType == typeof(ZaehlerwechselEinbauViewModel))
            {
                if (parameter is not RfidScanContextRecord ctx)
                    throw new InvalidOperationException("Navigation zu ZaehlerwechselEinbauViewModel ohne RfidScanContextRecord.");

                return new ZaehlerwechselEinbauViewModel(_supabaseService, ctx);
            }

            if (viewModelType == typeof(RfidScanContextViewModel))
            {
                if (parameter is not RfidScanContextRecord ctx)
                    throw new InvalidOperationException("Navigation zu RfidScanContextViewModel ohne RfidScanContextRecord.");

                return new RfidScanContextViewModel(_supabaseService, ctx);
            }

            // Detail
            if (viewModelType == typeof(MemberDetailViewModel))
            {
                var member = ResolveMember();
                if (member == null)
                    throw new InvalidOperationException(
                        "Navigation zu MemberDetailViewModel ohne MemberDTO. " +
                        "Weder parameter war MemberDTO noch konnte ein SelectedMember im MainWindowViewModel gefunden werden.");

                return new MemberDetailViewModel(_supabaseService, _authService, member);
            }

            if (viewModelType == typeof(NebenmitgliedDetailViewModel))
            {
                var ctx = ResolveNebenmitgliedContext();
                if (ctx == null)
                    throw new InvalidOperationException(
                        "Navigation zu NebenmitgliedDetailViewModel ohne NebenmitgliedContext. " +
                        "Bitte parameter übergeben oder im MainWindowViewModel bereitstellen (z.B. SelectedNebenmitgliedContext).");

                return new NebenmitgliedDetailViewModel(_supabaseService, _authService, ctx);
            }

            if (viewModelType == typeof(ArbeitsstundenViewModel))
            {
                var member = ResolveMember();
                if (member == null)
                    throw new InvalidOperationException(
                        "Navigation zu ArbeitsstundenViewModel ohne MemberDTO. " +
                        "Bitte parameter übergeben oder SelectedMember im MainWindowViewModel setzen.");

                return new ArbeitsstundenViewModel(_supabaseService, _authService, member);
            }

            if (viewModelType == typeof(MemberWartungsvertraegeViewModel))
            {
                var member = ResolveMember();
                if (member == null)
                    throw new InvalidOperationException(
                        "Navigation zu MemberWartungsvertraegeViewModel ohne MemberDTO. " +
                        "Bitte parameter übergeben oder SelectedMember im MainWindowViewModel setzen.");

                return new MemberWartungsvertraegeViewModel(_supabaseService, mainVm.UserContext, member);
            }

            if (viewModelType == typeof(AdminRoleViewModel))
            {
                var member = ResolveMember();
                if (member == null)
                    throw new InvalidOperationException(
                        "Navigation zu AdminRoleViewModel ohne MemberDTO. " +
                        "Bitte parameter übergeben oder SelectedMember im MainWindowViewModel setzen.");

                return new AdminRoleViewModel(_supabaseService, _authService, member);
            }

            if (viewModelType == typeof(UserManagementViewModel))
            {
                return new UserManagementViewModel(_supabaseService, _authService, mainVm);
            }

            if (viewModelType == typeof(GartenStromViewModel))
            {
                var belegung = ResolveBelegung();
                if (belegung == null)
                    throw new InvalidOperationException(
                        "Navigation zu GartenStromViewModel ohne ParzellenBelegungDTO. " +
                        "Bitte parameter übergeben oder SelectedBelegung im MainWindowViewModel setzen.");

                return new GartenStromViewModel(_supabaseService, belegung);
            }

            if (viewModelType == typeof(GartenWasserViewModel))
            {
                var belegung = ResolveBelegung();
                if (belegung == null)
                    throw new InvalidOperationException(
                        "Navigation zu GartenWasserViewModel ohne ParzellenBelegungDTO. " +
                        "Bitte parameter übergeben oder SelectedBelegung im MainWindowViewModel setzen.");

                return new GartenWasserViewModel(_supabaseService, belegung);
            }

            if (viewModelType == typeof(GartenDokumenteViewModel))
            {
                var belegung = ResolveBelegung();
                if (belegung == null)
                    throw new InvalidOperationException(
                        "Navigation zu GartenDokumenteViewModel ohne ParzellenBelegungDTO. " +
                        "Bitte parameter übergeben oder SelectedBelegung im MainWindowViewModel setzen.");

                return new GartenDokumenteViewModel(_supabaseService, belegung);
            }

            if (viewModelType == typeof(DokumenteViewModel))
            {
                var ctx = ResolveDokumenteContext();
                if (ctx == null)
                    throw new InvalidOperationException(
                        "Navigation zu DokumenteViewModel ohne DokumenteContext. " +
                        "Bitte parameter übergeben oder DokumenteContext im MainWindowViewModel bereitstellen.");

                return new DokumenteViewModel(_supabaseService, ctx);
            }

            if (viewModelType == typeof(ExportViewModel))
            {
                // hier bleibt wie gehabt: UserContext muss im MainVM vorhanden sein
                return new ExportViewModel(_supabaseService, mainVm.UserContext);
            }

            if (viewModelType == typeof(ImpressumViewModel))
            {
                return new ImpressumViewModel(_supabaseService, mainVm.UserContext);
            }

            // Fallback: default ctor
            return Activator.CreateInstance(viewModelType);
        }

        private static MemberDTO? TryGetMemberFromMainWindow(MainWindowViewModel mainVm)
        {
            // Wir versuchen mehrere typische Property-Namen aus Alt-/Umbau-Ständen.
            // KEIN harter Compile-Abhängigkeit, damit wir nichts kaputt machen.
            var candidates = new[]
            {
                "SelectedMember",
                "SelectedMemberDto",
                "CurrentMember",
                "CurrentMemberDto",
                "Member",
                "MemberDto"
            };

            // 1) Direkt MemberDTO Property
            var direct = TryGetFromMainWindow<MemberDTO>(mainVm, candidates);
            if (direct != null) return direct;

            // 2) Falls SelectedMember ein VM ist (z.B. MemberViewModel), versuche Property "Dto" oder "Member"
            object? selectedObj = TryGetFromMainWindowObject(mainVm, candidates);
            if (selectedObj == null) return null;

            var dtoProp = selectedObj.GetType().GetProperty("Dto", BindingFlags.Instance | BindingFlags.Public);
            if (dtoProp != null && typeof(MemberDTO).IsAssignableFrom(dtoProp.PropertyType))
                return dtoProp.GetValue(selectedObj) as MemberDTO;

            var memberProp = selectedObj.GetType().GetProperty("Member", BindingFlags.Instance | BindingFlags.Public);
            if (memberProp != null && typeof(MemberDTO).IsAssignableFrom(memberProp.PropertyType))
                return memberProp.GetValue(selectedObj) as MemberDTO;

            return null;
        }

        private static T? TryGetFromMainWindow<T>(MainWindowViewModel mainVm, params string[] propertyNames) where T : class
        {
            foreach (var name in propertyNames)
            {
                var pi = mainVm.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (pi == null) continue;

                var val = pi.GetValue(mainVm);
                if (val is T typed) return typed;
            }

            return null;
        }

        private static object? TryGetFromMainWindowObject(MainWindowViewModel mainVm, params string[] propertyNames)
        {
            foreach (var name in propertyNames)
            {
                var pi = mainVm.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (pi == null) continue;

                var val = pi.GetValue(mainVm);
                if (val != null) return val;
            }

            return null;
        }
    }
}