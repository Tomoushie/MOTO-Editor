// Moto.Editor/Views/EvolutionPanelView.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Moto.Core.AI.Evolution;

namespace Moto.Editor.Views
{
    public partial class EvolutionPanelView : ContentView
    {
        private List<EvolutionSuggestion> _items = new();

        public event Action<EvolutionSuggestion> Accepted;
        public event Action<EvolutionSuggestion> Rejected;

        public EvolutionPanelView()
        {
            InitializeComponent();
        }

        public void Load(List<EvolutionSuggestion> suggestions)
        {
            _items = suggestions;
            SuggestionList.ItemsSource = _items.ToList();
        }

        private void OnAcceptClicked(object sender, EventArgs e)
        {
            if (((Button)sender).BindingContext is EvolutionSuggestion s)
            {
                _items.Remove(s);
                SuggestionList.ItemsSource = _items.ToList();
                Accepted?.Invoke(s);
            }
        }

        private void OnRejectClicked(object sender, EventArgs e)
        {
            if (((Button)sender).BindingContext is EvolutionSuggestion s)
            {
                _items.Remove(s);
                SuggestionList.ItemsSource = _items.ToList();
                Rejected?.Invoke(s);
            }
        }
    }
}
