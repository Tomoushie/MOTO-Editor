// Moto.Editor/Platforms/Windows/MauiTitleBarBand.cs
// ★ AJOUT (25/09, passe « moyen → élevé ») : la bande de 32 DIP en haut de la fenêtre — bleue chez Tom quand Windows colore les
// barres de titre, noire quand la fenêtre est inactive — n'est PAS peinte par Windows. C'est la barre de titre par défaut de MAUI
// lui-même : un élément « AppTitleBarContainer » du gabarit de WindowRootView, qui affiche le titre de la fenêtre (d'où l'ancien
// contournement Title = " ") avec les couleurs de légende du système. Mesuré le 25/09 dans l'arbre natif : AppTitleBarContainer
// h = 32, et la barre MOTO (CustomMenuBarView) commençait à y = 32. Les 6 tentatives du 01-08/09 réglaient Windows (AppWindowTitleBar,
// OverlappedPresenter, DWM) : aucune ne pouvait atteindre un élément XAML de MAUI. MOTO dessine déjà sa propre barre : on replie
// celle de MAUI, et on la garde repliée (MAUI la ré-affiche à certains changements de présentation, ex. sortie du plein écran).
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Moto.Editor.Platforms.Windows;

internal static class MauiTitleBarBand
{
    private static readonly string[] PartNames = { "AppTitleBarContainer", "AppTitleBarContentControl" };

    // Éléments déjà « épinglés » (abonnés au rappel de visibilité) — une seule fois chacun, sans toucher à leur Tag.
    private static readonly ConditionalWeakTable<FrameworkElement, object> Pinned = new();

    /// <summary>
    /// Replie la barre de titre interne de MAUI et retire la marge haute qu'il réserve pour elle au-dessus du contenu
    /// (« ContentGrid » de sa vue de navigation, marge haute = 32 mesurée le 25/09 : replier la barre seule laissait la bande vide).
    /// Renvoie le nombre d'éléments traités (0 = rien trouvé : MAUI a changé de gabarit).
    /// </summary>
    public static int Collapse(Microsoft.UI.Xaml.Window window)
    {
        if (window.Content is not DependencyObject root) return 0;
        var count = 0;
        foreach (var element in Descendants(root, maxDepth: 16))
        {
            if (element is not FrameworkElement fe) continue;
            if (Array.IndexOf(PartNames, fe.Name) >= 0) { Pin(fe); count++; }
            else if (fe.Name == "ContentGrid" && IsInsideNavigationView(fe)) { PinNoTopMargin(fe); count++; }
        }
        return count;
    }

    private static bool IsInsideNavigationView(FrameworkElement fe)
    {
        for (DependencyObject? p = VisualTreeHelper.GetParent(fe); p is not null; p = VisualTreeHelper.GetParent(p))
            if (p is Microsoft.UI.Xaml.Controls.NavigationView) return true;
        return false;
    }

    private static void PinNoTopMargin(FrameworkElement fe)
    {
        if (fe.Margin.Top != 0) fe.Margin = new Microsoft.UI.Xaml.Thickness(fe.Margin.Left, 0, fe.Margin.Right, fe.Margin.Bottom);
        if (Pinned.TryGetValue(fe, out _)) return;
        Pinned.Add(fe, new object());
        fe.RegisterPropertyChangedCallback(FrameworkElement.MarginProperty, (sender, _) =>
        {
            if (sender is FrameworkElement f && f.Margin.Top != 0) f.Margin = new Microsoft.UI.Xaml.Thickness(f.Margin.Left, 0, f.Margin.Right, f.Margin.Bottom);
        });
    }

    private static void Pin(FrameworkElement fe)
    {
        fe.MaxHeight = 0;                       // MAUI ne touche jamais à MaxHeight : la hauteur reste nulle même s'il ré-affiche l'élément
        fe.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        if (Pinned.TryGetValue(fe, out _)) return;
        Pinned.Add(fe, new object());
        fe.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (sender, _) =>
        {
            if (sender is FrameworkElement f && f.Visibility != Microsoft.UI.Xaml.Visibility.Collapsed) f.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        });
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root, int maxDepth)
    {
        var stack = new Stack<(DependencyObject Node, int Depth)>();
        stack.Push((root, 0));
        while (stack.Count > 0)
        {
            var (node, depth) = stack.Pop();
            yield return node;
            if (depth >= maxDepth) continue;
            var n = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < n; i++) stack.Push((VisualTreeHelper.GetChild(node, i), depth + 1));
        }
    }
}
