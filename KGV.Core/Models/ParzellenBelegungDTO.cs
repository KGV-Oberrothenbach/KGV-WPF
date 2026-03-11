// File: Core/Models/ParzellenBelegungDTO.cs
using System;
using System.ComponentModel;

namespace KGV.Core.Models
{
    /// <summary>
    /// UI-DTO für Parzellenbelegungen (Anzeige in Member-Detail).
    /// Enthält "sprechende" Felder (GartenNr/Anlage) + Zeitraum.
    /// </summary>
    public class ParzellenBelegungDTO : INotifyPropertyChanged, IDataErrorInfo
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private int _belegungId;
        public int BelegungId
        {
            get => _belegungId;
            set { if (_belegungId == value) return; _belegungId = value; OnPropertyChanged(nameof(BelegungId)); }
        }

        private int _parzelleId;
        public int ParzelleId
        {
            get => _parzelleId;
            set { if (_parzelleId == value) return; _parzelleId = value; OnPropertyChanged(nameof(ParzelleId)); }
        }

        private int _mitgliedId;
        public int MitgliedId
        {
            get => _mitgliedId;
            set { if (_mitgliedId == value) return; _mitgliedId = value; OnPropertyChanged(nameof(MitgliedId)); }
        }

        private string _gartenNr = "";
        public string GartenNr
        {
            get => _gartenNr;
            set { if (_gartenNr == value) return; _gartenNr = value ?? ""; OnPropertyChanged(nameof(GartenNr)); OnPropertyChanged(nameof(Anzeige)); }
        }

        private string _anlage = "";
        public string Anlage
        {
            get => _anlage;
            set { if (_anlage == value) return; _anlage = value ?? ""; OnPropertyChanged(nameof(Anlage)); OnPropertyChanged(nameof(Anzeige)); }
        }

        private DateTime? _vonDatum;
        public DateTime? VonDatum
        {
            get => _vonDatum;
            set
            {
                var v = value?.Date;
                if (_vonDatum == v) return;
                _vonDatum = v;
                OnPropertyChanged(nameof(VonDatum));
                OnPropertyChanged(nameof(IsAktiv));
                OnPropertyChanged(nameof(StatusText));
            }
        }

        private DateTime? _bisDatum;
        public DateTime? BisDatum
        {
            get => _bisDatum;
            set
            {
                var v = value?.Date;
                if (_bisDatum == v) return;
                _bisDatum = v;
                OnPropertyChanged(nameof(BisDatum));
                OnPropertyChanged(nameof(IsAktiv));
                OnPropertyChanged(nameof(StatusText));
            }
        }

        /// <summary>
        /// Aktiv, falls "heute" im Zeitraum liegt.
        /// </summary>
        public bool IsAktiv
        {
            get
            {
                var today = DateTime.Today;
                var von = (VonDatum ?? DateTime.MinValue).Date;
                var bis = BisDatum?.Date;
                return von <= today && (bis == null || bis >= today);
            }
        }

        /// <summary>
        /// Für die UI: wir nennen Parzellen mit BisDatum "Frei", auch wenn BisDatum in Zukunft liegt.
        /// </summary>
        public string StatusText
        {
            get
            {
                if (!IsAktiv) return "Frei";
                if (BisDatum.HasValue) return $"Frei (bis {BisDatum.Value:dd.MM.yyyy})";
                return "Belegt";
            }
        }

        public string Anzeige => string.IsNullOrWhiteSpace(Anlage) ? GartenNr : $"{GartenNr} - {Anlage}";

        // ===== Validation (für spätere Editierbarkeit) =====
        public string Error => "";

        public string this[string columnName]
        {
            get
            {
                if (columnName == nameof(GartenNr) && string.IsNullOrWhiteSpace(GartenNr))
                    return "Garten-Nr ist erforderlich.";

                if (columnName == nameof(VonDatum) || columnName == nameof(BisDatum))
                {
                    if (VonDatum.HasValue && BisDatum.HasValue && BisDatum.Value.Date < VonDatum.Value.Date)
                        return "Bis-Datum darf nicht vor Von-Datum liegen.";
                }

                return "";
            }
        }
    }
}
