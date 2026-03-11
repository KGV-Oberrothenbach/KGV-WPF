// File: ViewModels/GartenDokumenteViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Wpf.Helpers;

namespace KGV.Wpf.ViewModels
{
    public sealed class GartenDokumenteViewModel : BaseViewModel, INavigationAware
    {
        private readonly ISupabaseService _supabaseService;

        public ParzellenBelegungDTO Belegung { get; }

        public string GartenNr => Belegung.GartenNr;

        public ObservableCollection<DocumentInfo> Dokumente { get; } = new();

        public RelayCommand<object?> RefreshCommand { get; }
        public RelayCommand<DocumentInfo> OpenCommand { get; }

        public GartenDokumenteViewModel(ISupabaseService supabaseService, ParzellenBelegungDTO belegung)
        {
            _supabaseService = supabaseService;
            Belegung = belegung;

            RefreshCommand = new RelayCommand<object?>(_ => _ = LoadAsync());
            OpenCommand = new RelayCommand<DocumentInfo>(doc =>
            {
                if (doc == null) return;
                _ = OpenAsync(doc);
            });
        }

        public async Task OnNavigatedToAsync()
        {
            await LoadAsync();
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private async Task LoadAsync()
        {
            try
            {
                Dokumente.Clear();
                foreach (var d in await _supabaseService.GetParzelleDokumenteAsync(Belegung.ParzelleId))
                    Dokumente.Add(d);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dokumente konnten nicht geladen werden: {ex.Message}", "Fehler", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task OpenAsync(DocumentInfo? doc)
        {
            try
            {
                if (doc == null)
                    return;

                var url = await _supabaseService.CreateDokumentSignedUrlAsync(doc.StoragePath, 3600);
                if (string.IsNullOrWhiteSpace(url))
                {
                    MessageBox.Show("Dokument konnte nicht geöffnet werden (kein URL).", "Fehler", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Öffnen fehlgeschlagen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
