using System;

namespace KGV.Wpf.Messages
{
    public sealed record NavigateToViewModelMessage(Type ViewModelType, object? Parameter = null);
}
