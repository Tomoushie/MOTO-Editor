// Moto.Editor/MainPage.InlineEdit.cs
// ★ AJOUT (24/09, "écriture générative fonctionnelle") : le bandeau IA de l'éditeur (bouton 🤖) — édition EN LIGNE d'une sélection (ou du fichier).
// Avant : la réponse du modèle REMPLAÇAIT tout le texte de l'éditeur, sans diff ni confirmation, même si le bloc de code n'était qu'un extrait
// ou avait été coupé — et rien ne permettait de revenir en arrière. Maintenant : le modèle écrit en flux, la réponse est contrôlée
// (Moto.Core/Moto.AI/Generation), l'utilisateur voit le diff et accepte ou refuse, et « ↩ Annuler » remet le fichier comme avant.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Moto.Core.AI.Generation;
using Moto.Core.Settings;
using Moto.Editor.Models;
using Moto.Editor.Services;

namespace Moto.Editor
{
    public partial class MainPage
    {
        private readonly InlineEditService _inlineEdit = new();

        /// <summary>Non nul tant qu'une édition en ligne est en cours (une seule à la fois).</summary>
        private CancellationTokenSource? _inlineEditCts;

        /// <summary>Modifications faites par le bandeau IA, de la plus ancienne à la plus récente — de quoi revenir en arrière (l'éditeur ne garde pas d'historique d'annulation après un remplacement de texte).</summary>
        private readonly List<AiEditUndo> _aiEditUndo = new();

        private const int MaxAiEditUndo = 20;

        /// <param name="Source">Le bloc du chat dont vient la modification (« Appliquer »), sinon null : son état dira « annulé » après ↩.</param>
        private sealed record AiEditUndo(EditorDocument Document, string Before, string After, ChatContentSegment? Source = null);

        /// <summary>Le bandeau IA a reçu une demande (modèle choisi, texte tapé).</summary>
        private async void OnAiBandPrompt(string model, string prompt)
        {
            if (_inlineEditCts is not null)
            {
                EditorPane.SetAiStatus("Une modification est déjà en cours : attends la fin, ou arrête-la avec ■.");
                return;
            }

            var doc = _viewModel.SelectedDocument;
            if (doc == null) { EditorPane.SetAiStatus("Ouvre un fichier à modifier."); return; }

            var request = new InlineEditRequest
            {
                DisplayPath = string.IsNullOrWhiteSpace(doc.Path) ? doc.Title : doc.Path,
                DocumentText = EditorPane.EditorText ?? string.Empty,
                Selection = EditorPane.GetSelectedText(),
                Instruction = prompt,
            };

            using var cts = new CancellationTokenSource();
            _inlineEditCts = cts; // posé AVANT la question ci-dessous : pas de 2e demande pendant qu'elle est affichée
            EditorPane.SetAiBusy(true);

            try
            {
                if (ChatService.IsExternalProviderName(model))
                {
                    EditorPane.SetAiStatus($"[{model}] Service en ligne : dis-moi si ton code peut partir…");
                    if (!await ConfirmOnlineSendAsync(model, request))
                    {
                        EditorPane.SetAiStatus($"[{model}] Rien n'a été envoyé : ton code est resté sur ta machine. (« MOTO interne » travaille sans rien envoyer.)");
                        return;
                    }
                    cts.Token.ThrowIfCancellationRequested();
                }

                EditorPane.SetAiStatus($"[{model}] {(request.Scope == InlineEditScope.Selection ? "Sélection envoyée" : "Fichier envoyé")}…");
                var outcome = await RunInlineEditAsync(model, request, cts.Token);
                if (!outcome.Succeeded)
                {
                    EditorPane.SetAiStatus($"[{ModelLabel(outcome, model)}] {outcome.Problem}");
                    return;
                }

                var plan = outcome.Plan!;

                // L'utilisateur a pu taper ou changer d'onglet pendant que le modèle écrivait : le plan a été calculé sur un texte périmé.
                if (!StillSameText(doc, request))
                {
                    EditorPane.SetAiStatus($"[{ModelLabel(outcome, model)}] Le fichier a changé pendant que le modèle écrivait : rien n'a été appliqué. Relance ta demande.");
                    return;
                }

                EditorPane.SetAiStatus($"[{ModelLabel(outcome, model)}] Proposition prête ({plan.Diff.Summary}) : à toi de voir…");
                if (!await ConfirmInlineEditAsync(doc, prompt, outcome))
                {
                    EditorPane.SetAiStatus($"[{ModelLabel(outcome, model)}] Modification refusée : rien n'a été modifié.");
                    return;
                }

                // Le fichier a pu bouger pendant la lecture du diff (l'overlay ne bloque pas les frappes clavier vers l'éditeur).
                if (!StillSameText(doc, request))
                {
                    EditorPane.SetAiStatus($"[{ModelLabel(outcome, model)}] Le fichier a changé pendant que tu regardais le diff : rien n'a été appliqué. Relance ta demande.");
                    return;
                }

                ApplyInlineEdit(doc, request.DocumentText, plan.NewText);
                EditorPane.SetAiStatus($"[{ModelLabel(outcome, model)}] Modification appliquée ({plan.Diff.Summary}). « ↩ Annuler » remet le fichier comme avant."
                                       + (outcome.Note is { } note ? $" {note}" : string.Empty));
            }
            catch (OperationCanceledException)
            {
                EditorPane.SetAiStatus($"[{model}] Génération arrêtée : rien n'a été modifié.");
            }
            catch (Exception ex)
            {
                EditorPane.SetAiStatus("Erreur IA : " + ex.Message + " Rien n'a été modifié.");
            }
            finally
            {
                _inlineEditCts = null;
                EditorPane.SetAiBusy(false);
            }
        }

        /// <summary>Le bouton ■ du bandeau IA : arrête l'appel au modèle.</summary>
        private void OnAiBandCancel()
        {
            try { _inlineEditCts?.Cancel(); }
            catch (ObjectDisposedException) { /* l'édition venait de se terminer */ }
        }

        private Task<InlineEditOutcome> RunInlineEditAsync(string model, InlineEditRequest request, CancellationToken ct)
        {
            void Progress(string message) => MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_inlineEditCts is not null) EditorPane.SetAiStatus(message); // pas de message tardif après la fin ou l'arrêt
            });

            return _chatService.TrackAsync("Bandeau IA (édition)", model, () => ChatService.IsExternalProviderName(model)
                    ? _inlineEdit.RunWithAsync(request, model, (fullPrompt, _) => _chatService.AskRawAsync(model, fullPrompt), ct)
                    : _inlineEdit.RunAsync(request, Progress, ct),
                outcome => outcome.Plan?.Replacement.Length ?? 0);
        }

        /// <summary>
        /// ★ AJOUT (25/09, confidentialité — choix A de Tom) : un modèle EN LIGNE choisi dans le sélecteur du bandeau reçoit du code (le passage
        /// sélectionné et ses lignes voisines, ou le fichier entier). Jusqu'ici il partait sans prévenir. Sans le service de confirmation, rien ne part.
        /// Le sélecteur ne choisit pas le service : la demande passe par le FallbackEngine, qui essaie les services configurés dans Réglages → IA
        /// par ordre de priorité — d'où la formulation « le premier qui répond ».
        /// </summary>
        private async Task<bool> ConfirmOnlineSendAsync(string model, InlineEditRequest request)
        {
            if (_confirmationService is null) return false;

            var answer = await _confirmationService.RequestAsync(new ConfirmationRequest
            {
                Action = ConfirmationAction.SendCodeOnline,
                Title = "🌐 Ton code va quitter ta machine",
                Message = $"Pour cette modification, {InlineEditPrompts.DescribeSentContent(request)} vont partir vers un service en ligne : "
                        + $"celui configuré dans Réglages → IA (le premier qui répond, pas forcément « {model} »).\n"
                        + "Une fois envoyé, on ne peut plus le reprendre.\n"
                        + "Pour garder ton code sur ta machine : « Ne pas envoyer », puis choisis « MOTO interne » dans le sélecteur du bandeau.",
                ConfirmText = "Envoyer",
                CancelText = "Ne pas envoyer",
                IsDestructive = true, // bouton rouge : ce qui part ne revient pas
            });
            return answer.Confirmed;
        }

        /// <summary>Le modèle réellement utilisé (celui des réglages, ou son remplaçant) plutôt que l'étiquette du sélecteur (« MOTO interne »).</summary>
        private static string ModelLabel(InlineEditOutcome outcome, string picked)
            => string.IsNullOrWhiteSpace(outcome.Model) ? picked : outcome.Model;

        private bool StillSameText(EditorDocument doc, InlineEditRequest request) => StillSameText(doc, request.DocumentText);

        /// <summary>Le même onglet est toujours affiché, avec exactement ce texte (sinon un plan calculé dessus serait faux).</summary>
        private bool StillSameText(EditorDocument doc, string text)
            => ReferenceEquals(_viewModel.SelectedDocument, doc) && string.Equals(EditorPane.EditorText, text, StringComparison.Ordinal);

        /// <summary>La boîte de confirmation de l'éditeur, avec le diff. Sans elle (service absent) on refuse : jamais d'écriture sans accord.</summary>
        private async Task<bool> ConfirmInlineEditAsync(EditorDocument doc, string prompt, InlineEditOutcome outcome)
        {
            if (_confirmationService is null) return false;

            var plan = outcome.Plan!;
            var lines = new List<string>
            {
                $"Demande : « {Shorten(prompt, 140)} »",
                $"{ModelLabel(outcome, "modèle")} · {plan.Diff.Summary} · {(plan.Scope == InlineEditScope.Selection ? "sélection" : "fichier entier")}",
            };
            if (!string.IsNullOrWhiteSpace(outcome.Note)) lines.Add(outcome.Note!);
            lines.AddRange(plan.Warnings.Select(w => "⚠ " + w));

            var answer = await _confirmationService.RequestAsync(new ConfirmationRequest
            {
                Action = ConfirmationAction.ModifyCode,
                Title = $"🤖 Modification proposée — {doc.Title}",
                Message = string.Join("\n", lines),
                Details = plan.Diff.Unified,
                DetailsAreDiff = true,
                ConfirmText = "Appliquer",
                CancelText = "Refuser",
            });
            return answer.Confirmed;
        }

        private void ApplyInlineEdit(EditorDocument doc, string before, string after, ChatContentSegment? source = null)
        {
            EditorPane.EditorText = after;
            doc.Text = after;
            if (!string.IsNullOrEmpty(doc.Path)) _cortex?.LearnFromCode(doc.Path, after);

            _aiEditUndo.Add(new AiEditUndo(doc, before, after, source));
            if (_aiEditUndo.Count > MaxAiEditUndo) _aiEditUndo.RemoveAt(0);
            RefreshAiUndoButton();
        }

        /// <summary>« ↩ Annuler » (et la commande de la palette) : remet le fichier affiché comme avant sa dernière modification par le bandeau IA.</summary>
        private async void UndoLastAiEdit()
        {
            var doc = _viewModel.SelectedDocument;
            var entry = _aiEditUndo.LastOrDefault(e => ReferenceEquals(e.Document, doc));
            if (doc is null || entry is null)
            {
                EditorPane.SetAiStatus("Aucune modification du bandeau IA à annuler dans ce fichier.");
                return;
            }

            try
            {
                // Depuis la modification, l'utilisateur a peut-être retravaillé le texte : l'annulation écraserait ce travail.
                if (!string.Equals(EditorPane.EditorText, entry.After, StringComparison.Ordinal)
                    && !await DisplayAlert("↩ Annuler la modification de l'IA",
                        "Tu as modifié ce fichier depuis la modification de l'IA. L'annuler le remettra comme avant l'IA : ce que tu as écrit depuis sera perdu.",
                        "Annuler quand même", "Garder"))
                    return;

                if (!ReferenceEquals(_viewModel.SelectedDocument, doc)) return; // changé d'onglet pendant la question

                EditorPane.EditorText = entry.Before;
                doc.Text = entry.Before;
                _aiEditUndo.Remove(entry);
                RefreshAiUndoButton();
                EditorPane.SetAiStatus("Modification de l'IA annulée : le fichier est comme avant.");
                // Sinon le bloc du chat afficherait encore « ✔ Appliqué » alors que le fichier est revenu en arrière.
                if (entry.Source is { } block) block.ApplyStatus = $"↩ Annulé : « {doc.Title} » est revenu comme avant ce code.";
            }
            catch (Exception ex)
            {
                EditorPane.SetAiStatus("Annulation impossible : " + ex.Message);
            }
        }

        /// <summary>« ↩ Annuler » n'est visible que si le fichier affiché a une modification IA à défaire.</summary>
        private void RefreshAiUndoButton()
        {
            var doc = _viewModel.SelectedDocument;
            EditorPane.SetAiUndoAvailable(doc is not null && _aiEditUndo.Any(e => ReferenceEquals(e.Document, doc)));
        }

        /// <summary>Un onglet fermé n'a plus rien à défaire (et ne doit pas rester retenu en mémoire par l'historique d'annulation).</summary>
        private void ForgetAiEdits(EditorDocument doc)
        {
            _aiEditUndo.RemoveAll(e => ReferenceEquals(e.Document, doc));
            RefreshAiUndoButton();
        }

        /// <summary>
        /// « Expliquer » (menu Cortex). Une explication n'est pas une modification : avant, cette action passait par l'édition du bandeau IA
        /// (« réponds avec le code COMPLET modifié ») puis REMPLAÇAIT le fichier par le premier bloc de code de la réponse.
        /// ★ CHANGÉ (24/09, chat en flux) : la question part dans le panneau de chat, qui s'ouvre, et la réponse s'y écrit en direct — avec le
        /// fichier affiché et la sélection, même en mode « Chat ». Le fichier n'est jamais touché.
        /// </summary>
        private async void ExplainCurrentCode()
        {
            var doc = _viewModel.SelectedDocument;
            if (doc == null) { StatusBar.SetStatus("Ouvre un fichier à expliquer."); return; }

            var whole = string.IsNullOrWhiteSpace(EditorPane.GetSelectedText());
            ShowAiChatPanel();

            try
            {
                var answer = await _chatService.SendAsync(whole
                        ? $"Explique simplement, en quelques phrases, ce que fait le fichier {doc.Title}. Ne réécris pas le code."
                        : "Explique simplement, en quelques phrases, ce que fait le code sélectionné. Ne réécris pas le code.",
                    includeActiveFile: true);
                if (answer is null)
                    StatusBar.SetStatus("⏳ Une réponse est déjà en train de s'écrire dans le chat : attends-la, ou arrête-la avec ■.");
            }
            catch (Exception ex)
            {
                StatusBar.SetStatus("Erreur IA : " + ex.Message);
            }
        }

        private static string Shorten(string? text, int max)
        {
            var flat = (text ?? string.Empty).Replace('\n', ' ').Trim();
            return flat.Length <= max ? flat : flat[..max] + "…";
        }
    }
}
