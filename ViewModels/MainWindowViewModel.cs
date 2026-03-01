using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Helpers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Messaging;
using KGV.Messages;

namespace KGV.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly IAuthService _authService;
        private readonly INavigationService _navigationService;
        private readonly ISupabaseService _supabaseService;

        private readonly SemaphoreSlim _navLock = new(1, 1);

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
            ISupabaseService supabaseService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));

            NavigateCommand = new RelayCommand<NavigationItem>(item => _ = NavigateByItemAsync(item));

            SeedSeasons();
            BuildNavigation();
            BuildMemberNavigation();
            UpdateNavigationVisibility();
            UpdateMemberNavigationVisibility();

            IsAdmin = _authService.IsAdmin;

            WeakReferenceMessenger.Default.Register<ParzelleSelectedMessage>(this, (_, msg) =>
                _ = OnParzelleSelectedAsync(msg.Belegung));

            WeakReferenceMessenger.Default.Register<NebenmitgliedSelectedMessage>(this, (_, msg) =>
                _ = OnNebenmitgliedSelectedAsync(msg.Context));

            // Start: Mitgliedersuche öffnen
            _ = NavigateToAsync((BaseViewModel)_navigationService.CreateViewModel(typeof(MemberSearchViewModel), this)!);
        }

        private async Task OnNebenmitgliedSelectedAsync(NebenmitgliedContext ctx)
        {
            var created = _navigationService.CreateViewModel(typeof(NebenmitgliedDetailViewModel), this, ctx);
            if (created is BaseViewModel vm)
                await NavigateToAsync(vm);
        }

        private void SeedSeasons()
        {
            if (Seasons.Count > 0) return;

            Seasons.Add("2024");
            Seasons.Add("2025");
            Seasons.Add("2026");
            SelectedSeason = "2026";
        }

        private void BuildNavigation()
        {
            NavigationItems.Clear();

            // Mitgliedersuche
            NavigationItems.Add(new NavigationItem
            {
                Title = "Mitgliedersuche",
                ViewModelType = typeof(MemberSearchViewModel),
                IsVisible = true
            });

            // Admin-Menü wird nur im Mitglied-Kontext angeboten (siehe MemberNavigationItems)
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
                IsVisible = SelectedMember != null,
                ButtonMargin = new System.Windows.Thickness(5)
            });

            MemberNavigationItems.Add(new NavigationItem
            {
                Title = "Dokumente",
                ViewModelType = typeof(DokumenteViewModel),
                IsVisible = SelectedMember != null,
                ButtonMargin = new System.Windows.Thickness(5)
            });

            // Admin-Menü nur im Mitglied-Kontext
            MemberNavigationItems.Add(new NavigationItem
            {
                Title = "Admin-Menü",
                ViewModelType = typeof(AdminRoleViewModel),
                IsVisible = SelectedMember != null && IsAdmin
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
                item.IsVisible = SelectedMember != null;
            }

            OnPropertyChanged(nameof(MemberNavigationItems));
        }

        private async Task NavigateByItemAsync(NavigationItem? item)
        {
            if (item == null) return;
            if (!item.IsVisible) return;
            if (item.ViewModelType == null) return;

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
            if (created is BaseViewModel vm)
                await NavigateToAsync(vm);
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