// File: ViewModels/GartenWasserViewModel.cs
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Helpers;
using KGV.Views;
using System;
using System.Windows;
using System.Diagnostics;

namespace KGV.ViewModels
{
    public sealed class GartenWasserViewModel : BaseViewModel, INavigationAware
    {
        private const short ZaehlerTypWasser = 2;
        private readonly ISupabaseService _supabaseService;

        public ParzellenBelegungDTO Belegung { get; }

        public string GartenNr => Belegung.GartenNr;

        public ObservableCollection<ZaehlerAblesungDTO> Ablesungen { get; } = new();

        public RelayCommand<object?> ZaehlerEinbauenCommand { get; }
        public RelayCommand<object?> ZaehlerAusbauenCommand { get; }
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

        public GartenWasserViewModel(ISupabaseService supabaseService, ParzellenBelegungDTO belegung)
        {
            _supabaseService = supabaseService;
            Belegung = belegung;

            ZaehlerEinbauenCommand = new RelayCommand<object?>(_ => _ = ZaehlerEinbauenAsync());
            ZaehlerAusbauenCommand = new RelayCommand<object?>(_ => _ = ZaehlerAusbauenAsync());
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
            var items = await _supabaseService.GetWasserAblesungenAsync(Belegung.ParzelleId);
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
                Title = $"Neue Ablesung (Wasser) - Garten Nr. {GartenNr}"
            };

            if (dlg.ShowDialog() != true)
                return;

            if (!dlg.Ablesedatum.HasValue || !dlg.Stand.HasValue)
            {
                MessageBox.Show("Bitte Ablesedatum und Zählerstand angeben.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var meter = await _supabaseService.GetActiveWasserzaehlerAsync(Belegung.ParzelleId, dlg.Ablesedatum.Value);
            if (meter == null)
            {
                MessageBox.Show("Kein aktiver Wasserzähler für dieses Datum gefunden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var ok = await _supabaseService.AddAblesungAsync(ZaehlerTypWasser, meter.Id, dlg.Ablesedatum.Value, dlg.Stand.Value, string.IsNullOrWhiteSpace(dlg.FotoPfad) ? null : dlg.FotoPfad.Trim());
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
                Title = $"Ablesung bearbeiten (Wasser) - Garten Nr. {GartenNr}"
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

        private async Task ZaehlerAusbauenAsync()
        {
            try
            {
                var dlg = new DatumDialog
                {
                    Owner = Application.Current?.MainWindow,
                    Title = $"Wasserzähler ausbauen (Garten Nr. {GartenNr})"
                };

                if (dlg.ShowDialog() != true)
                    return;

                if (!dlg.Datum.HasValue)
                {
                    MessageBox.Show("Bitte Ausbaudatum angeben.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var date = dlg.Datum.Value.Date;
                var current = await _supabaseService.GetActiveWasserzaehlerAsync(Belegung.ParzelleId, date);
                if (current == null)
                {
                    MessageBox.Show("Kein aktiver Wasserzähler für dieses Datum gefunden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var ok = await _supabaseService.SetWasserzaehlerAusgebautAmAsync(current.Id, date);
                if (!ok)
                {
                    MessageBox.Show("Ausbau konnte nicht gespeichert werden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Ausbau: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ZaehlerEinbauenAsync()
        {
            try
            {
                var dlg = new ZaehlerTauschDialog
                {
                    Owner = Application.Current?.MainWindow,
                    Title = $"Wasserzähler einbauen (Garten Nr. {GartenNr})"
                };

                if (dlg.ShowDialog() != true)
                    return;

                if (string.IsNullOrWhiteSpace(dlg.Zaehlernummer) || !dlg.Eichdatum.HasValue || !dlg.EingebautAm.HasValue)
                {
                    MessageBox.Show("Bitte Zählernummer, Eichdatum und Einbaudatum angeben.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var ok = await _supabaseService.AddWasserzaehlerAsync(Belegung.ParzelleId, dlg.Zaehlernummer.Trim(), dlg.Eichdatum.Value, dlg.EingebautAm.Value.Date);
                if (!ok)
                {
                    MessageBox.Show("Einbau konnte nicht gespeichert werden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Einbau: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;
    }
}
