// MotoPluginTemplate/Plugin.cs
using Moto.Plugin.SDK;

namespace MotoPluginTemplate
{
    /// <summary>
    /// Plugin MOTO Editor : PLUGIN_DESCRIPTION
    /// </summary>
    public sealed class Plugin : IPlugin
    {
        private IPluginContext? _context;

        public string Id => "motoplugin-template";
        public string Name => "MotoPluginTemplate";
        public string Version => "1.0.0";
        public string Author => "PLUGIN_AUTHOR";
        public string Description => "PLUGIN_DESCRIPTION";

        public void Initialize(IPluginContext context)
        {
            _context = context;
            _context.Log($"[{Name}] Initialisé");
        }

        public void Activate()
        {
            _context?.Log($"[{Name}] Activé");

            // Exemple : enregistrer une commande slash
            _context?.RegisterCommand("/hello", args =>
            {
                _context.SetStatus($"👋 Hello from {Name}! Args: {args}");
            });
        }

        public void Deactivate()
        {
            _context?.Log($"[{Name}] Désactivé");
        }

        public void Dispose()
        {
            _context?.Log($"[{Name}] Déchargé");
        }
    }
}
