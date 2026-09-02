// Moto.Editor/Services/MotoAiService.cs (v2)
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Moto.Core.AI;
using Moto.Core.AI.Internal;
using Moto.Core.AI.Internal.Models;

namespace Moto.Editor.Services
{
    /// <summary>
    /// Service MAUI d'accès à MOTO AI.
    /// Expose le FallbackEngine (providers) et le Kernel (interne) en instance unique.
    /// </summary>
    public class MotoAiService
    {
        /// <summary>Instance unique partagée (page paramètres + chat).</summary>
        public FallbackEngine Fallback { get; } = new FallbackEngine();

        private MotoAiKernel _kernel;
        private string _workspace = string.Empty;

        /// <summary>Kernel interne, recréé si le workspace change.</summary>
        public MotoAiKernel Kernel => _kernel ??= new MotoAiKernel(_workspace, null);

        public void SetWorkspace(string path)
        {
            if (_workspace != path)
            {
                _workspace = path;
                _kernel = null;
            }
        }

        public async Task<AiResponse> ExecuteAsync(AiRequest request)
        {
            // ★ CORRECTION : MotoAiKernel n'a pas de méthode Execute(AiRequest) —
            // son seul point d'entrée est RouteAsync(string, ...).
            var response = await Kernel.RouteAsync(request.UserText, 256, System.Threading.CancellationToken.None);
            return response ?? new AiResponse { Success = false, Summary = "Aucune réponse du moteur IA." };
        }

        /// <summary>
        /// ★ AJOUT (02/09, réveil de MotoAiPage) : appelée par Pages/MotoAiPage.
        /// xaml.cs (OnApplyClicked), qui existait déjà mais appelait une méthode
        /// jamais écrite — confirmé par erreur de compilation, pas par supposition.
        /// N'écrase JAMAIS un fichier existant (texte déjà affiché à l'utilisateur
        /// dans MotoAiPage : "Fichiers appliqués sans écraser les fichiers
        /// existants.") — un fichier qui existe déjà est simplement ignoré.
        /// ⚠️ Limite connue, pas cachée : AiResponse.FileChanges n'est aujourd'hui
        /// JAMAIS rempli par ExecuteAsync ci-dessus (RouteAsync renvoie du texte
        /// libre, rien n'extrait de "changements de fichiers" structurés de cette
        /// réponse) — cette méthode fonctionnera dès que cette extraction existera,
        /// pas avant. Documenté dans CLAUDE.md plutôt que de construire cette
        /// extraction en urgence (chantier à part entière).
        /// </summary>
        public async Task ApplyChangesAsync(IEnumerable<AiFileChange> changes, string workspace)
        {
            foreach (var change in changes)
            {
                var fullPath = Path.Combine(workspace, change.Path);

                if (change.ChangeType == FileChangeType.Delete)
                {
                    if (File.Exists(fullPath))
                        File.Delete(fullPath);
                    continue;
                }

                if (File.Exists(fullPath))
                    continue; // ne jamais écraser un fichier existant

                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                await File.WriteAllTextAsync(fullPath, change.Content);
            }
        }
    }
}
