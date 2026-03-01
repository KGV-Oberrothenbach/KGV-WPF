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

        private string _email = "";
        public string Email
        {
            get => _email;
            set => SetField(ref _email, value ?? "", nameof(Email), nameof(DisplayName));
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
                Email = other.Email;

                Bemerkungen = other.Bemerkungen;
                WhatsappEinwilligung = other.WhatsappEinwilligung;

                MitgliedSeit = other.MitgliedSeit;
                MitgliedEnde = other.MitgliedEnde;

                Role = other.Role;

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
                string.Equals(Email ?? "", other.Email ?? "", StringComparison.Ordinal) &&
                string.Equals(Bemerkungen ?? "", other.Bemerkungen ?? "", StringComparison.Ordinal) &&
                WhatsappEinwilligung == other.WhatsappEinwilligung &&
                MitgliedSeit == other.MitgliedSeit &&
                MitgliedEnde == other.MitgliedEnde &&
                string.Equals(Role ?? "", other.Role ?? "", StringComparison.Ordinal);
        }
    }
}