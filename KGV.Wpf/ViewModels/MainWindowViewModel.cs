// Datei: KGV.Wpf/ViewModels/MainWindowViewModel.cs

using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Core.Security;
using KGV.Wpf.Helpers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Messaging;
using KGV.Wpf.Messages;

namespace KGV.Wpf.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;
        private readonly ISupabaseService _supabaseService;

        public UserContext UserContext { get; }

        private readonly SemaphoreSlim _navLock = new(1, 1);

        private string _updateStatusText = string.Empty;
        public string UpdateStatusText
        {
            get => _updateStatusText;
            set
            {
                if (_updateStatusText == value) return;
                _updateStatusText = value;
                OnPropertyChanged();
            }
        }

        public string AppVersionText { get; }

        // ======= Saison =======
        public ObservableCollection<string> Seasons { get; } = new();

        private string? _selectedSeason;
        public string? SelectedSeason
        {
            get => _selectedSeason;
            set
            {
                if (_selectedSeason == value) return;
                _selectedSeason = value;
                OnPropertyChanged();

                if (int.TryParse(_selectedSeason, out var year))
                    WeakReferenceMessenger.Default.Send(new SeasonChangedMessage(year));
            }
        }

        // ======= Navigation =======
        public ObservableCollection<NavigationItem> NavigationItems { get; } = new();
        public ObservableCollection<NavigationItem> MemberNavigationItems { get; } = new();

        public ICommand NavigateCommand { get; }

        // ======= Rechte =======
        private bool _isAdmin;
        public bool IsAdmin
        {
            get => _isAdmin;
            set
            {
                if (_isAdmin == value) return;
                _isAdmin = value;
                OnPropertyChanged();
                UpdateNavigationVisibility();
                UpdateMemberNavigationVisibility();
            }
        }

        // ======= Current VM (ContentControl) =======
        private BaseViewModel? _currentViewModel;
        public BaseViewModel? CurrentViewModel
        {
            get => _currentViewModel;
            set
            {
                if (_currentViewModel == value) return;
                _currentViewModel = value;
                OnPropertyChanged();
            }
        }

        // ======= Selected Member =======
        private MemberDTO? _selectedMember;
        public MemberDTO? SelectedMember
        {
            get => _selectedMember;
            set
            {
                if (_selectedMember == value) return;

                _selectedMember = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMemberSelected));

                SelectedParzelle = null;
                BuildMemberNavigation();
                UpdateMemberNavigationVisibility();

                // ✅ FIX: Wenn ein Mitglied gewählt wird, muss die Detailansicht (Stammdaten) auch wirklich angezeigt werden.
                // Vorher wurde nur die Navigation/Sidebar aktualisiert, aber NICHT automatisch in MemberDetailViewModel navigiert.
                // Damit bleibt der ContentControl z.B. in "Mitgliedersuche" hängen -> wirkt wie "Details laden nicht".
                if (_selectedMember != null && UserContext.Has(PermissionFlags.CanSearchMembers))
                {
                    // Nur automatisch umschalten, wenn wir gerade in der Suche sind (oder noch nichts angezeigt wird).
                    // Dadurch bleibt die UX stabil, wenn man bewusst in Arbeitsstunden/Dokumente ist.
                    if (CurrentViewModel == null || CurrentViewModel is MemberSearchViewModel)
                    {
                        try
                        {
                            var created = _navigationService.CreateViewModel(typeof(MemberDetailViewModel), this, _selectedMember);
                            if (created is BaseViewModel vm)
                                _ = NavigateToAsync(vm);
                        }
                        catch
                        {
                            // Navigation-Fehler wird an anderer Stelle ohnehin angezeigt (NavigateByItemAsync),
                            // hier nur still, um Setter robust zu halten.
                        }
                    }
                }
            }
        }

        public bool IsMemberSelected => SelectedMember != null;

        private ParzellenBelegungDTO? _selectedParzelle;
        public ParzellenBelegungDTO? SelectedParzelle
        {
            get => _selectedParzelle;
            private set
            {
                if (_selectedParzelle == value) return;
                _selectedParzelle = value;
                OnPropertyChanged();
            }
        }

        public IAuthService AuthService => _authService;

        public MainWindowViewModel(
            IAuthService authService,
            INavigationService navigationService,
            ISupabaseService supabaseService,
            UserContext userContext)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
            UserContext = userContext ?? throw new ArgumentNullException(nameof(userContext));

            AppVersionText = BuildVersionText();

            NavigateCommand = new RelayCommand<NavigationItem>(item => _ = NavigateByItemAsync(item));

            _ = LoadSeasonsAsync();
            BuildNavigation();
            BuildMemberNavigation();
            UpdateNavigationVisibility();
            UpdateMemberNavigationVisibility();

            IsAdmin = UserContext.Has(PermissionFlags.CanEditAllMembers);

            WeakReferenceMessenger.Default.Register<ParzelleSelectedMessage>(this, (_, msg) =>
                _ = OnParzelleSelectedAsync(msg.Belegung));

            WeakReferenceMessenger.Default.Register<ParzelleContextChangedMessage>(this, (_, msg) =>
                OnParzelleContextChanged(msg.Belegung));

            WeakReferenceMessenger.Default.Register<NebenmitgliedSelectedMessage>(this, (_, msg) =>
                _ = OnNebenmitgliedSelectedAsync(msg.Context));

            WeakReferenceMessenger.Default.Register<NavigateToViewModelMessage>(this, (_, msg) =>
                _ = OnNavigateToViewModelRequestedAsync(msg));

            // Neutraler Start: Landing-Page (kein fachlicher Bereich als Default)
            if (UserContext.Has(PermissionFlags.CanSeeOwnDataOnly)
                && UserContext.MitgliedId.HasValue
                && UserContext.MitgliedId.Value > 0
                && UserContext.MitgliedId.Value <= int.MaxValue)
            {
                // Minimaler Kontext-Placeholder, damit "Meine Daten" navigierbar bleibt.
                SelectedMember ??= new MemberDTO { Id = (int)UserContext.MitgliedId.Value };
            }

            _ = NavigateToAsync(new HomeViewModel(_supabaseService, UserContext));
        }

        private static string BuildVersionText()
        {
            try
            {
                var v = Assembly.GetEntryAssembly()?.GetName().Version
                        ?? Assembly.GetExecutingAssembly().GetName().Version
                        ?? new Version(0, 0, 0, 0);

                var build = v.Build >= 0 ? v.Build : 0;
                return $"Version {v.Major}.{v.Minor}.{build}";
            }
            catch
            {
                return "Version ?";
            }
        }

        private async Task InitializeMyDataAsync()
        {
            try
            {
                if (!UserContext.MitgliedId.HasValue)
                    return;

                if (UserContext.MitgliedId.Value > int.MaxValue)
                    return;

                var myId = (int)UserContext.MitgliedId.Value;

                // Minimalen Placeholder setzen, damit Navigation ("Meine Daten") nicht leer läuft,
                // selbst wenn der Detail-Load fehlschlägt.
                SelectedMember ??= new MemberDTO { Id = myId };

                var rec = await _supabaseService.GetMitgliedByIdAsync(myId);
                if (rec == null)
                    return;

                var dto = MapToDTO(rec);
                SelectedMember = dto.Clone();

                var created = _navigationService.CreateViewModel(typeof(MemberDetailViewModel), this, SelectedMember);
                if (created is BaseViewModel vm)
                    await NavigateToAsync(vm);
            }
            catch
            {
            }
        }

        private async Task OnNebenmitgliedSelectedAsync(NebenmitgliedContext ctx)
        {
            var created = _navigationService.CreateViewModel(typeof(NebenmitgliedDetailViewModel), this, ctx);
            if (created is BaseViewModel vm)
                await NavigateToAsync(vm);
        }

        private async Task OnNavigateToViewModelRequestedAsync(NavigateToViewModelMessage msg)
        {
            if (msg?.ViewModelType == null) return;

            try
            {
                // Wenn explizit ein MemberDTO als Parameter mitkommt (z.B. "Zum Hauptmitglied"),
                // dann muss der globale Kontext (Sidebar/Member-Menü) mitgezogen werden.
                if (msg.ViewModelType == typeof(MemberDetailViewModel) && msg.Parameter is MemberDTO md)
                    SelectedMember = md;

                var created = _navigationService.CreateViewModel(msg.ViewModelType, this, msg.Parameter);
                if (created is BaseViewModel vm)
                    await NavigateToAsync(vm);
            }
            catch
            {
                // Navigation-Fehler werden in NavigateByItemAsync mit UI-Dialog behandelt.
                // Hier bewusst still, damit Messages keine App-crashs verursachen.
            }
        }

        private void OnParzelleContextChanged(ParzellenBelegungDTO? belegung)
        {
            if (SelectedMember == null)
                return;

            SelectedParzelle = belegung;
            BuildMemberNavigation();
            UpdateMemberNavigationVisibility();
        }

        private void SeedSeasons()
        {
            // Legacy fallback: keeps UI usable if DB is offline during startup.
            if (Seasons.Count > 0) return;

            var year = DateTime.Today.Year;
            Seasons.Add(year.ToString());
            SelectedSeason = year.ToString();
        }

        private async Task LoadSeasonsAsync()
        {
            try
            {
                var saisonen = await _supabaseService.GetSaisonRecordsAsync();
                if (saisonen == null || saisonen.Count == 0)
                {
                    SeedSeasons();
                    return;
                }

                var years = saisonen
                    .Select(x => x.Jahr)
                    .Distinct()
                    .OrderByDescending(x => x)
                    .ToList();

                Seasons.Clear();
                foreach (var y in years)
                    Seasons.Add(y.ToString());

                var todayYear = DateTime.Today.Year;
                SelectedSeason = years.Contains(todayYear) ? todayYear.ToString() : years.First().ToString();
            }
            catch
            {
                SeedSeasons();
            }
        }

        private void BuildNavigation()
        {
            NavigationItems.Clear();

            NavigationItems.Add(new NavigationItem
            {
                Title = "Start",
                ViewModelType = typeof(HomeViewModel),
                IsVisible = true
            });

            if (UserContext.Has(PermissionFlags.CanSearchMembers))
            {
                // Mitgliedersuche
                NavigationItems.Add(new NavigationItem
                {
                    Title = "Mitgliedersuche",
                    ViewModelType = typeof(MemberSearchViewModel),
                    IsVisible = true
                });
            }

            if (UserContext.Has(PermissionFlags.CanSeeOwnDataOnly))
            {
                NavigationItems.Add(new NavigationItem
                {
                    Title = "Meine Daten",
                    ViewModelType = typeof(MemberDetailViewModel),
                    IsVisible = true
                });
            }

            if (UserContext.Has(PermissionFlags.CanManageReadings))
            {
                NavigationItems.Add(new NavigationItem
                {
                    Title = "Ablesen",
                    ViewModelType = typeof(AblesenViewModel),
                    IsVisible = true
                });
            }

            if (UserContext.Has(PermissionFlags.CanManageWorkHours))
            {
                NavigationItems.Add(new NavigationItem
                {
                    Title = "Saison",
                    ViewModelType = typeof(SaisonViewModel),
                    IsVisible = true
                });
            }

            // Export (immer sichtbar)
            NavigationItems.Add(new NavigationItem
            {
                Title = "Export",
                ViewModelType = typeof(ExportViewModel),
                IsVisible = true
            });

            NavigationItems.Add(new NavigationItem
            {
                Title = "Info / Impressum",
                ViewModelType = typeof(ImpressumViewModel),
                IsVisible = true
            });
        }

        private void BuildMemberNavigation()
        {
            MemberNavigationItems.Clear();

            // Stammdaten bearbeiten (Detail)
            MemberNavigationItems.Add(new NavigationItem
            {
                Title = "Stammdaten",
                ViewModelType = typeof(MemberDetailViewModel),
                IsVisible = SelectedMember != null
            });

            MemberNavigationItems.Add(new NavigationItem
            {
                Title = "Arbeitsstunden",
                ViewModelType = typeof(ArbeitsstundenViewModel),
                IsVisible = SelectedMember != null && UserContext.Has(PermissionFlags.CanManageWorkHours),
                ButtonMargin = new System.Windows.Thickness(5)
            });

            MemberNavigationItems.Add(new NavigationItem
            {
                Title = "Dokumente",
                ViewModelType = typeof(DokumenteViewModel),
                IsVisible = SelectedMember != null && UserContext.Has(PermissionFlags.CanManageDocuments),
                ButtonMargin = new System.Windows.Thickness(5)
            });

            // Admin-Menü nur im Mitglied-Kontext
            MemberNavigationItems.Add(new NavigationItem
            {
                Title = "Admin-Menü",
                ViewModelType = typeof(AdminRoleViewModel),
                IsAdminOnly = true,
                IsVisible = SelectedMember != null
            });

            if (SelectedMember == null || SelectedParzelle == null)
                return;

            // Überschrift (nicht klickbar)
            MemberNavigationItems.Add(new NavigationItem
            {
                Title = $"Garten Nr. {SelectedParzelle.GartenNr}",
                ViewModelType = null,
                IsVisible = true,
                ButtonMargin = new System.Windows.Thickness(5, 12, 5, 4)
            });

            MemberNavigationItems.Add(new NavigationItem
            {
                Title = "Strom",
                ViewModelType = typeof(GartenStromViewModel),
                Parameter = SelectedParzelle,
                IsVisible = true,
                ButtonMargin = new System.Windows.Thickness(25, 5, 5, 5)
            });

            MemberNavigationItems.Add(new NavigationItem
            {
                Title = "Wasser",
                ViewModelType = typeof(GartenWasserViewModel),
                Parameter = SelectedParzelle,
                IsVisible = true,
                ButtonMargin = new System.Windows.Thickness(25, 5, 5, 5)
            });

            MemberNavigationItems.Add(new NavigationItem
            {
                Title = "Dokumente",
                ViewModelType = typeof(GartenDokumenteViewModel),
                Parameter = SelectedParzelle,
                IsVisible = true,
                ButtonMargin = new System.Windows.Thickness(25, 5, 5, 5)
            });
        }

        private static MemberDTO MapToDTO(MitgliedRecord m)
        {
            return new MemberDTO
            {
                Id = m.Id,
                Vorname = m.Vorname ?? string.Empty,
                Nachname = m.Name ?? string.Empty,
                Email = m.Email ?? string.Empty,
                Role = m.Role ?? string.Empty,
                AuthUserId = m.AuthUserId,
                ArbeitsstundenAltersregelTyp = m.ArbeitsstundenAltersregelTyp ?? "keine"
            };
        }

        private async Task OnParzelleSelectedAsync(ParzellenBelegungDTO belegung)
        {
            if (SelectedMember == null)
                return;

            SelectedParzelle = belegung;
            BuildMemberNavigation();
            UpdateMemberNavigationVisibility();

            // Default: nach Doppelklick direkt in Strom-Ansicht springen
            var created = _navigationService.CreateViewModel(typeof(GartenStromViewModel), this, SelectedParzelle);
            if (created is BaseViewModel vm)
                await NavigateToAsync(vm);
        }

        private void UpdateNavigationVisibility()
        {
            foreach (var item in NavigationItems)
            {
                item.IsVisible = !item.IsAdminOnly || IsAdmin;
            }

            // Refresh für UI (NavigationItem hat kein INotifyPropertyChanged)
            OnPropertyChanged(nameof(NavigationItems));
        }

        private void UpdateMemberNavigationVisibility()
        {
            foreach (var item in MemberNavigationItems)
            {
                if (SelectedMember == null)
                {
                    item.IsVisible = false;
                    continue;
                }

                var visible = true;

                if (item.IsAdminOnly)
                    visible = IsAdmin;

                if (item.ViewModelType == typeof(ArbeitsstundenViewModel))
                    visible = visible && UserContext.Has(PermissionFlags.CanManageWorkHours);

                if (item.ViewModelType == typeof(DokumenteViewModel))
                    visible = visible && UserContext.Has(PermissionFlags.CanManageDocuments);

                if (item.ViewModelType == typeof(GartenStromViewModel) ||
                    item.ViewModelType == typeof(GartenWasserViewModel) ||
                    item.ViewModelType == typeof(GartenDokumenteViewModel))
                    visible = visible && SelectedParzelle != null;

                // Überschrift "Garten Nr..." (nicht klickbar)
                if (item.ViewModelType == null)
                    visible = SelectedParzelle != null;

                item.IsVisible = visible;
            }

            OnPropertyChanged(nameof(MemberNavigationItems));
        }

        private async Task NavigateByItemAsync(NavigationItem? item)
        {
            if (item == null) return;
            if (!item.IsVisible) return;
            if (item.ViewModelType == null) return;

            try
            {
                object? parameter = item.Parameter;

                // MemberDetail braucht MemberDTO
                if (item.ViewModelType == typeof(MemberDetailViewModel))
                {
                    if (SelectedMember == null) return;
                    parameter = SelectedMember;
                }

                if (item.ViewModelType == typeof(ArbeitsstundenViewModel))
                {
                    if (SelectedMember == null) return;
                    parameter = SelectedMember;
                }

                if (item.ViewModelType == typeof(DokumenteViewModel))
                {
                    if (SelectedMember == null) return;
                    parameter = new DokumenteContext(SelectedMember, null);
                }

                if (item.ViewModelType == typeof(AdminRoleViewModel))
                {
                    if (SelectedMember == null) return;
                    parameter = SelectedMember;
                }

                var created = _navigationService.CreateViewModel(item.ViewModelType, this, parameter);
                if (created is not BaseViewModel vm)
                {
                    System.Windows.MessageBox.Show(
                        $"Navigation fehlgeschlagen: {item.Title}\nViewModel: {item.ViewModelType.Name}\nParameter: {(parameter == null ? "<null>" : parameter.GetType().Name)}",
                        "Fehler",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }

                await NavigateToAsync(vm);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Navigation fehlgeschlagen: {item.Title}\n{ex.Message}",
                    "Fehler",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Navigation inkl. Lifecycle (OnNavigatedFrom/To) wenn ViewModels INavigationAware implementieren.
        /// </summary>
        public async Task NavigateToAsync(BaseViewModel viewModel)
        {
            if (viewModel == null) return;

            await _navLock.WaitAsync();
            try
            {
                if (CurrentViewModel is INavigationAware oldVm)
                    await oldVm.OnNavigatedFromAsync();

                CurrentViewModel = viewModel;

                if (viewModel is INavigationAware newVm)
                    await newVm.OnNavigatedToAsync();
            }
            finally
            {
                _navLock.Release();
            }
        }

        public void NavigateTo(BaseViewModel viewModel)
        {
            _ = NavigateToAsync(viewModel);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}