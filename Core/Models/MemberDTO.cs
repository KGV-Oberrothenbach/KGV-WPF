using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;

namespace KGV.Core.Models
{
    public class MemberDTO
    {
        // Dirty Tracking (Event-basiert)
        public event EventHandler? Changed;

        /// <summary>
        /// Wenn true: Änderungen feuern kein Changed-Event (wichtig für CopyFrom beim Cancel).
        /// </summary>
        public bool SuppressChangedEvents { get; set; }

        private void RaiseChanged()
        {
            if (SuppressChangedEvents) return;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private int _id;
        public int Id
        {
            get => _id;
            set { if (_id == value) return; _id = value; RaiseChanged(); }
        }

        private string _vorname = "";
        public string Vorname
        {
            get => _vorname;
            set { if (_vorname == value) return; _vorname = value ?? ""; RaiseChanged(); }
        }

        private string _nachname = "";
        public string Nachname
        {
            get => _nachname;
            set { if (_nachname == value) return; _nachname = value ?? ""; RaiseChanged(); }
        }

        private DateTime? _geburtsdatum;
        public DateTime? Geburtsdatum
        {
            get => _geburtsdatum;
            set { if (_geburtsdatum == value) return; _geburtsdatum = value; RaiseChanged(); }
        }

        private string _strasse = "";
        public string Strasse
        {
            get => _strasse;
            set { if (_strasse == value) return; _strasse = value ?? ""; RaiseChanged(); }
        }

        private string _plz = "";
        public string PLZ
        {
            get => _plz;
            set { if (_plz == value) return; _plz = value ?? ""; RaiseChanged(); }
        }

        private string _ort = "";
        public string Ort
        {
            get => _ort;
            set { if (_ort == value) return; _ort = value ?? ""; RaiseChanged(); }
        }

        private string _telefon = "";
        public string Telefon
        {
            get => _telefon;
            set { if (_telefon == value) return; _telefon = value ?? ""; RaiseChanged(); }
        }

        private string _email = "";
        public string Email
        {
            get => _email;
            set { if (_email == value) return; _email = value ?? ""; RaiseChanged(); }
        }

        private string _bemerkungen = "";
        public string Bemerkungen
        {
            get => _bemerkungen;
            set { if (_bemerkungen == value) return; _bemerkungen = value ?? ""; RaiseChanged(); }
        }

        private bool _whatsappEinwilligung;
        public bool WhatsappEinwilligung
        {
            get => _whatsappEinwilligung;
            set { if (_whatsappEinwilligung == value) return; _whatsappEinwilligung = value; RaiseChanged(); }
        }

        private DateTime? _mitgliedSeit;
        public DateTime? MitgliedSeit
        {
            get => _mitgliedSeit;
            set { if (_mitgliedSeit == value) return; _mitgliedSeit = value; RaiseChanged(); }
        }

        private DateTime? _mitgliedEnde;
        public DateTime? MitgliedEnde
        {
            get => _mitgliedEnde;
            set { if (_mitgliedEnde == value) return; _mitgliedEnde = value; RaiseChanged(); }
        }

        /// <summary>
        /// Aktiv = MitgliedEnde == null (wie du es vorher hattest)
        /// </summary>
        public bool Aktiv
        {
            get => MitgliedEnde == null;
            set
            {
                if (value)
                {
                    MitgliedEnde = null; // triggert RaiseChanged über Setter
                }
            }
        }

        private string _role = "";
        public string Role
        {
            get => _role;
            set { if (_role == value) return; _role = value ?? ""; RaiseChanged(); }
        }

        public List<GartenDTO> Gärten { get; set; } = new List<GartenDTO>();

        public string DisplayName =>
            string.IsNullOrWhiteSpace($"{Vorname} {Nachname}".Trim())
                ? Email
                : $"{Vorname} {Nachname}".Trim();

        // ============================
        // Snapshot / Cancel Hilfen
        // ============================

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