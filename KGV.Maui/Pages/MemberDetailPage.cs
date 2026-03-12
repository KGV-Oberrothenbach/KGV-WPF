using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Maui.State;
using Microsoft.Maui.Layouts;

namespace KGV.Maui.Pages;

public sealed class MemberDetailPage : ContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly IAuthService _authService;
    private readonly MemberSelectionState _memberSelection;
    private readonly ParzelleSelectionState _parzelleSelection;

    private int? _loadedMitgliedId;
    private MemberDTO? _member;
    private MemberDTO? _originalSnapshot;

    private bool _isBusy;
    private Task? _loadTask;

    private bool _isEditMode;
    private bool _isDirty;
    private string? _lockUserId;
    private bool _eventsWired;

    private bool _isHauptmitglied;
    private int? _hauptmitgliedId;

    private readonly ActivityIndicator _busy;
    private readonly Label _title;
    private readonly Label _status;
    private readonly Label _editModeHint;

    private readonly Entry _nachname;
    private readonly Entry _vorname;
    private readonly DatePicker _geburtsdatum;
    private readonly Picker _altersregel;

    private readonly Entry _strasse;
    private readonly Entry _plz;
    private readonly Entry _ort;

    private readonly Entry _telefon;
    private readonly Entry _mobil;
    private readonly CheckBox _whatsapp;
    private readonly Entry _email;

    private readonly DatePicker _mitgliedSeit;
    private readonly CheckBox _aktiv;
    private readonly DatePicker _mitgliedEnde;

    private readonly Button _editButton;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;
    private readonly Button _goToHauptmitgliedButton;

    private readonly FlexLayout _parzellenButtons;
    private readonly List<Button> _parzellenButtonsCreated = new();
    private readonly List<ParzellenBelegungItem> _parzellenBelegungen = new();
    private ParzellenBelegungItem? _selectedBelegung;

    private readonly Picker _freeParzellePicker;
    private readonly List<ParzelleOption> _availableParzellen = new();
    private readonly DatePicker _assignVonDate;
    private readonly Button _assignParzelleButton;
    private readonly Button _endBelegungButton;

    private readonly Style? _entryBorderStyle;
    private readonly Style? _cardStyle;

    public MemberDetailPage(
        ISupabaseService supabaseService,
        IAuthService authService,
        MemberSelectionState memberSelection,
        ParzelleSelectionState parzelleSelection)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _memberSelection = memberSelection ?? throw new ArgumentNullException(nameof(memberSelection));
        _parzelleSelection = parzelleSelection ?? throw new ArgumentNullException(nameof(parzelleSelection));

        Title = "Stammdaten";

        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };
        _status = new Label { TextColor = Colors.Red };
        _title = new Label { FontSize = 22, FontAttributes = FontAttributes.Bold };
        _editModeHint = new Label { Text = "Bearbeitungsmodus aktiv", TextColor = Colors.DarkOrange, FontAttributes = FontAttributes.Bold, IsVisible = false };

        _nachname = new Entry { Placeholder = "Nachname" };
        _vorname = new Entry { Placeholder = "Vorname" };
        _geburtsdatum = new DatePicker { Date = DateTime.Today };

        _altersregel = new Picker { Title = "Altersregel" };
        _altersregel.ItemsSource = new List<string> { "keine", "frau75", "mann80" };
        _altersregel.SelectedIndexChanged += (_, __) => MarkDirtyIfEditing();

        _strasse = new Entry { Placeholder = "Straße / Hausnummer" };
        _plz = new Entry { Placeholder = "PLZ", Keyboard = Keyboard.Numeric };
        _ort = new Entry { Placeholder = "Ort" };

        _telefon = new Entry { Placeholder = "Telefon" };
        _mobil = new Entry { Placeholder = "Mobilnummer" };
        _whatsapp = new CheckBox();
        _email = new Entry { Placeholder = "E-Mail", Keyboard = Keyboard.Email };

        _mitgliedSeit = new DatePicker { Date = DateTime.Today };
        _aktiv = new CheckBox();
        _mitgliedEnde = new DatePicker { Date = DateTime.Today };

        _editButton = new Button { Text = "Bearbeiten" };
        _editButton.Clicked += async (_, __) => await ToggleEditAsync();

        _saveButton = new Button { Text = "Speichern" };
        _saveButton.Clicked += async (_, __) => await SaveAsync();

        _cancelButton = new Button { Text = "Abbrechen" };
        _cancelButton.Clicked += async (_, __) => await CancelAsync();

        _goToHauptmitgliedButton = new Button { Text = "Zum Hauptmitglied", IsVisible = false };
        _goToHauptmitgliedButton.Clicked += async (_, __) => await GoToHauptmitgliedAsync();

        _parzellenButtons = new FlexLayout
        {
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap,
            JustifyContent = FlexJustify.Start,
            AlignItems = FlexAlignItems.Start,
            AlignContent = FlexAlignContent.Start
        };

        _freeParzellePicker = new Picker { Title = "Freie Parzelle" };
        _freeParzellePicker.SelectedIndexChanged += (_, __) => UpdateEditState();

        _assignVonDate = new DatePicker { Date = DateTime.Today };

        _assignParzelleButton = new Button { Text = "Garten zuordnen" };
        _assignParzelleButton.Clicked += OnAssignParzelleClicked;

        _endBelegungButton = new Button { Text = "Belegung beenden" };
        _endBelegungButton.Clicked += OnEndBelegungClicked;

        _entryBorderStyle = TryGetStyle("EntryBorder");
        _cardStyle = TryGetStyle("Card");

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 14,
                Children =
                {
                    _busy,
                    WrapCard(new VerticalStackLayout
                    {
                        Spacing = 8,
                        Children = { _title, _editModeHint, _status }
                    }),
                    new HorizontalStackLayout
                    {
                        Spacing = 12,
                        Children = { _editButton, _saveButton, _cancelButton, _goToHauptmitgliedButton }
                    },
                    WrapCard(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            new Label { Text = "Persönliche Daten", FontAttributes = FontAttributes.Bold },
                            WrapEntry(_nachname),
                            WrapEntry(_vorname),
                            _geburtsdatum,
                            _altersregel
                        }
                    }),
                    WrapCard(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            new Label { Text = "Adresse", FontAttributes = FontAttributes.Bold },
                            WrapEntry(_strasse),
                            WrapEntry(_plz),
                            WrapEntry(_ort)
                        }
                    }),
                    WrapCard(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            new Label { Text = "Kontakt", FontAttributes = FontAttributes.Bold },
                            WrapEntry(_telefon),
                            WrapEntry(_mobil),
                            new HorizontalStackLayout
                            {
                                Spacing = 8,
                                Children =
                                {
                                    _whatsapp,
                                    new Label { Text = "WhatsApp Einwilligung", VerticalTextAlignment = TextAlignment.Center }
                                }
                            },
                            WrapEntry(_email)
                        }
                    }),
                    WrapCard(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            new Label { Text = "Mitgliedschaft", FontAttributes = FontAttributes.Bold },
                            _mitgliedSeit,
                            new HorizontalStackLayout
                            {
                                Spacing = 8,
                                Children =
                                {
                                    _aktiv,
                                    new Label { Text = "Aktiv", VerticalTextAlignment = TextAlignment.Center }
                                }
                            },
                            _mitgliedEnde
                        }
                    }),
                    WrapCard(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            new Label { Text = "Parzellen", FontAttributes = FontAttributes.Bold },
                            _parzellenButtons,
                            new HorizontalStackLayout
                            {
                                Spacing = 10,
                                Children = { _freeParzellePicker, _assignVonDate, _assignParzelleButton, _endBelegungButton }
                            }
                        }
                    })
                }
            }
        };

        Appearing += OnAppearing;
        Disappearing += OnDisappearing;

        UpdateEditState();
    }

    private static Color GetColorResource(string key, Color fallback)
    {
        try
        {
            if (Application.Current?.Resources == null)
                return fallback;

            if (Application.Current.Resources.TryGetValue(key, out var obj) && obj is Color c)
                return c;

            return fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private Style? TryGetStyle(string key)
    {
        if (Application.Current?.Resources == null)
            return null;

        return Application.Current.Resources.TryGetValue(key, out var obj) ? obj as Style : null;
    }

    private Border WrapEntry(Entry entry)
        => _entryBorderStyle != null ? new Border { Style = _entryBorderStyle, Content = entry } : new Border { Content = entry };

    private Border WrapCard(View content)
        => _cardStyle != null ? new Border { Style = _cardStyle, Content = content } : new Border { Content = content };

    private async void OnAppearing(object? sender, EventArgs e)
    {
        if (!_eventsWired)
        {
            _eventsWired = true;
            WireEvents();
        }

        await EnsureLoadedAsync();
    }

    private async void OnDisappearing(object? sender, EventArgs e)
    {
        if (_isEditMode)
            await ReleaseLockAsync(force: false);

        _isEditMode = false;
        _isDirty = false;
        UpdateEditState();
    }

    private void WireEvents()
    {
        _nachname.TextChanged += (_, __) => MarkDirtyIfEditing();
        _vorname.TextChanged += (_, __) => MarkDirtyIfEditing();
        _geburtsdatum.DateSelected += (_, __) => MarkDirtyIfEditing();

        _strasse.TextChanged += (_, __) => MarkDirtyIfEditing();
        _plz.TextChanged += (_, __) => MarkDirtyIfEditing();
        _ort.TextChanged += (_, __) => MarkDirtyIfEditing();

        _telefon.TextChanged += (_, __) => MarkDirtyIfEditing();
        _mobil.TextChanged += (_, __) => MarkDirtyIfEditing();
        _email.TextChanged += (_, __) => MarkDirtyIfEditing();
        _whatsapp.CheckedChanged += (_, __) => MarkDirtyIfEditing();

        _mitgliedSeit.DateSelected += (_, __) => MarkDirtyIfEditing();
        _mitgliedEnde.DateSelected += (_, __) => MarkDirtyIfEditing();
        _aktiv.CheckedChanged += (_, __) =>
        {
            if (!_isEditMode) return;
            _mitgliedEnde.IsEnabled = !_aktiv.IsChecked;
            MarkDirtyIfEditing();
        };
    }

    private Task EnsureLoadedAsync()
    {
        // Guard gegen parallele Initialisierung (schnelles Navigieren / mehrfaches Appearing)
        if (_loadTask != null && !_loadTask.IsCompleted)
            return _loadTask;

        _loadTask = EnsureLoadedCoreAsync();
        return _loadTask;
    }

    private async Task EnsureLoadedCoreAsync()
    {
        var selectedId = _memberSelection.SelectedMitgliedId;
        if (selectedId == null)
        {
            _loadedMitgliedId = null;
            _member = null;
            _originalSnapshot = null;
            _title.Text = "Bitte ein Mitglied auswählen";
            _status.Text = string.Empty;
            ClearParzellenUi(clearContext: true);
            _isEditMode = false;
            _isDirty = false;
            UpdateEditState();
            return;
        }

        // Beim erneuten Öffnen (Appearing) defensiv neu laden, solange nicht im Edit-Mode.
        if (_loadedMitgliedId == selectedId && _member != null)
        {
            if (!_isEditMode)
                await LoadMemberAsync(selectedId.Value);
            return;
        }

        // Kontextwechsel: stale Parzellenkontext zurücksetzen (wird beim Laden neu gesetzt)
        if (_loadedMitgliedId.HasValue && _loadedMitgliedId.Value != selectedId.Value)
        {
            _parzelleSelection.SelectedParzelleId = null;
            _parzelleSelection.GartenNr = null;
        }

        if (_isEditMode)
            await CancelAsync();

        await LoadMemberAsync(selectedId.Value);
    }

    private async Task LoadMemberAsync(int mitgliedId)
    {
        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            var rec = await _supabaseService.GetMitgliedByIdAsync(mitgliedId);
            if (rec == null)
            {
                _loadedMitgliedId = null;
                _member = null;
                _originalSnapshot = null;
                _title.Text = "Mitglied nicht gefunden";
                _status.Text = $"Mitglied nicht gefunden (Id={mitgliedId}).";
                ClearParzellenUi(clearContext: true);
                UpdateEditState();
                return;
            }

            _loadedMitgliedId = mitgliedId;
            _member = new MemberDTO
            {
                Id = rec.Id,
                Vorname = rec.Vorname ?? string.Empty,
                Nachname = rec.Name ?? string.Empty,
                Geburtsdatum = rec.Geburtsdatum,
                Strasse = rec.Adresse ?? string.Empty,
                PLZ = rec.Plz ?? string.Empty,
                Ort = rec.Ort ?? string.Empty,
                Telefon = rec.Telefon ?? string.Empty,
                Mobilnummer = rec.Handy ?? string.Empty,
                Email = rec.Email ?? string.Empty,
                Bemerkungen = rec.Bemerkung ?? string.Empty,
                WhatsappEinwilligung = rec.WhatsappEinwilligung,
                MitgliedSeit = rec.MitgliedSeit,
                MitgliedEnde = rec.MitgliedEnde,
                Role = rec.Role ?? string.Empty,
                ArbeitsstundenAltersregelTyp = rec.ArbeitsstundenAltersregelTyp ?? "keine"
            };

            _isHauptmitglied = rec.HauptmitgliedId == null;
            _hauptmitgliedId = rec.HauptmitgliedId;
            _altersregel.IsVisible = _isHauptmitglied;

            _goToHauptmitgliedButton.IsVisible = _hauptmitgliedId.HasValue;

            _originalSnapshot = _member.Clone();

            _title.Text = $"{_member.Nachname}, {_member.Vorname}".Trim(' ', ',');
            ApplyDtoToUi(_member);
            await LoadParzellenAsync(mitgliedId);

            _isEditMode = false;
            _isDirty = false;
            UpdateEditState();
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }
    private async Task GoToHauptmitgliedAsync()
    {
        if (_hauptmitgliedId == null)
            return;

        if (_isEditMode)
            await CancelAsync();

        _memberSelection.SelectedMitgliedId = _hauptmitgliedId.Value;
        await LoadMemberAsync(_hauptmitgliedId.Value);
    }

    private void ApplyDtoToUi(MemberDTO dto)
    {
        _nachname.Text = dto.Nachname;
        _vorname.Text = dto.Vorname;
        _geburtsdatum.Date = (dto.Geburtsdatum ?? DateTime.Today).Date;

        _strasse.Text = dto.Strasse;
        _plz.Text = dto.PLZ;
        _ort.Text = dto.Ort;

        _telefon.Text = dto.Telefon;
        _mobil.Text = dto.Mobilnummer;
        _email.Text = dto.Email;
        _whatsapp.IsChecked = dto.WhatsappEinwilligung;

        _mitgliedSeit.Date = (dto.MitgliedSeit ?? DateTime.Today).Date;
        _aktiv.IsChecked = dto.Aktiv;
        _mitgliedEnde.Date = (dto.MitgliedEnde ?? DateTime.Today).Date;
        _mitgliedEnde.IsEnabled = _isEditMode && !_aktiv.IsChecked;

        if (_isHauptmitglied)
        {
            var value = (dto.ArbeitsstundenAltersregelTyp ?? "keine").Trim().ToLowerInvariant();
            var idx = -1;
            for (var i = 0; i < _altersregel.Items.Count; i++)
            {
                if (string.Equals(_altersregel.Items[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }
            _altersregel.SelectedIndex = idx;
        }
    }

    private void ApplyUiToDto(MemberDTO dto)
    {
        dto.Nachname = (_nachname.Text ?? string.Empty).Trim();
        dto.Vorname = (_vorname.Text ?? string.Empty).Trim();
        dto.Geburtsdatum = _geburtsdatum.Date;

        dto.Strasse = (_strasse.Text ?? string.Empty).Trim();
        dto.PLZ = (_plz.Text ?? string.Empty).Trim();
        dto.Ort = (_ort.Text ?? string.Empty).Trim();

        dto.Telefon = (_telefon.Text ?? string.Empty).Trim();
        dto.Mobilnummer = (_mobil.Text ?? string.Empty).Trim();
        dto.Email = (_email.Text ?? string.Empty).Trim();
        dto.WhatsappEinwilligung = _whatsapp.IsChecked;

        dto.MitgliedSeit = _mitgliedSeit.Date;
        dto.MitgliedEnde = _aktiv.IsChecked ? null : _mitgliedEnde.Date;

        if (_isHauptmitglied)
        {
            dto.ArbeitsstundenAltersregelTyp = _altersregel.SelectedItem as string ?? "keine";
        }
    }

    private async Task ToggleEditAsync()
    {
        if (_isEditMode)
        {
            await CancelAsync();
            return;
        }

        await EnterEditModeAsync();
    }

    private void MarkDirtyIfEditing()
    {
        if (!_isEditMode || _member == null || _originalSnapshot == null)
            return;

        var tmp = _member.Clone();
        ApplyUiToDto(tmp);
        _isDirty = !tmp.ValueEquals(_originalSnapshot);
        UpdateEditState();
    }

    private async Task EnterEditModeAsync()
    {
        if (_isEditMode || _loadedMitgliedId == null)
            return;

        if (string.IsNullOrWhiteSpace(_authService.CurrentUserId))
        {
            _status.Text = "Nicht angemeldet.";
            return;
        }

        SetBusy(true);
        try
        {
            var ok = await _supabaseService.TryLockMitgliedAsync(_loadedMitgliedId.Value, _authService.CurrentUserId);
            if (!ok)
            {
                _status.Text = "Mitglied ist gesperrt (wird gerade bearbeitet).";
                return;
            }

            _lockUserId = _authService.CurrentUserId;
            _isEditMode = true;
            _isDirty = false;
            UpdateEditState();
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task SaveAsync()
    {
        if (_isBusy)
            return;

        if (!_isEditMode || !_isDirty || _member == null || _originalSnapshot == null)
            return;

        if (string.IsNullOrWhiteSpace(_authService.CurrentUserId))
        {
            _status.Text = "Nicht angemeldet.";
            return;
        }

        SetBusy(true);
        try
        {
            var dto = _member.Clone();
            ApplyUiToDto(dto);

            var validationError = Validate(dto);
            if (!string.IsNullOrEmpty(validationError))
            {
                _status.Text = validationError;
                return;
            }

            var ok = await _supabaseService.UpdateMitgliedAsync(dto, _authService.CurrentUserId);
            if (!ok)
            {
                _status.Text = "Speichern fehlgeschlagen.";
                return;
            }

            _originalSnapshot.CopyFrom(dto);
            _member.CopyFrom(dto);

            _isDirty = false;
            _isEditMode = false;
            UpdateEditState();

            await ReleaseLockAsync(force: false);
            await LoadMemberAsync(dto.Id);
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static string? Validate(MemberDTO dto)
    {
        if (dto == null)
            return "Mitgliedsdaten fehlen.";

        if (string.IsNullOrWhiteSpace(dto.Nachname))
            return "Nachname ist Pflicht.";

        if (string.IsNullOrWhiteSpace(dto.Vorname))
            return "Vorname ist Pflicht.";

        return null;
    }

    private async Task CancelAsync()
    {
        if (!_isEditMode)
            return;

        if (_originalSnapshot != null)
            ApplyDtoToUi(_originalSnapshot);

        _isDirty = false;
        _isEditMode = false;
        UpdateEditState();

        await ReleaseLockAsync(force: false);
    }

    private async void OnAssignParzelleClicked(object? sender, EventArgs e)
    {
        if (!_isEditMode)
        {
            await DisplayAlert("Hinweis", "Zum Zuordnen bitte erst 'Bearbeiten' aktivieren.", "OK");
            return;
        }

        if (_member == null)
            return;

        if (_freeParzellePicker.SelectedItem is not ParzelleOption parz)
            return;

        SetBusy(true);
        try
        {
            var ok = await _supabaseService.AssignParzelleToMitgliedAsync(_member.Id, parz.ParzelleId, _assignVonDate.Date.Date);
            if (!ok)
            {
                await DisplayAlert("Fehler", "Zuweisung fehlgeschlagen.", "OK");
                return;
            }

            await LoadParzellenAsync(_member.Id);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", ex.Message, "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnEndBelegungClicked(object? sender, EventArgs e)
    {
        if (!_isEditMode)
        {
            await DisplayAlert("Hinweis", "Zum Beenden bitte erst 'Bearbeiten' aktivieren.", "OK");
            return;
        }

        if (_member == null || _selectedBelegung == null)
            return;

        if (_selectedBelegung.BisDatum != null)
            return;

        SetBusy(true);
        try
        {
            var ok = await _supabaseService.EndParzellenBelegungAsync(_selectedBelegung.BelegungId, DateTime.Today);
            if (!ok)
            {
                await DisplayAlert("Fehler", "Belegung konnte nicht beendet werden.", "OK");
                return;
            }

            await LoadParzellenAsync(_member.Id);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", ex.Message, "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task LoadParzellenAsync(int mitgliedId)
    {
        _parzellenBelegungen.Clear();
        _availableParzellen.Clear();
        _selectedBelegung = null;
        _assignVonDate.Date = DateTime.Today;

        var parzellen = await _supabaseService.GetAllParzellenAsync();
        var memberBelegungen = await _supabaseService.GetBelegungenForMitgliedAsync(mitgliedId);
        var allBelegungen = await _supabaseService.GetAllParzellenBelegungenAsync();

        var parzById = parzellen.ToDictionary(p => p.Id, p => p);

        foreach (var b in memberBelegungen
                     .OrderByDescending(x => x.BisDatum == null)
                     .ThenByDescending(x => x.VonDatum ?? DateTime.MinValue))
        {
            parzById.TryGetValue(b.ParzelleId, out var p);

            _parzellenBelegungen.Add(new ParzellenBelegungItem(
                belegungId: b.Id,
                parzelleId: b.ParzelleId,
                gartenNr: p?.GartenNr ?? $"#{b.ParzelleId}",
                anlage: p?.Anlage ?? string.Empty,
                von: b.VonDatum?.Date,
                bis: b.BisDatum?.Date));
        }

        var preferredParzelleId = _parzelleSelection.SelectedParzelleId;
        _selectedBelegung = preferredParzelleId.HasValue
            ? _parzellenBelegungen.FirstOrDefault(x => x.ParzelleId == preferredParzelleId.Value)
            : _parzellenBelegungen.FirstOrDefault(x => x.BisDatum == null) ?? _parzellenBelegungen.FirstOrDefault();

        if (_selectedBelegung != null)
        {
            _parzelleSelection.SelectedParzelleId = _selectedBelegung.ParzelleId;
            _parzelleSelection.GartenNr = _selectedBelegung.GartenNr;
        }

        RenderParzellenButtons();
        UpdateParzellenButtonStyles();

        var today = DateTime.Today;

        var activeToday = allBelegungen
            .GroupBy(b => b.ParzelleId)
            .Select(g => g.Where(x =>
                    (x.VonDatum ?? DateTime.MinValue).Date <= today &&
                    (x.BisDatum == null || x.BisDatum.Value.Date >= today))
                .OrderByDescending(x => x.VonDatum ?? DateTime.MinValue)
                .FirstOrDefault())
            .Where(x => x != null)
            .ToDictionary(x => x!.ParzelleId, x => x!);

        foreach (var p in parzellen
                     .OrderBy(x => GetGartenNrSortKey(x.GartenNr))
                     .ThenBy(x => x.GartenNr, StringComparer.CurrentCultureIgnoreCase))
        {
            if (!activeToday.ContainsKey(p.Id))
                _availableParzellen.Add(new ParzelleOption(p.Id, p.GartenNr ?? $"#{p.Id}", p.Anlage));
        }

        _freeParzellePicker.ItemsSource = _availableParzellen;
        _freeParzellePicker.ItemDisplayBinding = new Binding(nameof(ParzelleOption.Display));
        _freeParzellePicker.SelectedIndex = -1;

        UpdateEditState();
    }

    private void ClearParzellenUi(bool clearContext)
    {
        _parzellenBelegungen.Clear();
        _availableParzellen.Clear();
        _selectedBelegung = null;
        _parzellenButtons.Children.Clear();
        _parzellenButtonsCreated.Clear();
        _freeParzellePicker.ItemsSource = null;
        _freeParzellePicker.SelectedIndex = -1;

        if (clearContext)
        {
            _parzelleSelection.SelectedParzelleId = null;
            _parzelleSelection.GartenNr = null;
        }
    }

    private void RenderParzellenButtons()
    {
        _parzellenButtonsChildrenClear();

        if (_parzellenBelegungen.Count == 0)
        {
            _parzellenButtons.Children.Add(new Label { Text = "Keine Parzellen vorhanden.", Opacity = 0.7 });
            _parzelleSelection.SelectedParzelleId = null;
            _parzelleSelection.GartenNr = null;
            return;
        }

        foreach (var b in _parzellenBelegungen)
        {
            var btn = new Button
            {
                Text = b.GartenDisplay,
                CornerRadius = 16,
                HeightRequest = 42,
                FontSize = 13,
                Padding = new Thickness(14, 10),
                Margin = new Thickness(0, 0, 8, 8)
            };

            btn.Clicked += (_, __) => SelectBelegung(b);

            _parzellenButtonsCreated.Add(btn);
            _parzellenButtons.Children.Add(btn);
        }
    }

    private void _parzellenButtonsChildrenClear()
    {
        _parzellenButtons.Children.Clear();
        _parzellenButtonsCreated.Clear();
    }

    private void SelectBelegung(ParzellenBelegungItem b)
    {
        if (_isBusy)
            return;

        _selectedBelegung = b;
        _parzelleSelection.SelectedParzelleId = b.ParzelleId;
        _parzelleSelection.GartenNr = b.GartenNr;
        UpdateParzellenButtonStyles();
        UpdateEditState();
    }

    private void UpdateParzellenButtonStyles()
    {
        var selectedId = _selectedBelegung?.ParzelleId;

        var primary = GetColorResource("KgvPrimary", Colors.DarkOliveGreen);
        var surface = GetColorResource("KgvSurface", Colors.LightGray);
        var text = GetColorResource("KgvText", Colors.Black);

        for (var i = 0; i < _parzellenBelegungen.Count && i < _parzellenButtonsCreated.Count; i++)
        {
            var b = _parzellenBelegungen[i];
            var btn = _parzellenButtonsCreated[i];

            var isSelected = selectedId.HasValue && b.ParzelleId == selectedId.Value;
            btn.BackgroundColor = isSelected ? primary : surface;
            btn.TextColor = isSelected ? Colors.White : text;
        }
    }

    private async Task ReleaseLockAsync(bool force)
    {
        if (_loadedMitgliedId == null || string.IsNullOrWhiteSpace(_lockUserId))
            return;

        try
        {
            await _supabaseService.ReleaseLockMitgliedAsync(_loadedMitgliedId.Value, _lockUserId, force);
        }
        catch
        {
        }
        finally
        {
            _lockUserId = null;
        }
    }

    private void UpdateEditState()
    {
        // Während Laden/Speichern UI defensiv sperren, um Nebenläufigkeit zu vermeiden.
        if (_isBusy)
        {
            _nachname.IsReadOnly = true;
            _vorname.IsReadOnly = true;
            _geburtsdatum.IsEnabled = false;

            _strasse.IsReadOnly = true;
            _plz.IsReadOnly = true;
            _ort.IsReadOnly = true;

            _telefon.IsReadOnly = true;
            _mobil.IsReadOnly = true;
            _email.IsReadOnly = true;
            _whatsapp.IsEnabled = false;

            _mitgliedSeit.IsEnabled = false;
            _aktiv.IsEnabled = false;
            _mitgliedEnde.IsEnabled = false;

            _altersregel.IsEnabled = false;

            _editButton.IsEnabled = false;
            _saveButton.IsEnabled = false;
            _cancelButton.IsEnabled = false;
            _goToHauptmitgliedButton.IsEnabled = false;

            _saveButton.IsVisible = _isEditMode;
            _cancelButton.IsVisible = _isEditMode;

            _assignParzelleButton.IsEnabled = false;
            _endBelegungButton.IsEnabled = false;

            _freeParzellePicker.IsEnabled = false;
            _assignVonDate.IsEnabled = false;

            _freeParzellePicker.IsVisible = _isEditMode;
            _assignVonDate.IsVisible = _isEditMode;
            _assignParzelleButton.IsVisible = _isEditMode;
            _endBelegungButton.IsVisible = _isEditMode;

            foreach (var btn in _parzellenButtonsCreated)
                btn.IsEnabled = false;

            return;
        }

        _nachname.IsReadOnly = !_isEditMode;
        _vorname.IsReadOnly = !_isEditMode;
        _geburtsdatum.IsEnabled = _isEditMode;

        _strasse.IsReadOnly = !_isEditMode;
        _plz.IsReadOnly = !_isEditMode;
        _ort.IsReadOnly = !_isEditMode;

        _telefon.IsReadOnly = !_isEditMode;
        _mobil.IsReadOnly = !_isEditMode;
        _email.IsReadOnly = !_isEditMode;
        _whatsapp.IsEnabled = _isEditMode;

        _mitgliedSeit.IsEnabled = _isEditMode;
        _aktiv.IsEnabled = _isEditMode;
        _mitgliedEnde.IsEnabled = _isEditMode && !_aktiv.IsChecked;

        _altersregel.IsEnabled = _isEditMode && _isHauptmitglied;

        _editButton.IsEnabled = _loadedMitgliedId != null;
        _saveButton.IsEnabled = _isEditMode && _isDirty;
        _cancelButton.IsEnabled = _isEditMode;
        _goToHauptmitgliedButton.IsEnabled = !_isEditMode && _hauptmitgliedId.HasValue;

        _saveButton.IsVisible = _isEditMode;
        _cancelButton.IsVisible = _isEditMode;

        _editModeHint.IsVisible = _isEditMode;
        if (_isEditMode)
        {
            _editButton.Text = "Bearbeiten (aktiv)";
            _editButton.BackgroundColor = Colors.DarkOrange;
            _editButton.TextColor = Colors.White;
        }
        else
        {
            _editButton.Text = "Bearbeiten";
            _editButton.BackgroundColor = Colors.Transparent;
            _editButton.TextColor = Colors.Black;
        }

        _assignParzelleButton.IsEnabled = _isEditMode
            && _availableParzellen.Count > 0
            && _freeParzellePicker.SelectedItem is ParzelleOption;
        _endBelegungButton.IsEnabled = _isEditMode && _selectedBelegung != null && _selectedBelegung.BisDatum == null;

        _freeParzellePicker.IsEnabled = _isEditMode && _availableParzellen.Count > 0;
        _assignVonDate.IsEnabled = _isEditMode;

        _freeParzellePicker.IsVisible = _isEditMode;
        _assignVonDate.IsVisible = _isEditMode;
        _assignParzelleButton.IsVisible = _isEditMode;
        _endBelegungButton.IsVisible = _isEditMode;

        foreach (var btn in _parzellenButtonsCreated)
            btn.IsEnabled = true;
    }

    private void SetBusy(bool value)
    {
        _isBusy = value;
        _busy.IsVisible = value;
        _busy.IsRunning = value;
        UpdateEditState();
    }

    private static int GetGartenNrSortKey(string? gartenNr)
    {
        if (string.IsNullOrWhiteSpace(gartenNr))
            return int.MaxValue;

        var digits = new string(gartenNr.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : int.MaxValue;
    }

    private sealed record ParzelleOption(int ParzelleId, string GartenNr, string? Anlage)
    {
        public string Display => string.IsNullOrWhiteSpace(Anlage) ? GartenNr : $"{GartenNr} ({Anlage})";
    }

    private sealed class ParzellenBelegungItem
    {
        public ParzellenBelegungItem(int belegungId, int parzelleId, string gartenNr, string anlage, DateTime? von, DateTime? bis)
        {
            BelegungId = belegungId;
            ParzelleId = parzelleId;
            GartenNr = gartenNr;
            Anlage = anlage;
            VonDatum = von;
            BisDatum = bis;
        }

        public int BelegungId { get; }
        public int ParzelleId { get; }
        public string GartenNr { get; }
        public string Anlage { get; }
        public DateTime? VonDatum { get; }
        public DateTime? BisDatum { get; }

        public string GartenDisplay => string.IsNullOrWhiteSpace(Anlage) ? GartenNr : $"{GartenNr} ({Anlage})";
    }
}
