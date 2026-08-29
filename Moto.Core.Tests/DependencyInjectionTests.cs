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

            // Chaque ServiceType ne doit apparaître qu'une seule fois
            var duplicates = services
                .GroupBy(d => d.ServiceType)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key.Name)
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
            Assert.Contains(types, t => t.Name == "InlayHintService");
            Assert.Contains(types, t => t.Name == "CrdtSession");
            Assert.Contains(types, t => t.Name == "MarketplaceClientPro");
            Assert.Contains(types, t => t.Name == "DebugEnginePro");
        }
    }
}
