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

        public Task<AiResponse> ExecuteAsync(AiRequest request)
        {
            return Task.Run(() => Kernel.Execute(request));
        }
    }
}
