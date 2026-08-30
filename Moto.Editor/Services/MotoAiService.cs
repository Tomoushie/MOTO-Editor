// Moto.Editor/Services/MotoAiService.cs (v2)
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
    }
}
