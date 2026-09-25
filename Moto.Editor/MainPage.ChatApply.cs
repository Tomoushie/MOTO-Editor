// Moto.Editor/MainPage.ChatApply.cs
// ★ AJOUT (25/09, « Appliquer » dans le chat — brique 3 de l'écriture générative, choix de Tom) : le bouton « Appliquer » d'un bloc de code
// d'une réponse du chat pose ce code dans le fichier affiché. Jusqu'ici il fallait « Copier », puis trouver soi-même où coller — et ne pas
// laisser l'ancienne version en double. OÙ poser est décidé par Moto.Core (CodeApplyPlanner, testé) ; ici : lire l'éditeur, montrer le diff,
// attendre l'accord, appliquer — avec le même « ↩ Annuler » que le bandeau IA. Rien ne part vers un modèle : tout se passe sur la machine.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moto.Core.AI.Generation;
using Moto.Core.Settings;
using Moto.Editor.Models;

namespace Moto.Editor
{
    public partial class MainPage
    {
        /// <summary>Vrai pendant qu'un bloc du chat attend l'accord de l'utilisateur (une seule boîte de confirmation à la fois).</summary>
        private bool _chatApplyBusy;

        /// <summary>« Appliquer » sur un bloc de code du chat (ChatService.ApplyCodeHandler). Le résultat s'affiche sous le bloc.</summary>
        private async Task ApplyChatCodeAsync(ChatContentSegment segment)
        {
            if (_chatApplyBusy)
            {
                segment.ApplyStatus = "Un autre bloc attend ta réponse dans la fenêtre principale : réponds-y d'abord.";
                return;
            }
            if (_inlineEditCts is not null)
            {
                segment.ApplyStatus = "Le bandeau IA est en train de modifier le fichier : attends la fin (ou arrête-le avec ■), puis reclique sur Appliquer.";
                return;
            }

            var doc = _viewModel.SelectedDocument;
            if (doc is null)
            {
                segment.ApplyStatus = "Aucun fichier n'est ouvert : ouvre celui où poser ce code, puis reclique sur Appliquer.";
                return;
            }

            _chatApplyBusy = true;
            var applied = false;
            try
            {
                var before = EditorPane.EditorText ?? string.Empty;
                var normalized = InlineEditPlanner.Normalize(before);
                // Sélection ou curseur, seulement si l'éditeur a été cliqué depuis que ce texte y a été posé (sinon : endroit inconnu).
                var range = EditorPane.GetSelectionRange();
                var selection = range is { Length: > 0 } r && r.Start + r.Length <= normalized.Length ? normalized.Substring(r.Start, r.Length) : null;

                // Le chat a écrit ce bloc pour un AUTRE fichier du projet : le poser dans celui affiché serait presque toujours une erreur.
                if (selection is null && OtherProjectFile(segment.PathHint, doc) is { } other)
                {
                    segment.ApplyStatus = $"Ce code est pour « {Path.GetFileName(other)} », pas pour « {doc.Title} » : ouvre ce fichier, puis reclique sur Appliquer. "
                                        + "(Pour le mettre quand même ici : sélectionne dans l'éditeur le passage à remplacer, puis reclique.) Rien n'a été modifié.";
                    return;
                }

                var outcome = CodeApplyPlanner.Plan(new CodeApplyRequest
                {
                    DisplayPath = string.IsNullOrWhiteSpace(doc.Path) ? doc.Title : doc.Path,
                    DocumentText = before,
                    Selection = selection,
                    SelectionStart = selection is null ? null : range!.Value.Start,
                    CaretIndex = range?.Start,
                    Code = segment.Text,
                    PathHint = segment.PathHint,
                    IsComplete = segment.IsComplete,
                });
                if (!outcome.Succeeded)
                {
                    segment.ApplyStatus = "⚠ " + outcome.Problem;
                    return;
                }

                var plan = outcome.Plan!;
                segment.ApplyStatus = "⏳ Regarde la fenêtre principale : le diff t'attend.";
                if (!await ConfirmChatApplyAsync(doc, plan))
                {
                    segment.ApplyStatus = "Refusé : rien n'a été modifié.";
                    return;
                }

                // Le fichier a pu bouger pendant la lecture du diff (autre onglet, frappe clavier) : le plan serait faux.
                if (!StillSameText(doc, before))
                {
                    segment.ApplyStatus = "Le fichier a changé pendant que tu regardais le diff : rien n'a été appliqué. Reclique sur Appliquer.";
                    return;
                }

                ApplyInlineEdit(doc, before, plan.NewText, segment);
                applied = true;
                EditorPane.ShowAiBand(); // pour que « ↩ Annuler » soit visible
                EditorPane.SetAiStatus($"[Chat] Code appliqué dans « {doc.Title} » ({plan.Diff.Summary}). « ↩ Annuler » remet le fichier comme avant.");
                segment.ApplyStatus = $"✔ Appliqué dans « {doc.Title} » — {plan.Description} · {plan.Diff.Summary}. "
                                    + "« ↩ Annuler » (bandeau IA de l'éditeur) remet le fichier comme avant.";
            }
            catch (Exception ex)
            {
                App.LogCrash("MainPage.ApplyChatCodeAsync", ex);
                segment.ApplyStatus = applied ? "⚠ Code appliqué, mais erreur ensuite : " + ex.Message : "⚠ Erreur : " + ex.Message + " Rien n'a été modifié.";
            }
            finally
            {
                _chatApplyBusy = false;
            }
        }

        /// <summary>La boîte de confirmation de l'éditeur, avec le diff. Sans elle (service absent) on refuse : jamais d'écriture sans accord.</summary>
        private async Task<bool> ConfirmChatApplyAsync(EditorDocument doc, CodeApplyPlan plan)
        {
            if (_confirmationService is null) return false;

            var lines = new List<string> { $"{plan.Description} · {plan.Diff.Summary}" };
            lines.AddRange(plan.Warnings.Select(w => "⚠ " + w));
            lines.Add("Vérifie l'endroit (numéro de ligne) dans le diff. Pas le bon ? « Refuser », sélectionne dans l'éditeur le passage à remplacer "
                    + "(ou clique là où insérer), puis reclique sur Appliquer.");

            var answer = await _confirmationService.RequestAsync(new ConfirmationRequest
            {
                Action = ConfirmationAction.ModifyCode,
                Title = $"🤖 Appliquer le code du chat — {doc.Title}",
                Message = string.Join("\n", lines),
                Details = plan.Diff.Unified,
                DetailsAreDiff = true,
                ConfirmText = "Appliquer",
                CancelText = "Refuser",
            });
            return answer.Confirmed;
        }

        /// <summary>
        /// Le fichier du projet que désigne le nom annoncé par le chat pour ce bloc, s'il est AUTRE que le fichier affiché. Null : pas de nom,
        /// même fichier, ou fichier introuvable — sans doute un fichier à créer ; le code va alors dans le fichier affiché et le diff le signale.
        /// </summary>
        private string? OtherProjectFile(string? hint, EditorDocument doc)
        {
            if (string.IsNullOrWhiteSpace(hint)) return null;
            var relative = hint.Trim().Replace('\\', '/');
            while (relative.StartsWith("./", StringComparison.Ordinal)) relative = relative[2..];
            relative = relative.TrimStart('/');
            var name = Path.GetFileName(relative);
            if (name.Length == 0
                || string.Equals(name, doc.Title, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, Path.GetFileName(doc.Path ?? string.Empty), StringComparison.OrdinalIgnoreCase))
                return null;

            // Onglets ouverts et fichiers du projet : même chemin relatif de préférence, sinon même nom.
            var known = _viewModel.Documents.Select(d => d.Path).Concat(_viewModel.Files.Select(f => f.Path))
                .Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            var match = known.FirstOrDefault(p => p.Replace('\\', '/').EndsWith("/" + relative, StringComparison.OrdinalIgnoreCase))
                     ?? known.FirstOrDefault(p => string.Equals(Path.GetFileName(p), name, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;

            // Puis à côté du fichier affiché, ou depuis la racine du projet.
            foreach (var folder in new[] { Path.GetDirectoryName(doc.Path ?? string.Empty), _currentRoot })
            {
                if (string.IsNullOrWhiteSpace(folder)) continue;
                try
                {
                    var path = Path.GetFullPath(Path.Combine(folder, relative));
                    if (File.Exists(path)) return path;
                }
                catch (Exception)
                {
                    // Nom de fichier invalide (caractères interdits…) : ce n'est pas un fichier du projet.
                }
            }
            return null;
        }
    }
}
