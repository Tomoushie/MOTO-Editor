// Moto.Core/Preview/PreviewEngine.cs
// Moteur de prévisualisation web local (sandbox offline) + WebSocket live-reload.
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.Preview
{
    public sealed class PreviewRequest
    {
        public string SourcePath { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public string Language { get; init; } = "html";
    }

    public sealed class PreviewResult
    {
        public bool Success { get; init; }
        public string HtmlContent { get; init; } = string.Empty;
        public string PreviewFilePath { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>
    /// Prévisualiseur web local.
    /// - Wraps JS/CSS dans un HTML si besoin
    /// - Génère un fichier temporaire pour WebView
    /// - Auto-refresh via WebSocket local
    /// </summary>
    public sealed class PreviewEngine : IDisposable
    {
        private readonly string _previewDir;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private int _counter;
        private int _wsPort = 5050;

        public event Action<string>? PreviewUpdated;

        public PreviewEngine()
        {
            _previewDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MotoEditor", "previews");
            Directory.CreateDirectory(_previewDir);
        }

        /// <summary>
        /// Configure le port WebSocket pour le live-reload.
        /// </summary>
        public void SetWebSocketPort(int port)
        {
            _wsPort = port;
        }

        public async Task<PreviewResult> GeneratePreviewAsync(PreviewRequest request, CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var html = WrapIfNeeded(request);
                var fileName = $"preview_{Interlocked.Increment(ref _counter)}.html";
                var path = Path.Combine(_previewDir, fileName);

                await File.WriteAllTextAsync(path, html, Encoding.UTF8, ct).ConfigureAwait(false);

                PreviewUpdated?.Invoke(path);

                return new PreviewResult
                {
                    Success = true,
                    HtmlContent = html,
                    PreviewFilePath = path,
                    Message = $"Preview généré : {fileName}"
                };
            }
            catch (Exception ex)
            {
                return new PreviewResult
                {
                    Success = false,
                    Message = $"Erreur preview : {ex.Message}"
                };
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Détecte le langage et wrappe le contenu dans un HTML valide.
        /// </summary>
        private string WrapIfNeeded(PreviewRequest request)
        {
            var lang = request.Language.ToLowerInvariant();
            var ext = Path.GetExtension(request.SourcePath).ToLowerInvariant();

            if (lang == "html" || ext == ".html" || ext == ".htm")
                return InjectLiveReload(request.Content);

            if (lang == "css" || ext == ".css")
            {
                return $@"<!DOCTYPE html>
<html><head><meta charset=""utf-8"">
<style>{request.Content}</style>
{LiveReloadScript(_wsPort)}
</head><body>
<div class=""moto-preview"">
  <h1>CSS Preview</h1>
  <p>Paragraphe de démonstration.</p>
  <button>Bouton exemple</button>
  <ul><li>Item 1</li><li>Item 2</li></ul>
</div>
</body></html>";
            }

            if (lang == "javascript" || lang == "js" || ext == ".js")
            {
                return $@"<!DOCTYPE html>
<html><head><meta charset=""utf-8"">
{LiveReloadScript(_wsPort)}
</head><body>
<div id=""moto-output""></div>
<script>
try {{
  const __out = document.getElementById('moto-output');
  const __log = (...args) => {{
    const line = document.createElement('div');
    line.textContent = args.join(' ');
    __out.appendChild(line);
  }};
  console.log = __log;
  {request.Content}
}} catch (e) {{
  document.getElementById('moto-output').innerHTML =
    '<pre style=""color:red"">Erreur : ' + e.message + '</pre>';
}}
</script>
</body></html>";
            }

            if (lang == "java" || ext == ".java")
            {
                return $@"<!DOCTYPE html>
<html><head><meta charset=""utf-8""></head><body>
<h3>📄 Source Java (aperçu statique)</h3>
<pre style=""background:#1e1f24;color:#e5e7eb;padding:12px;border-radius:6px""><code>{System.Net.WebUtility.HtmlEncode(request.Content)}</code></pre>
</body></html>";
            }

            // Fallback : affichage source
            return $@"<!DOCTYPE html>
<html><head><meta charset=""utf-8""></head><body>
<pre>{System.Net.WebUtility.HtmlEncode(request.Content)}</pre>
</body></html>";
        }

        /// <summary>
        /// Injecte un script de live-reload dans le HTML source.
        /// </summary>
        private string InjectLiveReload(string html)
        {
            if (html.Contains("</body>", StringComparison.OrdinalIgnoreCase))
            {
                return Regex.Replace(
                    html,
                    "</body>",
                    LiveReloadScript(_wsPort) + "</body>",
                    RegexOptions.IgnoreCase);
            }
            return html + LiveReloadScript(_wsPort);
        }

        /// <summary>
        /// Script JavaScript qui se connecte au serveur WebSocket pour le live-reload.
        /// </summary>
        private static string LiveReloadScript(int wsPort = 5050)
        {
            return $@"<script>
(function() {{
  var MOTO_WS_PORT = {wsPort};
  var ws = null;
  var reconnectDelay = 1000;

  function connect() {{
    try {{
      ws = new WebSocket('ws://localhost:' + MOTO_WS_PORT + '/');
      ws.onopen = function() {{
        console.log('[MOTO] Live-reload connecté');
        reconnectDelay = 1000;
      }};
      ws.onmessage = function(e) {{
        try {{
          var msg = JSON.parse(e.data);
          if (msg.type === 'reload') {{
            console.log('[MOTO] Reload v' + msg.version);
            location.reload();
          }}
        }} catch(err) {{}}
      }};
      ws.onclose = function() {{
        setTimeout(connect, reconnectDelay);
        reconnectDelay = Math.min(reconnectDelay * 1.5, 5000);
      }};
      ws.onerror = function() {{ ws.close(); }};
    }} catch(e) {{
      setTimeout(connect, reconnectDelay);
    }}
  }}

  connect();
  window.__MOTO_PREVIEW_VERSION__ = Date.now();
}})();
</script>";
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_previewDir))
                {
                    foreach (var file in Directory.GetFiles(_previewDir, "preview_*.html"))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch { }
            _gate.Dispose();
        }
    }
}
