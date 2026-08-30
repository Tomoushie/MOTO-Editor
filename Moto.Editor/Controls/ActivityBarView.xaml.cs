// Moto.Editor/Controls/ActivityBarView.xaml.cs
using System;
using Microsoft.Maui.Controls;

namespace Moto.Editor.Controls
{
    public partial class ActivityBarView : ContentView
    {
        /// <summary>Id de l'activité sélectionnée (explorer, search, ai, cortex, collab, settings).</summary>
        public event Action<string> ActivitySelected;

        public ActivityBarView()
        {
            InitializeComponent();
        }

        private void OnItemTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is string id)
            {
                ActivitySelected?.Invoke(id);
            }
        }
    }
}
