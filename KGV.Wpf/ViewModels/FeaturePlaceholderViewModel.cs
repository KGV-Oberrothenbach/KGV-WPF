namespace KGV.Wpf.ViewModels
{
    public abstract class FeaturePlaceholderViewModel : BaseViewModel
    {
        public string TitleText { get; }
        public string HintText { get; }

        protected FeaturePlaceholderViewModel(string title, string hint)
        {
            TitleText = title;
            HintText = hint;
        }
    }
}
