// File: Core/Models/MemberDTO.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace KGV.Core.Models
{
    public class MemberDTO : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public event EventHandler? Changed;

        public bool SuppressChangedEvents { get; set; }

        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void RaiseChanged()
        {
            if (SuppressChangedEvents) return;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        // Stabilisiert DATE-Spalten gegen -1/+1 Drift (Timezone/Kind)
        private static DateTime? NormalizeDate(DateTime? value)
        {
            if (!value.HasValue) return null;
            var noon = value.Value.Date.AddHours(12);
            return DateTime.SpecifyKind(noon, DateTimeKind.Unspecified);
        }

        private bool SetField<T>(ref T field, T value, string propertyName, params string[] alsoNotify)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;

            field = value;
            OnPropertyChanged(propertyName);

            if (alsoNotify != null)
            {
                foreach (var p in alsoNotify)
                    OnPropertyChanged(p);
            }

            RaiseChanged();
            return true;
        }

        private int _id;
        public int Id
        {
            get => _id;
            set => SetField(ref _id, value, nameof(Id));
        }

        private string _vorname = "";
        public string Vorname
        {
            get => _vorname;
            set => SetField(ref _vorname, value ?? "", nameof(Vorname), nameof(DisplayName));
        }

        private string _nachname = "";
        public string Nachname
        {
            get => _nachname;
            set => SetField(ref _nachname, value ?? "", nameof(Nachname), nameof(DisplayName));
        }

        private DateTime? _geburtsdatum;
        public DateTime? Geburtsdatum
        {
            get => _geburtsdatum;
            set => SetField(ref _geburtsdatum, NormalizeDate(value), nameof(Geburtsdatum));
        }

        private string _strasse = "";
        public string Strasse
        {
            get => _strasse;
            set => SetField(ref _strasse, value ?? "", nameof(Strasse));
        }

        private string _plz = "";
        public string PLZ
        {
            get => _plz;
            set => SetField(ref _plz, value ?? "", nameof(PLZ));
        }

        private string _ort = "";
        public string Ort
        {
            get => _ort;
            set => SetField(ref _ort, value ?? "", nameof(Ort));
        }

        private string _telefon = "";
        public string Telefon
        {
            get => _telefon;
            set => SetField(ref _telefon, value ?? "", nameof(Telefon));
        }

        private string _mobilnummer = "";
        public string Mobilnummer
        {
            get => _mobilnummer;
            set => SetField(ref _mobilnummer, value ?? "", nameof(Mobilnummer));
        }

        private string _email = "";
        public string Email
        {
            get => _email;
            set => SetField(ref _email, value ?? "", nameof(Email), nameof(DisplayName));
        }

        private bool _emailInfoEinwilligung;
        public bool EmailInfoEinwilligung
        {
            get => _emailInfoEinwilligung;
            set => SetField(ref _emailInfoEinwilligung, value, nameof(EmailInfoEinwilligung));
        }

        private bool _emailRechnungEinwilligung;
        public bool EmailRechnungEinwilligung
        {
            get => _emailRechnungEinwilligung;
            set => SetField(ref _emailRechnungEinwilligung, value, nameof(EmailRechnungEinwilligung));
        }

        private string _bemerkungen = "";
        public string Bemerkungen
        {
            get => _bemerkungen;
            set => SetField(ref _bemerkungen, value ?? "", nameof(Bemerkungen));
        }

        private bool _whatsappEinwilligung;
        public bool WhatsappEinwilligung
        {
            get => _whatsappEinwilligung;
            set => SetField(ref _whatsappEinwilligung, value, nameof(WhatsappEinwilligung));
        }

        private DateTime? _mitgliedSeit;
        public DateTime? MitgliedSeit
        {
            get => _mitgliedSeit;
            set => SetField(ref _mitgliedSeit, NormalizeDate(value), nameof(MitgliedSeit));
        }

        private DateTime? _mitgliedEnde;
        public DateTime? MitgliedEnde
        {
            get => _mitgliedEnde;
            set => SetField(ref _mitgliedEnde, NormalizeDate(value), nameof(MitgliedEnde), nameof(Aktiv));
        }

        public bool Aktiv
        {
            get => MitgliedEnde == null;
            set
            {
                if (value) MitgliedEnde = null;
            }
        }

        private string _role = "";
        public string Role
        {
            get => _role;
            set => SetField(ref _role, value ?? "", nameof(Role));
        }

        private string _arbeitsstundenAltersregelTyp = "keine";
        public string ArbeitsstundenAltersregelTyp
        {
            get => _arbeitsstundenAltersregelTyp;
            set => SetField(ref _arbeitsstundenAltersregelTyp, (value ?? "keine").Trim().ToLowerInvariant(), nameof(ArbeitsstundenAltersregelTyp));
        }

        private Guid? _authUserId;
        public Guid? AuthUserId
        {
            get => _authUserId;
            set => SetField(ref _authUserId, value, nameof(AuthUserId));
        }

        private List<GartenDTO> _gärten = new();
        public List<GartenDTO> Gärten
        {
            get => _gärten;
            set => SetField(ref _gärten, value ?? new List<GartenDTO>(), nameof(Gärten));
        }

        // WPF kann bei manchen Controls/Templates (z.B. ComboBox) intern eine TwoWay-Bindung erzeugen.
        // Damit es dabei nicht zu "TwoWay ... funktioniert nicht mit schreibgeschützter Eigenschaft" kommt,
        // hat DisplayName einen (no-op) Setter.
        public string DisplayName
        {
            get =>
                string.IsNullOrWhiteSpace($"{Vorname} {Nachname}".Trim())
                    ? Email
                    : $"{Vorname} {Nachname}".Trim();
            set { /* no-op */ }
        }

        public MemberDTO Clone()
        {
            var copy = new MemberDTO();
            copy.CopyFrom(this);
            return copy;
        }

        public void CopyFrom(MemberDTO other)
        {
            if (other == null) return;

            var prev = SuppressChangedEvents;
            SuppressChangedEvents = true;
            try
            {
                Id = other.Id;
                Vorname = other.Vorname;
                Nachname = other.Nachname;
                Geburtsdatum = other.Geburtsdatum;

                Strasse = other.Strasse;
                PLZ = other.PLZ;
                Ort = other.Ort;

                Telefon = other.Telefon;
                Mobilnummer = other.Mobilnummer;
                Email = other.Email;
                EmailInfoEinwilligung = other.EmailInfoEinwilligung;
                EmailRechnungEinwilligung = other.EmailRechnungEinwilligung;

                Bemerkungen = other.Bemerkungen;
                WhatsappEinwilligung = other.WhatsappEinwilligung;

                MitgliedSeit = other.MitgliedSeit;
                MitgliedEnde = other.MitgliedEnde;

                Role = other.Role;
                AuthUserId = other.AuthUserId;

                ArbeitsstundenAltersregelTyp = other.ArbeitsstundenAltersregelTyp;

                Gärten = other.Gärten != null ? new List<GartenDTO>(other.Gärten) : new List<GartenDTO>();
            }
            finally
            {
                SuppressChangedEvents = prev;
            }
        }

        public bool ValueEquals(MemberDTO other)
        {
            if (other == null) return false;

            return
                Id == other.Id &&
                string.Equals(Vorname ?? "", other.Vorname ?? "", StringComparison.Ordinal) &&
                string.Equals(Nachname ?? "", other.Nachname ?? "", StringComparison.Ordinal) &&
                Geburtsdatum == other.Geburtsdatum &&
                string.Equals(Strasse ?? "", other.Strasse ?? "", StringComparison.Ordinal) &&
                string.Equals(PLZ ?? "", other.PLZ ?? "", StringComparison.Ordinal) &&
                string.Equals(Ort ?? "", other.Ort ?? "", StringComparison.Ordinal) &&
                string.Equals(Telefon ?? "", other.Telefon ?? "", StringComparison.Ordinal) &&
                string.Equals(Mobilnummer ?? "", other.Mobilnummer ?? "", StringComparison.Ordinal) &&
                string.Equals(Email ?? "", other.Email ?? "", StringComparison.Ordinal) &&
                EmailInfoEinwilligung == other.EmailInfoEinwilligung &&
                EmailRechnungEinwilligung == other.EmailRechnungEinwilligung &&
                string.Equals(Bemerkungen ?? "", other.Bemerkungen ?? "", StringComparison.Ordinal) &&
                WhatsappEinwilligung == other.WhatsappEinwilligung &&
                MitgliedSeit == other.MitgliedSeit &&
                MitgliedEnde == other.MitgliedEnde &&
                string.Equals(Role ?? "", other.Role ?? "", StringComparison.Ordinal) &&
                AuthUserId == other.AuthUserId &&
                string.Equals(ArbeitsstundenAltersregelTyp ?? "", other.ArbeitsstundenAltersregelTyp ?? "", StringComparison.Ordinal);
        }
    }
}
