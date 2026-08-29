// Moto.Editor/Views/AutoLinkView.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Moto.Core.AI.AutoLink;

namespace Moto.Editor.Views
{
    public partial class AutoLinkView : ContentView
    {
        private List<AutoLinkAction> _actions = new();

        /// <summary>Déclenché quand l'utilisateur clique sur Apply.</summary>
        public event Action<AutoLinkAction> ApplyRequested;

        /// <summary>Déclenché quand l'utilisateur clique sur Dismiss.</summary>
        public event Action<AutoLinkAction> DismissRequested;

        public AutoLinkView()
        {
            InitializeComponent();
        }

        public void Load(AutoLinkReport report)
        {
            _actions = report.Actions.ToList();
            ActionsList.ItemsSource = _actions;
        }

        public void Clear()
        {
            _actions.Clear();
            ActionsList.ItemsSource = _actions;
        }

        private void OnApplyClicked(object sender, EventArgs e)
        {
            if (((Button)sender).BindingContext is AutoLinkAction action)
            {
                _actions.Remove(action);
                ActionsList.ItemsSource = _actions.ToList();
                ApplyRequested?.Invoke(action);
            }
        }

        private void OnDismissClicked(object sender, EventArgs e)
        {
            if (((Button)sender).BindingContext is AutoLinkAction action)
            {
                _actions.Remove(action);
                ActionsList.ItemsSource = _actions.ToList();
                DismissRequested?.Invoke(action);
            }
        }
    }
}
