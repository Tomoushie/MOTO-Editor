using System;
using System.Threading.Tasks;
using Moto.Core.Licensing;
using Moto.Core.Plugins;
using Moto.Plugin.SDK;

namespace Moto.Plugins.MotoDarkPro
{
    public sealed class MotoDarkProPlugin : IPlugin, IMotoPlugin
    {
        public string Id => "moto-dark-pro";
        public string Name => "Moto Dark Pro";
        public string Version => "1.0.0";
        public string Author => "MOTO Team";
        public string Description => "Thème UI premium avec animations avancées et icônes HD (Achat unique 5€)";

        private readonly LicenseValidator _licenseValidator;
        private IPluginContext? _context;

        public MotoDarkProPlugin()
        {
            _licenseValidator = new LicenseValidator();
        }

        public void Initialize(IPluginContext context)
        {
            _context = context;
            _context.Log($"[{Name}] Initialisé (mode compatibilité)");
        }

        public async Task InitializeAsync(IPluginContext context)
        {
            _context = context;

            var status = _licenseValidator.Validate(Id);
            if (!status.IsValid)
            {
                context.ShowMessage($"⚠️ {Name} : {status.Reason}. Achetez la licence à 5€ pour l'activer.");
                // On ne bloque pas totalement, mais on désactive les features premium
                return;
            }

            context.RegisterCommand("/theme apply-pro", ApplyProTheme);
            await Task.CompletedTask;
        }

        public async Task ActivateAsync()
        {
            var status = _licenseValidator.Validate(Id);
            if (!status.IsValid)
            {
                _context?.ShowMessage($"❌ {Name} désactivé : licence requise (5€).");
                return;
            }

            _context?.Logger.Info("Moto Dark Pro activé avec succès");
            await Task.CompletedTask;
        }

        public async Task DeactivateAsync()
        {
            _context?.Logger.Info("Moto Dark Pro désactivé");
            await Task.CompletedTask;
        }

        public void Activate() { /* Compat legacy */ }
        public void Deactivate() { /* Compat legacy */ }
        public void Dispose() { /* Cleanup */ }

        private Task ApplyProTheme(CommandContext ctx)
        {
            ctx.ShowMessage("🎨 Thème Moto Dark Pro appliqué avec animations fluides.");
            return Task.CompletedTask;
        }
    }
}
