// Moto.Editor/Controls/EditorPaneView.TabVariants.cs (partial)
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Moto.Core.AI;

namespace Moto.Editor.Controls
{
    public partial class EditorPaneView
    {
        private TabVariantsEngine? _tabVariantsEngine;
        private bool _isTabVariantsActive = false;

        public void InitializeTabVariants(TabVariantsEngine engine)
        {
            _tabVariantsEngine = engine;
        }

        /// <summary>
        /// Déclenché par TAB : génère des variantes de code.
        /// </summary>
        public async Task TriggerTabVariantsAsync()
        {
            if (_tabVariantsEngine == null || _isTabVariantsActive)
                return;

            _isTabVariantsActive = true;
            SetAiStatus("🔄 Génération des variantes…");

            try
            {
                var currentCode = GetSelectedText();
                var filePath = GetCurrentFilePath();
                var projectStructure = GetProjectStructure();

                var variants = await _tabVariantsEngine.GenerateVariantsAsync(
                    currentCode, filePath, projectStructure);

                if (variants.Count > 0)
                {
                    ShowVariantsOverlay(variants);
                }
                else
                {
                    SetAiStatus("Aucune variante générée.");
                }
            }
            catch (Exception ex)
            {
                SetAiStatus($"Erreur : {ex.Message}");
            }
            finally
            {
                _isTabVariantsActive = false;
            }
        }

        private void ShowVariantsOverlay(System.Collections.Generic.IReadOnlyList<CodeVariant> variants)
        {
            // Afficher un overlay avec les variantes
            SetAiStatus($"✅ {variants.Count} variante(s) disponible(s) - Ctrl+TAB pour naviguer");
        }

        private string GetCurrentFilePath()
        {
            // À implémenter selon votre structure
            return "";
        }

        private string GetProjectStructure()
        {
            // À implémenter selon votre structure
            return "";
        }
    }
}
