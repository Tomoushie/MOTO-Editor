// Moto.Editor/Views/TerminalPanelView.xaml.cs
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Moto.Editor.ViewModels;

namespace Moto.Editor.Views
{
    /// <summary>
    /// Dock du bas (Terminal). Aucune logique propre : affiche TerminalLines,
    /// envoie TerminalInput via SendTerminalCommand — tout le reste (le vrai
    /// shell, le streaming de sortie) vit dans Moto.Core.Services.TerminalService,
    /// piloté par MainViewModel (voir le commentaire du .xaml).
    /// </summary>
    public partial class TerminalPanelView : ContentView
    {
        public TerminalPanelView()
        {
            InitializeComponent();

            BindingContextChanged += (s, e) =>
            {
                if (BindingContext is MainViewModel vm)
                {
                    // ★ AJOUT (01/09) : défilement automatique vers la dernière ligne
                    // à chaque sortie du shell — sans ça, suivre un build/une commande
                    // longue obligerait à faire défiler manuellement à chaque ligne.
                    vm.TerminalLines.CollectionChanged += OnLinesChanged;
                    // ★ AJOUT (01/09, revue croisée) : re-défile aussi quand le dock
                    // passe de masqué à visible — sans ça, un dock ouvert après un
                    // long historique déjà accumulé (build lancé avant sa première
                    // ouverture) pouvait s'afficher figé en HAUT du journal, sans
                    // indice qu'il y avait davantage de sortie plus bas.
                    vm.PropertyChanged += OnViewModelPropertyChanged;
                }
            };
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MainViewModel.IsTerminalVisible)) return;
            if (BindingContext is not MainViewModel vm) return;

            // ★ AJOUT (01/09, revue croisée) : démarre le shell dès l'ouverture du
            // dock si aucun ne tourne déjà (mode Expert, menu Affichage > Terminal…
            // peuvent rendre le dock visible sans qu'aucun dossier n'ait été ouvert)
            // — sinon le panneau a l'air actif mais taper une commande n'a
            // silencieusement aucun effet.
            if (vm.IsTerminalVisible)
                vm.EnsureTerminalRunning();

            ScrollToBottomIfVisible();
        }

        private void OnLinesChanged(object sender, NotifyCollectionChangedEventArgs e) => ScrollToBottomIfVisible();

        /// <summary>
        /// ★ CORRECTION (01/09, revue croisée) : ne défile que si le dock est
        /// RÉELLEMENT visible — appeler ScrollTo sur une CollectionView jamais
        /// arrangée (ligne "Auto" repliée à 0 tant que le dock est masqué, même
        /// patron IsVisible qu'ailleurs dans ce projet) n'a de toute façon aucun
        /// sens visuel, et évite par la même occasion tout risque théorique lié
        /// à l'arrangement WinUI de ce genre de contrôle (historique documenté
        /// dans MainPage.xaml).
        /// </summary>
        private void ScrollToBottomIfVisible()
        {
            if (BindingContext is not MainViewModel vm || !vm.IsTerminalVisible || vm.TerminalLines.Count == 0)
                return;
            MainThread.BeginInvokeOnMainThread(() =>
                LinesList.ScrollTo(vm.TerminalLines.Count - 1, position: ScrollToPosition.End, animate: false));
        }

        private void OnCloseClicked(object sender, System.EventArgs e)
        {
            if (BindingContext is MainViewModel vm)
                vm.IsTerminalVisible = false;
        }

        private void OnSendClicked(object sender, System.EventArgs e)
        {
            if (BindingContext is MainViewModel vm && vm.SendTerminalCommand.CanExecute(null))
                vm.SendTerminalCommand.Execute(null);
        }
    }
}
