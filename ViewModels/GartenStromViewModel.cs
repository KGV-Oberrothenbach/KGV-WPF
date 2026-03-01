// File: ViewModels/GartenStromViewModel.cs
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Helpers;
using KGV.Views;
using System;
using System.Windows;
using System.Diagnostics;
using System.Linq;

namespace KGV.ViewModels
{
    public sealed class GartenStromViewModel : BaseViewModel, INavigationAware
    {
        private const short ZaehlerTypStrom = 1;
        private readonly ISupabaseService _supabaseService;

        public ParzellenBelegungDTO Belegung { get; }

        public string GartenNr => Belegung.GartenNr;

        public ObservableCollection<ZaehlerAblesungDTO> Ablesungen { get; } = new();

        public RelayCommand<object?> ZaehlerTauschCommand { get; }
        public RelayCommand<object?> NeueAblesungCommand { get; }
        public RelayCommand<object?> AblesungBearbeitenCommand { get; }
        public RelayCommand<object?> OpenFotoCommand { get; }

        private ZaehlerAblesungDTO? _selectedAblesung;
        public ZaehlerAblesungDTO? SelectedAblesung
        {
            get => _selectedAblesung;
            set
            {
                if (_selectedAblesung == value) return;
                _selectedAblesung = value;
                OnPropertyChanged();
                AblesungBearbeitenCommand.RaiseCanExecuteChanged();
            }
        }

        public GartenStromViewModel(ISupabaseService supabaseService, ParzellenBelegungDTO belegung)
        {
            _supabaseService = supabaseService;
            Belegung = belegung;

            ZaehlerTauschCommand = new RelayCommand<object?>(_ => _ = ZaehlerTauschAsync());
            NeueAblesungCommand = new RelayCommand<object?>(_ => _ = NeueAblesungAsync());
            AblesungBearbeitenCommand = new RelayCommand<object?>(_ => _ = AblesungBearbeitenAsync(), _ => SelectedAblesung != null);
            OpenFotoCommand = new RelayCommand<object?>(p => OpenFoto(p));
        }

        public async Task OnNavigatedToAsync()
        {
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            var items = await _supabaseService.GetStromAblesungenAsync(Belegung.ParzelleId);
            Ablesungen.Clear();
            foreach (var i in items)
                Ablesungen.Add(i);
        }

        private void OpenFoto(object? parameter)
        {
            var url = parameter as string;
            if (string.IsNullOrWhiteSpace(url)) return;

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
            }
        }

        private async Task NeueAblesungAsync()
        {
            var dlg = new AblesungDialog
            {
                Owner = Application.Current?.MainWindow,
                Title = $"Neue Ablesung (Strom) - Garten Nr. {GartenNr}"
            };

            if (dlg.ShowDialog() != true)
                return;

            if (!dlg.Ablesedatum.HasValue || !dlg.Stand.HasValue)
            {
                MessageBox.Show("Bitte Ablesedatum und Zählerstand angeben.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var meter = await _supabaseService.GetActiveStromzaehlerAsync(Belegung.ParzelleId, dlg.Ablesedatum.Value);
            if (meter == null)
            {
                MessageBox.Show("Kein aktiver Stromzähler für dieses Datum gefunden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var ok = await _supabaseService.AddAblesungAsync(ZaehlerTypStrom, meter.Id, dlg.Ablesedatum.Value, dlg.Stand.Value, string.IsNullOrWhiteSpace(dlg.FotoPfad) ? null : dlg.FotoPfad.Trim());
            if (!ok)
            {
                MessageBox.Show("Ablesung konnte nicht gespeichert werden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await LoadAsync();
        }

        private async Task AblesungBearbeitenAsync()
        {
            if (SelectedAblesung == null)
                return;

            var dlg = new AblesungDialog
            {
                Owner = Application.Current?.MainWindow,
                Title = $"Ablesung bearbeiten (Strom) - Garten Nr. {GartenNr}"
            };
            dlg.SetInitialValues(SelectedAblesung.Ablesedatum, SelectedAblesung.Stand, SelectedAblesung.FotoPfad);

            if (dlg.ShowDialog() != true)
                return;

            if (!dlg.Ablesedatum.HasValue || !dlg.Stand.HasValue)
            {
                MessageBox.Show("Bitte Ablesedatum und Zählerstand angeben.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var ok = await _supabaseService.UpdateAblesungAsync(SelectedAblesung.AblesungId, dlg.Ablesedatum.Value, dlg.Stand.Value, string.IsNullOrWhiteSpace(dlg.FotoPfad) ? null : dlg.FotoPfad.Trim());
            if (!ok)
            {
                MessageBox.Show("Ablesung konnte nicht gespeichert werden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await LoadAsync();
        }

        private async Task ZaehlerTauschAsync()
        {
            try
            {
                var dlg = new ZaehlerTauschDialog
                {
                    Owner = Application.Current?.MainWindow,
                    Title = $"Stromzähler tauschen (Garten Nr. {GartenNr})"
                };

                if (dlg.ShowDialog() != true)
                    return;

                if (string.IsNullOrWhiteSpace(dlg.Zaehlernummer) || !dlg.Eichdatum.HasValue || !dlg.EingebautAm.HasValue)
                {
                    MessageBox.Show("Bitte Zählernummer, Eichdatum und Einbaudatum angeben.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Strom: Ausbau- und Einbaudatum identisch
                var changeDate = dlg.EingebautAm.Value.Date;
                var current = await _supabaseService.GetActiveStromzaehlerAsync(Belegung.ParzelleId, changeDate);
                if (current != null)
                {
                    var ended = await _supabaseService.SetStromzaehlerAusgebautAmAsync(current.Id, changeDate);
                    if (!ended)
                    {
                        MessageBox.Show("Alter Zähler konnte nicht ausgebaut werden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                var ok = await _supabaseService.AddStromzaehlerAsync(Belegung.ParzelleId, dlg.Zaehlernummer.Trim(), dlg.Eichdatum.Value, changeDate);
                if (!ok)
                {
                    MessageBox.Show("Zählertausch konnte nicht gespeichert werden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Zählertausch: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;
    }
}
