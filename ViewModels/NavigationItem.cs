using System;
using System.Collections.Generic;
using System.Windows;

namespace KGV.ViewModels
{
    public class NavigationItem
    {
        public string Title { get; set; } = string.Empty;

        public Thickness ButtonMargin { get; set; } = new Thickness(5);

        /// <summary>
        /// Ziel-ViewModel-Typ (ViewModel-first Navigation).
        /// Beispiel: typeof(MemberSearchViewModel), typeof(MemberDetailViewModel), ...
        /// </summary>
        public Type? ViewModelType { get; set; }

        /// <summary>
        /// Optionaler Parameter für Navigation (z.B. MemberDTO, Id, etc.)
        /// </summary>
        public object? Parameter { get; set; }

        public bool IsAdminOnly { get; set; } = false;
        public bool IsVorstandOnly { get; set; } = false;

        /// <summary>
        /// Wird im XAML genutzt, um Buttons ein-/auszublenden.
        /// </summary>
        public bool IsVisible { get; set; } = true;

        public List<NavigationItem>? SubItems { get; set; }
    }
}