using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KGV.Core.Models;

namespace KGV.ViewModels
{
    public partial class MemberViewModel : ObservableObject
    {
        private readonly MemberDTO _member;

        public MemberViewModel(MemberDTO member)
        {
            _member = member;
            LoadFromDTO();
        }

        [ObservableProperty] private string vorname = "";
        [ObservableProperty] private string nachname = "";
        [ObservableProperty] private string strasse = "";
        [ObservableProperty] private string plz = "";
        [ObservableProperty] private string ort = "";
        [ObservableProperty] private string telefon = "";
        [ObservableProperty] private string email = "";
        [ObservableProperty] private string bemerkungen = "";
        [ObservableProperty] private bool whatsappEinwilligung = false;
        [ObservableProperty] private bool aktiv = true;
        [ObservableProperty] private string role = "";

        public IRelayCommand SaveCommand => new RelayCommand<object?>(_ => SaveChanges());

        private void SaveChanges()
        {
            _member.Vorname = Vorname;
            _member.Nachname = Nachname;
            _member.Strasse = Strasse;
            _member.PLZ = Plz;
            _member.Ort = Ort;
            _member.Telefon = Telefon;
            _member.Email = Email;
            _member.Bemerkungen = Bemerkungen;
            _member.WhatsappEinwilligung = WhatsappEinwilligung;
            _member.Aktiv = Aktiv;
            _member.Role = Role;

            // TODO: SupabaseService zum Speichern aufrufen
        }

        public void LoadFromDTO()
        {
            Vorname = _member.Vorname;
            Nachname = _member.Nachname;
            Strasse = _member.Strasse;
            Plz = _member.PLZ;
            Ort = _member.Ort;
            Telefon = _member.Telefon;
            Email = _member.Email;
            Bemerkungen = _member.Bemerkungen;
            WhatsappEinwilligung = _member.WhatsappEinwilligung;
            Aktiv = _member.Aktiv;
            Role = _member.Role;
        }
    }
}