using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Helpers;

namespace KGV.ViewModels
{
    public sealed class DokumenteViewModel : BaseViewModel, INavigationAware
    {
        private readonly ISupabaseService _supabaseService;

        public DokumenteContext Context { get; }

        public ObservableCollection<DocumentInfo> MitgliedDokumente { get; } = new();

        public RelayCommand<object?> RefreshCommand { get; }
        public RelayCommand<DocumentInfo> OpenCommand { get; }

        public DokumenteViewModel(ISupabaseService supabaseService, DokumenteContext ctx)
        {
            _supabaseService = supabaseService;
            Context = ctx;

            RefreshCommand = new RelayCommand<object?>(_ => _ = LoadAsync());
            OpenCommand = new RelayCommand<DocumentInfo>(doc => _ = OpenAsync(doc));
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
                MitgliedDokumente.Clear();
                foreach (var d in await _supabaseService.GetMitgliedDokumenteAsync(Context.Member.Id))
                    MitgliedDokumente.Add(d);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dokumente konnten nicht geladen werden: {ex.Message}", "Fehler", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task OpenAsync(DocumentInfo doc)
        {
            try
            {
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
