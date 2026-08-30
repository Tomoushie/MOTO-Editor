// Moto.Editor/Controls/EditorPaneView.cs (v31 corrigé — CRDT + Image + partial)
using System;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Moto.Core.Collab;
using Moto.Editor.Models;
using Moto.Editor.Views;

namespace Moto.Editor.Controls;

/// <summary>
/// EditorPaneView v31 : panneau d'édition avec CRDT multi-curseurs + support image.
/// Fichier partial — les autres partials contiennent : tabs, gutter, minimap, inlay hints.
/// </summary>
public partial class EditorPaneView
{
    // ── CRDT & collaboration ──
    private CrdtCollabSession? _crdtSession;
    private CrdtCursorRenderer? _cursorRenderer;
    private RemoteCursorOverlay? _cursorOverlay;

    /// <summary>
    /// Initialise le support CRDT multi-curseurs.
    /// </summary>
    public void InitializeCrdt(CrdtCollabSession session, CrdtCursorRenderer cursorRenderer)
    {
        _crdtSession = session;
        _cursorRenderer = cursorRenderer;

        _cursorOverlay = new RemoteCursorOverlay
        {
            InputTransparent = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        // ★ CORRECTION : EditorPaneView est un ContentView, jamais un Grid — c'est son
        // Content (le Grid racine du .xaml) qu'il faut viser pour ajouter l'overlay.
        if (Content is Grid rootGrid)
            rootGrid.Children.Add(_cursorOverlay);

        // S'abonner aux événements
        _crdtSession.RemoteCursorMoved += cursor =>
        {
            _cursorRenderer.UpdateCursor(new RemoteCursorView
            {
                UserId = cursor.UserId,
                DisplayName = cursor.DisplayName,
                Color = cursor.Color,
                DocumentPath = cursor.DocumentId,
                Line = cursor.Line,
                Column = cursor.Column,
                LastSeenUtc = cursor.LastUpdateUtc
            });
        };

        _crdtSession.DocumentChanged += newContent =>
        {
            MainThread.BeginInvokeOnMainThread(() => EditorText = newContent);
        };

        _cursorRenderer.CursorsChanged += cursors =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
                _cursorOverlay?.RenderCursors(cursors));
        };
    }

    /// <summary>
    /// Envoie la position du curseur local aux pairs (à appeler sur CursorMoved).
    /// </summary>
    public async Task BroadcastCursorAsync(int line, int column)
    {
        if (_crdtSession == null) return;
        await _crdtSession.UpdateCursorAsync(line, column);
    }

    /// <summary>
    /// Rejoint une session collaborative.
    /// </summary>
    public async Task JoinCollabSessionAsync(string serverUrl, string displayName)
    {
        if (_crdtSession == null) return;
        await _crdtSession.JoinAsync(serverUrl, displayName);
    }

    /// <summary>
    /// Ouvre une image dans un nouvel onglet (même flux qu'un fichier de code).
    /// À appeler depuis FileTreeService quand ImageDocument.IsSupported(path).
    /// </summary>
    public void OpenImage(ImageDocument doc)
    {
        var viewer = new ImageViewerView();
        viewer.LoadDocument(doc);

        // Fenêtre externe thémée (Windows) : mise de côté pour cette passe — voir
        // Moto.Editor.csproj (ExternalImageWindow a des ambiguïtés de types MAUI/WinUI
        // jamais résolues). Le bouton ⛶ reste sans effet tant que ce n'est pas rebranché.

        // Ajoute un onglet via le mécanisme existant des tabs
        AddTabForImage(doc, viewer);
    }

    /// <summary>
    /// Crée l'onglet image. Réutilise le même conteneur de tabs que les fichiers de code.
    /// (Implémentation dans un autre fichier partial ou à relier à votre méthode interne.)
    /// </summary>
    partial void AddTabForImage(ImageDocument doc, ImageViewerView viewer);
}
