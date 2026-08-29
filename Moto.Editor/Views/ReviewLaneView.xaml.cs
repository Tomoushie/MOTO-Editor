using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Moto.Core.Collab;

namespace Moto.Editor.Views;

/// <summary>
/// Items 70/73 — Review Lane légère (P1). Consomme ReviewLaneService.
/// Affiche/édite les commentaires ; n'applique aucune opération structurée (réservée à XENO).
/// </summary>
public partial class ReviewLaneView : ContentView
{
    private readonly ReviewLaneService _review;
    private readonly CollabRoleService _roles;
    private string _currentFilePath = "";

    public ObservableCollection<ReviewCommentItem> Items { get; } = new();

    public ReviewLaneView(ReviewLaneService review, CollabRoleService roles)
    {
        InitializeComponent();
        _review = review;
        _roles = roles;

        BindingContext = this;
        CommentsList.ItemsSource = Items;

        _review.CommentsChanged += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
        _roles.RoleChanged += (_, role) => MainThread.BeginInvokeOnMainThread(() =>
            ModeLabel.Text = $"Mode : {role}");

        ModeLabel.Text = $"Mode : {_roles.CurrentRole}";
    }

    /// <summary>À appeler quand l'éditeur change de fichier actif.</summary>
    public void SetFile(string filePath)
    {
        _currentFilePath = filePath;
        Refresh();
    }

    private void Refresh()
    {
        Items.Clear();
        foreach (var c in _review.GetCommentsForFile(_currentFilePath)
                                 .Where(c => c.Status == ReviewCommentStatus.Open))
        {
            Items.Add(new ReviewCommentItem
            {
                Id = c.Id,
                LineLabel = $"L.{c.Line}",
                Author = c.Author,
                Text = c.Text
            });
        }
    }

    private void OnAddClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CommentEntry.Text)) return;
        int.TryParse(LineEntry.Text, out int line);

        _review.AddComment(new ReviewComment
        {
            FilePath = _currentFilePath,
            Line = line,
            Author = Environment.UserName,
            Text = CommentEntry.Text.Trim()
        });

        CommentEntry.Text = "";
        LineEntry.Text = "";
    }

    private void OnResolveClicked(object? sender, EventArgs e)
    {
        if (sender is Button b && b.CommandParameter is string id)
        {
            if (!_roles.CanReview) return; // garde-fou basé sur le rôle
            _review.SetStatus(id, ReviewCommentStatus.Resolved);
        }
    }
}

/// <summary>ViewModel léger d'affichage (évite d'exposer le modèle métier directement).</summary>
public sealed class ReviewCommentItem
{
    public string Id { get; set; } = "";
    public string LineLabel { get; set; } = "";
    public string Author { get; set; } = "";
    public string Text { get; set; } = "";
}
