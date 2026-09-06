// Moto.Core.Tests/DependencyInjectionTests.cs
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Moto.Editor.DependencyInjection;
using Xunit;

namespace Moto.Core.Tests
{
    /// <summary>Valide qu'aucun service n'est enregistré en double (point 9).</summary>
    public class DependencyInjectionTests
    {
        [Fact]
        public void RegisterMotoServices_HasNoDuplicateRegistrations()
        {
            var services = new ServiceCollection();
            services.AddLogging(); // requis par certaines factories (non résolues ici)

            services.RegisterMotoServices();

            // Chaque paire (ServiceType, ImplementationType) ne doit apparaître qu'une
            // seule fois. Pas juste ServiceType seul (06/09, corrigé) : ISpecializedAgent
            // est enregistré ~17 fois par design (un par agent concret, résolu ensuite via
            // IEnumerable<ISpecializedAgent>) — un vrai doublon, c'est le MÊME type
            // concret enregistré deux fois pour le même service, pas une interface à
            // implémentations multiples.
            var duplicates = services
                .GroupBy(d => (d.ServiceType, d.ImplementationType))
                .Where(g => g.Count() > 1)
                .Select(g => $"{g.Key.ServiceType.Name} -> {g.Key.ImplementationType?.Name ?? "(factory)"}")
                .ToList();

            Assert.Empty(duplicates);
        }

        [Fact]
        public void RegisterMotoServices_RegistersKeyServices()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.RegisterMotoServices();

            var types = services.Select(d => d.ServiceType).ToList();

            Assert.Contains(types, t => t.Name == "WindowManager");
            Assert.Contains(types, t => t.Name == "ProactiveAnalyticsEngine");
            Assert.Contains(types, t => t.Name == "AgentOrchestratorV3");
            // InlayHintService retiré de cette liste (06/09) : son unique IInlayHintProvider
            // concret (RoslynInlayHintProvider) est explicitement exclu de la compilation —
            // LSP/Roslyn "v1.0, à venir" (voir Moto.Core.csproj) — donc jamais enregistré,
            // pas un oubli. MotoServiceCollectionExtensions.cs le documente déjà (ligne
            // "IInlayHintProvider/InlayHintService : LSP mis de côté pour cette passe").
            Assert.Contains(types, t => t.Name == "CrdtSession");
            Assert.Contains(types, t => t.Name == "MarketplaceClientPro");
            Assert.Contains(types, t => t.Name == "DebugEnginePro");
        }
    }
}
