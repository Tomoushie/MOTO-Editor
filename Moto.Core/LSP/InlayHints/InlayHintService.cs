// Moto.Core/LSP/InlayHints/InlayHintService.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.LSP.InlayHints
{
    /// <summary>
    /// Service d'inlay hints avec debounce (jamais de travail inutile).
    /// </summary>
    public sealed class InlayHintService
    {
        private readonly IInlayHintProvider _provider;
        private readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(300);
        private CancellationTokenSource? _cts;

        /// <summary>Déclenché quand de nouveaux hints sont disponibles.</summary>
        public event Action<IReadOnlyList<InlayHint>>? HintsUpdated;

        public InlayHintService(IInlayHintProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>Demande un refresh (debounce appliqué).</summary>
        public void RequestRefresh(string filePath, string content, int startLine, int endLine)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(_debounce, token);
                    var hints = await _provider.GetHintsAsync(filePath, content, startLine, endLine);
                    if (!token.IsCancellationRequested)
                        HintsUpdated?.Invoke(hints);
                }
                catch (OperationCanceledException) { /* debounce annulé */ }
            }, token);
        }
    }
}
