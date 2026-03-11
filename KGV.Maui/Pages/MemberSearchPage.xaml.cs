using KGV.Maui.ViewModels;
using KGV.Maui.State;

namespace KGV.Maui.Pages;

public partial class MemberSearchPage : ContentPage
{
    private readonly MemberSearchViewModel _vm;
    private readonly MemberSelectionState _memberSelection;
    private readonly ParzelleSelectionState _parzelleSelection;

    private Task? _initTask;
    private bool _isNavigating;

    public MemberSearchPage(MemberSearchViewModel vm, MemberSelectionState memberSelection, ParzelleSelectionState parzelleSelection)
    {
        InitializeComponent();

        _vm = vm;
        _memberSelection = memberSelection;
        _parzelleSelection = parzelleSelection;
        BindingContext = _vm;

        Appearing += MemberSearchPage_Appearing;
    }

    private async void MemberSearchPage_Appearing(object? sender, EventArgs e)
    {
        await EnsureInitializedAsync();
    }

    private Task EnsureInitializedAsync()
    {
        // Guard gegen parallele Initialisierung (schnelles Navigieren / mehrfaches Appearing)
        if (_initTask != null && !_initTask.IsCompleted)
            return _initTask;

        _initTask = _vm.InitializeAsync();
        return _initTask;
    }

    private async void ResultsCollectionView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isNavigating || _vm.IsBusy)
        {
            if (sender is CollectionView cv0)
                cv0.SelectedItem = null;
            return;
        }

        var item = e.CurrentSelection?.FirstOrDefault() as MemberSearchResultItem;

        if (sender is CollectionView cv)
            cv.SelectedItem = null;

        var member = await _vm.SelectResultAsync(item);
        if (member == null)
            return;

        _isNavigating = true;
        try
        {
            _memberSelection.SelectedMitgliedId = member.Id;

            // Defensiv: Kontextwechsel -> Parzellenkontext zurücksetzen, um stale Daten in Garten-Seiten zu vermeiden.
            _parzelleSelection.SelectedParzelleId = null;
            _parzelleSelection.GartenNr = null;

            await Shell.Current.GoToAsync("//memberdetail");
        }
        finally
        {
            _isNavigating = false;
        }
    }
}
