// Moto.Editor/Views/CollabView.xaml.cs — AJOUTS CRDT
// Ajouter dans la classe existante CollabView

private CrdtCollabSession? _crdtSession;
private CrdtCursorRenderer? _cursorRenderer;
private RemoteCursorOverlay? _cursorOverlay;

/// <summary>
/// Initialise la session CRDT avec le service de curseurs.
/// </summary>
public void InitializeCrdt(CrdtCollabSession session, CrdtCursorRenderer cursorRenderer)
{
    _crdtSession = session;
    _cursorRenderer = cursorRenderer;

    // S'abonner aux événements
    _crdtSession.RemoteCursorMoved += OnRemoteCursorMoved;
    _crdtSession.DocumentChanged += OnDocumentChanged;
    _crdtSession.PeersChanged += OnPeersChanged;

    // Créer l'overlay de curseurs
    _cursorOverlay = new RemoteCursorOverlay
    {
        InputTransparent = true,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill
    };

    // Ajouter l'overlay au conteneur principal
    if (Content is Grid rootGrid)
        rootGrid.Children.Add(_cursorOverlay);
}

private void OnRemoteCursorMoved(CrdtCursorInfo cursor)
{
    if (_cursorRenderer == null) return;

    var view = new RemoteCursorView
    {
        UserId = cursor.UserId,
        DisplayName = cursor.DisplayName,
        Color = cursor.Color,
        DocumentPath = cursor.DocumentId,
        Line = cursor.Line,
        Column = cursor.Column,
        LastSeenUtc = cursor.LastUpdateUtc
    };

    _cursorRenderer.UpdateCursor(view);
    RefreshCursorOverlay();
}

private void OnDocumentChanged(string newContent)
{
    MainThread.BeginInvokeOnMainThread(() =>
    {
        // Mettre à jour le contenu de l'éditeur
        // (à adapter selon l'API de votre éditeur)
        System.Diagnostics.Debug.WriteLine($"[CRDT] Document mis à jour ({newContent.Length} chars)");
    });
}

private void OnPeersChanged(IReadOnlyList<CrdtPeerInfo> peers)
{
    MainThread.BeginInvokeOnMainThread(() =>
    {
        // Mettre à jour la liste des pairs dans l'UI
        StatusLabel.Text = $"👥 {peers.Count} utilisateur(s) connecté(s)";
    });
}

private void RefreshCursorOverlay()
{
    if (_cursorRenderer == null || _cursorOverlay == null) return;

    MainThread.BeginInvokeOnMainThread(() =>
    {
        var cursors = _cursorRenderer.GetAllActiveCursors();
        _cursorOverlay.RenderCursors(cursors);
    });
}

/// <summary>
/// Envoie la position du curseur local aux pairs.
/// </summary>
public async Task SendLocalCursorAsync(int line, int column)
{
    if (_crdtSession == null) return;
    await _crdtSession.UpdateCursorAsync(line, column);
}
