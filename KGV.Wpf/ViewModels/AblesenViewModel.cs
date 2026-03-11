using KGV.Wpf.Helpers;
using KGV.Wpf.Messages;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace KGV.Wpf.ViewModels
{
    public sealed class AblesenViewModel : BaseViewModel
    {
        public ICommand OpenAblesungErfassenCommand { get; }
        public ICommand OpenZaehlerwechselCommand { get; }
        public ICommand OpenRfidEinrichtenCommand { get; }
        public ICommand OpenFaelligeZaehlerCommand { get; }

        public AblesenViewModel()
        {
            OpenAblesungErfassenCommand = new RelayCommand<object?>(_ => NavigateTo(typeof(AblesungErfassenViewModel)));
            OpenZaehlerwechselCommand = new RelayCommand<object?>(_ => NavigateTo(typeof(ZaehlerwechselScanViewModel)));
            OpenRfidEinrichtenCommand = new RelayCommand<object?>(_ => NavigateTo(typeof(RfidEinrichtenViewModel)));
            OpenFaelligeZaehlerCommand = new RelayCommand<object?>(_ => NavigateTo(typeof(FaelligeZaehlerViewModel)));
        }

        private static void NavigateTo(Type vmType)
        {
            WeakReferenceMessenger.Default.Send(new NavigateToViewModelMessage(vmType));
        }
    }
}
