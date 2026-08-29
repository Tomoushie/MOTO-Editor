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

        private void OnClicked(object sender, EventArgs e)
        {
            if (sender is Button b && b.CommandParameter is string id)
            {
                ActivitySelected?.Invoke(id);
            }
        }
    }
}
