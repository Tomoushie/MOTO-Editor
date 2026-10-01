// Moto.Editor/Controls/CodeEditorView.xaml.cs (v5 — passe « moyen → élevé » du 25/09)
// ★ REPRIS (25/09, choix C de Tom : l'éditeur WebView redevient l'éditeur de tous les jours) : l'éditeur SkiaSharp
// (CodeEditorViewSkia) n'a toujours ni défilement (rien au-delà de ~34 lignes) ni clic/sélection à la souris — il reste dans le
// dépôt pour la suite du chantier, mais un logiciel « prêt pour une bêta » doit d'abord éditer un vrai fichier. Refait au passage :
//   - coloration PAR LANGAGE (C#, C/Java/Go/Rust…, JS/TS, Python, JSON, XML/XAML/HTML, Markdown, PowerShell, shell, CSS, YAML,
//     SQL, INI) — l'ancienne version appliquait une seule regex « pseudo-C# » à tous les fichiers ; recalcul limité aux lignes
//     modifiées (et à celles dont l'état — commentaire /* */ ouvert, etc. — change), pas tout le fichier à chaque touche ;
//   - habillage façon VS Code : police Cascadia Mono, ligne courante, numéro de ligne actif, barres de défilement fines,
//     sélection bleue, mini-carte redessinée ;
//   - Tab / Maj+Tab (indente ou désindente, aussi sur plusieurs lignes), Entrée garde l'indentation (+1 niveau après { [ ( :),
//     via execCommand : Ctrl+Z les défait normalement ;
//   - fins de ligne Windows (\r\n) PRÉSERVÉES : un textarea les convertit en \n, et la première frappe convertissait tout le
//     fichier (diff géant dans git) — le texte renvoyé à C# les retrouve ;
//   - GetSelectionRange() : même contrat que l'éditeur Skia (null tant que l'utilisateur n'a pas placé le curseur depuis que le
//     texte a été posé par programme) — c'est ce qui permet à « Appliquer » (chat) de ne jamais deviner un endroit.
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Moto.Editor.Controls
{
    /// <summary>
    /// Éditeur de code maison (WebView) : coloration par langage, numéros de ligne, mini-carte, texte fantôme (Tab pour accepter),
    /// pont JS ↔ C#.
    /// </summary>
    public partial class CodeEditorView : ContentView
    {
        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(CodeEditorView),
                string.Empty, BindingMode.TwoWay, propertyChanged: OnTextChanged);

        public static readonly BindableProperty FontSizeProperty =
            BindableProperty.Create(nameof(FontSizeMode), typeof(double), typeof(CodeEditorView),
                14.0, propertyChanged: OnFontSizeChanged);

        /// <summary>Contenu du document (two-way avec le JS).</summary>
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        /// <summary>Taille de police (réglable via paramètres, compatible SettingsApplier).</summary>
        public double FontSizeMode
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        /// <summary>Déclenché quand l'utilisateur tape dans l'éditeur.</summary>
        public event EventHandler<string> EditorChanged;

        /// <summary>
        /// ★ AJOUT (25/09) : raccourci de MOTO (« ctrl+s », « ctrl+shift+p », « ctrl+shift+i », « ctrl+b », « f5 », « f11 ») tapé
        /// pendant que le curseur est dans le code — les touches vont alors au WebView, pas aux raccourcis XAML de la fenêtre.
        /// </summary>
        public event Action<string>? ShortcutPressed;

        /// <summary>
        /// ★ AJOUT (01/10, décision C item 1 tranche 2) : sélection (ou curseur si
        /// Length = 0) changée par l'utilisateur. Levé aux deux points où <c>_lastRange</c>
        /// est mis à jour (message WebView2 « S » et repli moto://sel). Porte l'offset de
        /// début et la longueur, dans le repère du texte NORMALISÉ aux « \n » (voir
        /// PullSelectionAsync) — c'est à l'abonné de convertir en ligne/colonne.
        /// </summary>
        public event Action<(int Start, int Length)>? SelectionChanged;

        private bool _loaded;
        private bool _suppress;
        private double _pendingFontSize = 14.0;
        // ★ (31/08) : mémorisée même avant la fin du chargement du WebView, puis appliquée dans Navigated.
        private bool _pendingMinimapVisible = true;
        // ★ (01/10, lot auto_*) : mémorisée avant la fin du chargement du WebView puis appliquée dans Navigated
        // (même patron que _pendingMinimapVisible) — sans elle, auto_indent=false ne survivrait pas au redémarrage :
        // SetAutoIndent est un no-op tant que _loaded est false, et le JS repartirait sur AUTO_INDENT=true.
        private bool _pendingAutoIndent = true;
        private string _language = "plain";
        private string _lastSelection = string.Empty;
        private (int Start, int Length)? _lastRange;
        // Le dernier texte poussé utilisait des fins de ligne Windows : le textarea les rend en « \n », on les remet en le relisant.
        private bool _crlf;
        // ★ (03/09) : les poussées de texte s'appliquent strictement dans l'ordre demandé (sinon un texte vide périmé pouvait
        // écraser le vrai contenu arrivé avant lui).
        private readonly SemaphoreSlim _pushGate = new(1, 1);
        // Numéro de la dernière poussée C# → JS : un texte renvoyé par le JS pour une poussée plus ancienne est périmé (ignoré).
        private int _pushVersion;

        public CodeEditorView()
        {
            InitializeComponent();

            Web.Source = new HtmlWebViewSource { Html = EditorHtml };

#if WINDOWS
            // ★ AJOUT (25/09) : canal de messages de WebView2 (chrome.webview.postMessage) — le texte et la sélection arrivent
            // directement, sans relecture par EvaluateJavaScriptAsync. Les « pings » moto:// ci-dessous ne restent qu'en repli :
            // sous Windows, NavigationStarting ne se déclenche que pour une navigation de page, pas pour le chargement d'une image.
            Web.HandlerChanged += (_, _) => AttachWebMessages();
#endif

            // Repli (hors WebView2) : pings JS → C# (moto://changed, moto://sel) interceptés et annulés.
            Web.Navigating += (s, e) =>
            {
                if (e.Url == null) return;

                if (e.Url.StartsWith("moto://changed"))
                {
                    e.Cancel = true;
                    _ = PullContentAsync();
                }
                else if (e.Url.StartsWith("moto://sel"))
                {
                    e.Cancel = true;
                    _ = PullSelectionAsync();
                }
            };

            Web.Navigated += async (s, e) =>
            {
                _loaded = true;
                await Web.EvaluateJavaScriptAsync($"setLang('{_language}')");
                await Web.EvaluateJavaScriptAsync($"setFontSize({_pendingFontSize.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
                await Web.EvaluateJavaScriptAsync($"setMini({(_pendingMinimapVisible ? "true" : "false")})");
                await Web.EvaluateJavaScriptAsync($"setAutoIndent({(_pendingAutoIndent ? "true" : "false")})");
                await PushContentAsync();
            };
        }

        // ------------------------------------------------------------------
        // API publique
        // ------------------------------------------------------------------

        /// <summary>Navigue vers une ligne (Navigation Assistant).</summary>
        public async void GoToLine(int line)
        {
            if (_loaded) await Web.EvaluateJavaScriptAsync($"goLine({line})");
        }

        /// <summary>Active/désactive l'indentation auto (Tab/Enter) — réglage auto_indent.</summary>
        public async void SetAutoIndent(bool enabled)
        {
            _pendingAutoIndent = enabled;
            if (_loaded)
                await Web.EvaluateJavaScriptAsync($"setAutoIndent({(enabled ? "true" : "false")})");
        }

        /// <summary>Affiche/masque la mini-carte (paramètre minimap_show).</summary>
        public async void SetMinimapVisible(bool visible)
        {
            _pendingMinimapVisible = visible;
            if (_loaded)
                await Web.EvaluateJavaScriptAsync($"setMini({(visible ? "true" : "false")})");
        }

        /// <summary>Choisit la coloration d'après l'extension du fichier affiché (appelé à chaque chargement de document).</summary>
        public void SetLanguageFromPath(string? path)
        {
            var language = LanguageOf(path);
            if (language == _language) return;
            _language = language;
            if (_loaded) _ = Web.EvaluateJavaScriptAsync($"setLang('{language}')");
        }

        /// <summary>
        /// GHOST TEXT (Pair Programming) : affiche une suggestion grise ; l'utilisateur accepte avec Tab (géré côté JS).
        /// </summary>
        public async void SetGhost(string suggestion)
        {
            var json = JsonSerializer.Serialize(suggestion ?? "");
            if (json.Contains('\n') || json.Contains('\r')) json = json.Replace("\r", "").Replace("\n", " ");
            if (_loaded) await Web.EvaluateJavaScriptAsync($"setGhost({json})");
        }

        /// <summary>Texte sélectionné (pour /selection du chat) — valeur mise en cache.</summary>
        public string GetSelectedText() => _lastSelection;

        /// <summary>
        /// Sélection (ou curseur, Length = 0) en coordonnées du texte aux « \n », ou null si l'utilisateur n'a pas placé le curseur
        /// depuis que le texte a été posé par programme — même contrat que CodeEditorViewSkia.GetSelectionRange.
        /// </summary>
        public (int Start, int Length)? GetSelectionRange() => _lastRange;

        /// <summary>Langage de coloration d'après l'extension (« plain » si inconnu).</summary>
        internal static string LanguageOf(string? path)
        {
            var name = Path.GetFileName(path ?? string.Empty).ToLowerInvariant();
            if (name is ".gitignore" or ".gitattributes" or ".editorconfig") return "ini";
            return Path.GetExtension(name) switch
            {
                ".cs" or ".csx" => "cs",
                ".java" or ".kt" or ".kts" or ".scala" or ".swift" or ".dart" or ".go" or ".rs"
                    or ".c" or ".h" or ".cpp" or ".hpp" or ".cc" or ".cxx" or ".m" or ".php" or ".gradle" => "clike",
                ".js" or ".mjs" or ".cjs" or ".jsx" or ".ts" or ".tsx" => "js",
                ".py" or ".pyw" => "py",
                ".json" or ".jsonc" or ".json5" => "json",
                ".xml" or ".xaml" or ".csproj" or ".vbproj" or ".fsproj" or ".vcxproj" or ".props" or ".targets" or ".config"
                    or ".resx" or ".html" or ".htm" or ".svg" or ".xsd" or ".nuspec" or ".appxmanifest" or ".manifest" => "xml",
                ".md" or ".markdown" => "md",
                ".ps1" or ".psm1" or ".psd1" => "ps",
                ".sh" or ".bash" or ".zsh" or ".bat" or ".cmd" => "sh",
                ".css" or ".scss" or ".less" => "css",
                ".yml" or ".yaml" => "yaml",
                ".sql" => "sql",
                ".ini" or ".toml" or ".cfg" or ".conf" or ".properties" or ".env" => "ini",
                _ => "plain",
            };
        }

        /// <summary>
        /// ★ AJOUT (01/10, décision C item 1 tranche 2) : nom LISIBLE du langage d'après
        /// l'extension, pour la puce « Langage » de la barre de statut (sb_language).
        /// Plus précis que <see cref="LanguageOf"/> — qui regroupe volontairement en
        /// « clike » pour la COLORATION — : ici chaque extension connue reçoit son vrai
        /// nom (Java, Kotlin, C#, C++…). Retourne "" si l'extension est inconnue : la
        /// puce reste alors masquée (jamais de nom inventé).
        /// </summary>
        internal static string LanguageDisplayName(string? path)
        {
            var name = Path.GetFileName(path ?? string.Empty).ToLowerInvariant();
            if (name is ".gitignore" or ".gitattributes" or ".editorconfig") return "INI";
            return Path.GetExtension(name) switch
            {
                ".cs" => "C#", ".csx" => "C# Script",
                ".java" => "Java", ".kt" or ".kts" => "Kotlin", ".scala" => "Scala",
                ".swift" => "Swift", ".dart" => "Dart", ".go" => "Go", ".rs" => "Rust",
                ".c" => "C", ".h" => "C/C++", ".cpp" or ".hpp" or ".cc" or ".cxx" => "C++",
                ".m" => "Objective-C", ".php" => "PHP", ".gradle" => "Gradle",
                ".js" or ".mjs" or ".cjs" => "JavaScript", ".jsx" => "JSX",
                ".ts" => "TypeScript", ".tsx" => "TSX",
                ".py" or ".pyw" => "Python",
                ".json" or ".jsonc" or ".json5" => "JSON",
                ".xaml" => "XAML", ".xml" => "XML",
                ".csproj" or ".vbproj" or ".fsproj" or ".vcxproj" or ".props" or ".targets" => "MSBuild",
                ".html" or ".htm" => "HTML", ".svg" => "SVG",
                ".md" or ".markdown" => "Markdown",
                ".ps1" or ".psm1" or ".psd1" => "PowerShell",
                ".sh" => "Shell", ".bash" => "Bash", ".zsh" => "Zsh", ".bat" or ".cmd" => "Batch",
                ".css" => "CSS", ".scss" => "SCSS", ".less" => "Less",
                ".yml" or ".yaml" => "YAML",
                ".sql" => "SQL",
                ".ini" or ".toml" or ".cfg" or ".conf" or ".properties" or ".env" => "INI",
                _ => "",
            };
        }

        // ------------------------------------------------------------------
        // Sync C# → JS
        // ------------------------------------------------------------------

        private static void OnTextChanged(BindableObject b, object old, object neu)
        {
            var view = (CodeEditorView)b;
            if (view._suppress) return;
            view._pushVersion++;              // dès maintenant, tout message du JS antérieur à cette poussée est périmé
            view._lastRange = null;           // texte posé par programme : endroit inconnu tant que l'utilisateur n'a pas recliqué
            view._lastSelection = string.Empty;
            _ = view.PushContentAsync();
        }

        private static void OnFontSizeChanged(BindableObject b, object old, object neu)
        {
            var view = (CodeEditorView)b;
            view._pendingFontSize = (double)neu;

            if (view._loaded)
                _ = view.Web.EvaluateJavaScriptAsync($"setFontSize({((double)neu).ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        }

        /// <summary>
        /// ★ CORRECTIF (03/09) : `EvaluateJavaScriptAsync` (WinUI) échoue en silence dès que le script contient un saut de ligne
        /// échappé — le contenu passe donc en Base64 (décodé côté JS par setContentB64).
        /// </summary>
        private async Task PushContentAsync()
        {
            if (!_loaded) return;
            await _pushGate.WaitAsync();
            try
            {
                var text = Text ?? string.Empty;
                _crlf = text.Contains("\r\n", StringComparison.Ordinal);
                var version = _pushVersion;
                var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text));
                await Web.EvaluateJavaScriptAsync($"setContentB64('{base64}',{version})");
            }
            finally
            {
                _pushGate.Release();
            }
        }

        // ------------------------------------------------------------------
        // Sync JS → C#
        // ------------------------------------------------------------------

#if WINDOWS
        private static readonly string[] ContextMenuKept = { "undo", "redo", "cut", "copy", "paste", "selectAll" };
        private object? _hookedPlatformView;

        private void AttachWebMessages()
        {
            if (Web.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.WebView2 wv2 || ReferenceEquals(wv2, _hookedPlatformView)) return;
            _hookedPlatformView = wv2;
            var hooked = false;
            void Hook()
            {
                if (hooked || wv2.CoreWebView2 is null) return;
                hooked = true;
                var core = wv2.CoreWebView2;
                // Un éditeur, pas un navigateur : F5/Ctrl+R (recharger = perdre la page de l'éditeur), Ctrl+P (imprimer),
                // Ctrl+F (barre de recherche du navigateur), zoom Ctrl+molette et bulle d'état désactivés. Les touches d'édition
                // (Ctrl+C/X/V/Z/A) ne sont pas concernées ; les raccourcis de MOTO passent par le pont « K » du JS.
                core.Settings.AreBrowserAcceleratorKeysEnabled = false;
                core.Settings.IsZoomControlEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                // Clic droit : seulement Couper/Copier/Coller/Tout sélectionner/Annuler/Rétablir (le menu par défaut proposait
                // aussi Recharger, Enregistrer sous, Imprimer, Inspecter).
                core.ContextMenuRequested += (_, args) =>
                {
                    var items = args.MenuItems;
                    for (var i = items.Count - 1; i >= 0; i--)
                        if (Array.IndexOf(ContextMenuKept, items[i].Name) < 0) items.RemoveAt(i);
                    while (items.Count > 0 && items[0].Kind == Microsoft.Web.WebView2.Core.CoreWebView2ContextMenuItemKind.Separator) items.RemoveAt(0);
                };
                core.WebMessageReceived += (_, args) =>
                {
                    string? message = null;
                    try { message = args.TryGetWebMessageAsString(); } catch (ArgumentException) { }
                    OnWebMessage(message);
                };
            }
            if (wv2.CoreWebView2 is not null) Hook();
            else wv2.CoreWebView2Initialized += (_, _) => Hook();
        }
#endif

        /// <summary>
        /// Message du JS : « T&lt;version&gt;\n&lt;texte&gt; » (le texte a changé), « S&lt;version&gt;,&lt;début&gt;,&lt;fin&gt;,&lt;placéParL'utilisateur&gt;\n&lt;texte
        /// sélectionné&gt; » ou « K&lt;raccourci&gt;\n » (raccourci de MOTO tapé alors que le curseur est dans le code). Un T ou un S envoyé
        /// avant la dernière poussée de texte par C# est ignoré : ses positions désigneraient un autre texte.
        /// </summary>
        private void OnWebMessage(string? message)
        {
            if (string.IsNullOrEmpty(message)) return;
            var newline = message.IndexOf('\n');
            if (newline < 1) return;
            var head = message.Substring(1, newline - 1);
            var body = message.Substring(newline + 1);
            switch (message[0])
            {
                case 'T':
                    if (int.TryParse(head, out var version) && version == _pushVersion) ApplyTextFromEditor(body);
                    break;
                case 'K':
                    ShortcutPressed?.Invoke(head);
                    break;
                case 'S':
                    var parts = head.Split(',');
                    if (parts.Length != 4 || !int.TryParse(parts[0], out var selVersion) || selVersion != _pushVersion
                        || !int.TryParse(parts[1], out var start) || !int.TryParse(parts[2], out var end)) return;
                    if (parts[3] != "1")
                    {
                        _lastRange = null;
                        _lastSelection = string.Empty;
                        return;
                    }
                    _lastRange = (start, Math.Max(0, end - start));
                    _lastSelection = body;
                    SelectionChanged?.Invoke(_lastRange.Value);
                    break;
            }
        }

        private void ApplyTextFromEditor(string text)
        {
            if (_crlf) text = text.Replace("\n", "\r\n");
            if (text == Text) return;
            _suppress = true;
            Text = text;
            _suppress = false;
            EditorChanged?.Invoke(this, text);
        }

        private async Task PullContentAsync()
        {
            var json = await Web.EvaluateJavaScriptAsync("getContent()");

            if (string.IsNullOrEmpty(json)) return;

            ApplyTextFromEditor(JsonSerializer.Deserialize<string>(json) ?? string.Empty);
        }

        private async Task PullSelectionAsync()
        {
            // « début,fin,placéParL'utilisateur » — des nombres seulement : rien à échapper à travers le pont.
            var raw = (await Web.EvaluateJavaScriptAsync("getSelInfo()"))?.Trim('"', ' ');
            if (string.IsNullOrEmpty(raw)) return;
            var parts = raw.Split(',');
            if (parts.Length != 3 || !int.TryParse(parts[0], out var start) || !int.TryParse(parts[1], out var end)) return;

            if (parts[2] != "1")
            {
                _lastRange = null;
                _lastSelection = string.Empty;
                return;
            }
            var normalized = (Text ?? string.Empty).Replace("\r\n", "\n");
            start = Math.Clamp(start, 0, normalized.Length);
            end = Math.Clamp(end, start, normalized.Length);
            _lastRange = (start, end - start);
            _lastSelection = normalized.Substring(start, end - start);
            SelectionChanged?.Invoke(_lastRange.Value);
        }

        // ------------------------------------------------------------------
        // HTML/JS embarqué
        // ------------------------------------------------------------------

        private const string EditorHtml = """"
<!DOCTYPE html>
<html><head><meta charset='utf-8'/>
<style>
:root{--fs:14px;--lh:21px}
*{box-sizing:border-box}
html,body{margin:0;height:100%;overflow:hidden;background:#1e2025;color:#d4d4d4}
body,#area,#back{font-family:'Cascadia Mono','Cascadia Code',Consolas,'Courier New',monospace;font-size:var(--fs);line-height:var(--lh);font-variant-ligatures:none;tab-size:4}
#wrap{position:absolute;top:0;left:0;bottom:0;right:72px;overflow:hidden}
#gutter{position:absolute;left:0;top:0;bottom:0;width:60px;overflow:hidden;background:#1e2025;z-index:3;cursor:default}
#gut{position:absolute;left:0;right:0;top:0;padding:10px 16px 0 0;text-align:right;white-space:pre;color:#5a5f69;font-size:calc(var(--fs) - 1px);line-height:var(--lh);user-select:none}
#gutA{position:absolute;left:0;right:0;height:var(--lh);padding-right:16px;text-align:right;color:#c6c8cc;background:#1e2025;font-size:calc(var(--fs) - 1px);line-height:var(--lh);display:none;user-select:none}
#cur{position:absolute;left:60px;right:0;height:var(--lh);background:rgba(255,255,255,.045);display:none;pointer-events:none;z-index:0}
#back,#area{position:absolute;left:60px;top:0;margin:0;border:0;padding:10px 24px 120px 12px;white-space:pre}
#back{right:14px;bottom:14px;overflow:hidden;pointer-events:none;z-index:1;color:#d4d4d4}
#area{right:0;bottom:0;z-index:2;background:transparent;color:transparent;caret-color:#aeafad;resize:none;outline:none;overflow:auto}
#area::selection{background:rgba(38,111,178,.5);color:transparent}
#area::-webkit-scrollbar{width:14px;height:14px}
#area::-webkit-scrollbar-thumb{background-color:rgba(121,121,121,.32);border:4px solid transparent;background-clip:padding-box;border-radius:8px}
#area::-webkit-scrollbar-thumb:hover{background-color:rgba(121,121,121,.55)}
#area::-webkit-scrollbar-track,#area::-webkit-scrollbar-corner{background:transparent}
#mini{position:absolute;right:0;top:0;bottom:0;width:72px;background:#1b1d21;border-left:1px solid rgba(255,255,255,.05);cursor:pointer}
#view{position:absolute;right:0;width:72px;background:rgba(255,255,255,.07);pointer-events:none}
#gbar{position:absolute;left:72px;bottom:12px;max-width:60%;background:#25272d;color:#9ca3af;border:1px solid rgba(255,255,255,.08);border-radius:6px;padding:4px 10px;font:12px 'Segoe UI Variable','Segoe UI',sans-serif;display:none;box-shadow:0 4px 16px rgba(0,0,0,.35);white-space:nowrap;overflow:hidden;text-overflow:ellipsis;z-index:4}
.k{color:#569cd6}.k2{color:#c586c0}.s{color:#ce9178}.c{color:#6a9955}.n{color:#b5cea8}.t{color:#4ec9b0}.f{color:#dcdcaa}
.p{color:#9b9b9b}.a{color:#9cdcfe}.tg{color:#569cd6}.h{color:#569cd6}.u{color:#6b7280}.e{color:#d7ba7d}
</style></head><body>
<div id='wrap'><div id='cur'></div><div id='gutter'><div id='gut'></div><div id='gutA'></div></div>
<div id='back'></div><textarea id='area' spellcheck='false' autocomplete='off' autocorrect='off' autocapitalize='off' wrap='off'></textarea></div>
<canvas id='mini'></canvas><div id='view'></div><div id='gbar'></div>
<script>
var area=document.getElementById('area'),back=document.getElementById('back'),gut=document.getElementById('gut'),
gutA=document.getElementById('gutA'),gutter=document.getElementById('gutter'),cur=document.getElementById('cur'),
mini=document.getElementById('mini'),view=document.getElementById('view'),wrap=document.getElementById('wrap'),gbar=document.getElementById('gbar');
var LANG='plain',LH=21,PAD=10,ghostText='',caretByUser=false,lines=[''],htmlC=[''],stC=[0],gutCount=0,pingT=0,selT=0,miniOn=true,miniSc=3,VER=0,pendingT=false,AUTO_INDENT=true;

function esc(s){return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');}
function sp(c,t){return '<span class="'+c+'">'+esc(t)+'</span>';}
function words(s){var o={};s.split(' ').forEach(function(w){if(w)o[w]=1;});return o;}
var KW={
cs:words('abstract as base bool byte char checked class const decimal default delegate double enum event explicit extern false fixed float implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly record ref sbyte sealed short sizeof stackalloc static string struct this true typeof uint ulong unchecked unsafe ushort using var virtual void volatile async get set init value partial where nameof with global dynamic required file scoped nint nuint'),
clike:words('abstract auto bool boolean byte char class const constexpr default delete double enum explicit extends extern false final float fn func impl implements import int interface let long mod mut namespace new nil null override package pub private protected public readonly self short signed sizeof static struct super template this trait true type typedef typename uint union unsigned use var virtual void volatile val fun object'),
js:words('abstract as async boolean class const constructor debugger declare default delete enum export extends false from function get implements import in instanceof interface let module namespace new null number of private protected public readonly set static string super this true type typeof undefined var void any never unknown keyof'),
py:words('and as assert async class def del False from global import in is lambda None nonlocal not or pass True self cls print'),
ps:words('begin class data dynamicparam end enum filter function hidden param process static workflow true false null'),
sh:words('alias declare echo export local readonly set unset source call setlocal endlocal exit'),
sql:words('add all alter and as asc between by case check column constraint create database default delete desc distinct drop else end exists foreign from full group having in index inner insert into is join key left like limit not null on or order outer primary references right select set table then top truncate union unique update values view when where with count sum avg min max')
};
var CTRL=words('if else elif for foreach while do switch case break continue return throw try catch except finally raise await yield goto when loop match until then fi esac done');

function word(t,line,start,end,kw,prev){
if(CTRL[t])return sp('k2',t);
if(kw[t])return sp('k',t);
if(prev==='def'||prev==='fn'||prev==='func'||prev==='function')return sp('f',t);
if(prev==='class'||prev==='struct'||prev==='interface'||prev==='enum'||prev==='record'||prev==='trait')return sp('t',t);
var j=end;while(j<line.length&&line.charCodeAt(j)===32)j++;
var ch=line.charAt(j);
if(ch==='('||(ch==='<'&&/^[A-Z]/.test(t)&&/^<[\w<>, ?]*>\s*\(/.test(line.slice(j))))return sp('f',t);
if(/^[A-Z][a-z0-9]\w*$/.test(t)&&line.charAt(start-1)!=='.')return sp('t',t);
return esc(t);}

var RX_C=/(\/\/.*$)|(\/\*)|(@?\$?"(?:[^"\\]|\\.)*"?|'(?:[^'\\\n]|\\.){0,8}'?|`(?:[^`\\]|\\.)*`?)|(\b0[xXbB][0-9a-fA-F_]+\b|\b\d[\d_]*(?:\.\d[\d_]*)?(?:[eE][+-]?\d+)?[fFdDmMuUlL]*\b)|(^\s*#\s*[a-z]+.*$)|([A-Za-z_][\w]*)/g;
function tokC(line,st,kw){
var out='',i=0;
if(st==='bc'){var e=line.indexOf('*/');if(e<0)return{h:sp('c',line),s:'bc'};out=sp('c',line.slice(0,e+2));i=e+2;}
RX_C.lastIndex=i;var m,last=i,prev='';
while((m=RX_C.exec(line))){
if(m.index>last)out+=esc(line.slice(last,m.index));
var t=m[0];
if(m[1])out+=sp('c',t);
else if(m[2]){var e2=line.indexOf('*/',m.index+2);if(e2<0)return{h:out+sp('c',line.slice(m.index)),s:'bc'};out+=sp('c',line.slice(m.index,e2+2));RX_C.lastIndex=e2+2;}
else if(m[3])out+=sp('s',t);
else if(m[4])out+=sp('n',t);
else if(m[5])out+=sp('p',t);
else if(m[6]){out+=word(t,line,m.index,RX_C.lastIndex,kw,prev);prev=t;last=RX_C.lastIndex;continue;}
prev='';last=RX_C.lastIndex;}
if(last<line.length)out+=esc(line.slice(last));
return{h:out,s:0};}

var RX_PY=/(#.*$)|("""|''')|([rRbBfFuU]{0,2}(?:"(?:[^"\\]|\\.)*"?|'(?:[^'\\]|\\.)*'?))|(\b0[xXoObB][0-9a-fA-F_]+\b|\b\d[\d_]*(?:\.\d[\d_]*)?(?:[eE][+-]?\d+)?j?\b)|(^\s*@[\w.]+)|([A-Za-z_]\w*)/g;
function tokPy(line,st){
var out='',i=0;
if(st==='"'||st==="'"){var q=st+st+st,e=line.indexOf(q);if(e<0)return{h:sp('s',line),s:st};out=sp('s',line.slice(0,e+3));i=e+3;}
RX_PY.lastIndex=i;var m,last=i,prev='';
while((m=RX_PY.exec(line))){
if(m.index>last)out+=esc(line.slice(last,m.index));
var t=m[0];
if(m[1])out+=sp('c',t);
else if(m[2]){var e2=line.indexOf(t,m.index+3);if(e2<0)return{h:out+sp('s',line.slice(m.index)),s:t.charAt(0)};out+=sp('s',line.slice(m.index,e2+3));RX_PY.lastIndex=e2+3;}
else if(m[3])out+=sp('s',t);
else if(m[4])out+=sp('n',t);
else if(m[5])out+=sp('e',t);
else if(m[6]){out+=word(t,line,m.index,RX_PY.lastIndex,KW.py,prev);prev=t;last=RX_PY.lastIndex;continue;}
prev='';last=RX_PY.lastIndex;}
if(last<line.length)out+=esc(line.slice(last));
return{h:out,s:0};}

var RX_JSON=/("(?:[^"\\]|\\.)*"?)(\s*:)?|(-?\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b)|\b(true|false|null)\b|(\/\/.*$)/g;
function tokJson(line){
var out='',m,last=0;RX_JSON.lastIndex=0;
while((m=RX_JSON.exec(line))){
if(m.index>last)out+=esc(line.slice(last,m.index));
if(m[1])out+=m[2]?sp('a',m[1])+esc(m[2]):sp('s',m[1]);
else if(m[3])out+=sp('n',m[3]);
else if(m[4])out+=sp('k',m[4]);
else out+=sp('c',m[0]);
last=RX_JSON.lastIndex;}
if(last<line.length)out+=esc(line.slice(last));
return{h:out,s:0};}

function tokXml(line,st){
var out='',i=0,n=line.length;
while(i<n){
if(st==='bc'){var e=line.indexOf('-->',i);if(e<0){out+=sp('c',line.slice(i));return{h:out,s:'bc'};}out+=sp('c',line.slice(i,e+3));i=e+3;st=0;continue;}
if(st==='tag'){
var rest=line.slice(i),m;
if((m=/^\s+/.exec(rest))){out+=m[0];i+=m[0].length;continue;}
if((m=/^\/?>/.exec(rest))){out+=sp('tg',m[0]);i+=m[0].length;st=0;continue;}
if((m=/^"[^"]*"?|^'[^']*'?/.exec(rest))){out+=sp('s',m[0]);i+=m[0].length;continue;}
if((m=/^[\w:.\-]+/.exec(rest))){out+=sp('a',m[0]);i+=m[0].length;continue;}
out+=esc(rest.charAt(0));i++;continue;}
var lt=line.indexOf('<',i);
if(lt<0){out+=esc(line.slice(i)).replace(/&amp;[\w#]+;/g,function(x){return '<span class="n">'+x+'</span>';});break;}
if(lt>i)out+=esc(line.slice(i,lt));
i=lt;
if(line.substr(i,4)==='<!--'){st='bc';out+=sp('c','<!--');i+=4;continue;}
var tm=/^<[\/?!]?[\w:.\-]*/.exec(line.slice(i));
out+=sp('tg',tm[0]);i+=tm[0].length;st='tag';}
return{h:out,s:st};}

function tokMd(line,st){
if(/^\s*(```|~~~)/.test(line))return{h:sp('p',line),s:st==='fence'?0:'fence'};
if(st==='fence')return{h:sp('s',line),s:'fence'};
if(/^#{1,6}\s/.test(line))return{h:sp('h',line),s:0};
if(/^\s*>/.test(line))return{h:sp('c',line),s:0};
if(/^\s*(---|\*\*\*|___)\s*$/.test(line))return{h:sp('u',line),s:0};
var out='',i=0,m=/^(\s*)([-*+]|\d+\.)(\s+)/.exec(line);
if(m){out=esc(m[1])+sp('k',m[2])+esc(m[3]);i=m[0].length;}
out+=esc(line.slice(i)).replace(/`([^`]+)`/g,'<span class="s">`$1`</span>').replace(/\*\*([^*]+)\*\*/g,'<span class="e">**$1**</span>')
.replace(/\[([^\]]+)\]\(([^)]+)\)/g,'<span class="a">[$1]</span><span class="u">($2)</span>');
return{h:out,s:0};}

var RX_PS=/(#.*$)|(<#)|("(?:[^"`]|`.)*"?|'[^']*'?)|(\$[\w:]+|\$\{[^}]*\})|(\b[A-Za-z]+-[A-Za-z]\w*\b)|((?:^|\s)-[A-Za-z]\w*)|(\b\d+(?:\.\d+)?\b)|([A-Za-z_]\w*)/g;
function tokPs(line,st){
var out='',i=0;
if(st==='bc'){var e=line.indexOf('#>');if(e<0)return{h:sp('c',line),s:'bc'};out=sp('c',line.slice(0,e+2));i=e+2;}
RX_PS.lastIndex=i;var m,last=i,prev='';
while((m=RX_PS.exec(line))){
if(m.index>last)out+=esc(line.slice(last,m.index));
var t=m[0];
if(m[1])out+=sp('c',t);
else if(m[2]){var e2=line.indexOf('#>',m.index+2);if(e2<0)return{h:out+sp('c',line.slice(m.index)),s:'bc'};out+=sp('c',line.slice(m.index,e2+2));RX_PS.lastIndex=e2+2;}
else if(m[3])out+=sp('s',t);
else if(m[4])out+=sp('a',t);
else if(m[5])out+=sp('f',t);
else if(m[6])out+=sp('u',t);
else if(m[7])out+=sp('n',t);
else{var lw=t.toLowerCase();out+=CTRL[lw]?sp('k2',t):(KW.ps[lw]?sp('k',t):(prev==='function'?sp('f',t):esc(t)));prev=lw;last=RX_PS.lastIndex;continue;}
prev='';last=RX_PS.lastIndex;}
if(last<line.length)out+=esc(line.slice(last));
return{h:out,s:0};}

var RX_SH=/(#.*$|^\s*(?:rem|REM|::)(?:\s.*)?$)|("(?:[^"\\]|\\.)*"?|'[^']*'?)|(\$\w+|\$\{[^}]*\}|%[\w~:]+%?)|(\b\d+\b)|([A-Za-z_][\w\-]*)/g;
function tokSh(line){
var out='',m,last=0;RX_SH.lastIndex=0;
while((m=RX_SH.exec(line))){
if(m.index>last)out+=esc(line.slice(last,m.index));
var t=m[0];
if(m[1])out+=sp('c',t);else if(m[2])out+=sp('s',t);else if(m[3])out+=sp('a',t);else if(m[4])out+=sp('n',t);
else{var lw=t.toLowerCase();out+=CTRL[lw]?sp('k2',t):(KW.sh[lw]?sp('k',t):esc(t));}
last=RX_SH.lastIndex;}
if(last<line.length)out+=esc(line.slice(last));
return{h:out,s:0};}

var RX_CSS=/(\/\*)|("(?:[^"\\]|\\.)*"?|'(?:[^'\\]|\\.)*'?)|(#[0-9a-fA-F]{3,8}\b)|(-?\b\d+(?:\.\d+)?(?:px|em|rem|%|vh|vw|s|ms|deg|fr)?\b)|(@[\w-]+)|([\w-]+)(\s*:)(?!:)|([.#]?[\w-]+)/g;
function tokCss(line,st){
var out='',i=0;
if(st==='bc'){var e=line.indexOf('*/');if(e<0)return{h:sp('c',line),s:'bc'};out=sp('c',line.slice(0,e+2));i=e+2;}
var sel=/\{\s*$/.test(line)&&line.indexOf(':')<0;
RX_CSS.lastIndex=i;var m,last=i;
while((m=RX_CSS.exec(line))){
if(m.index>last)out+=esc(line.slice(last,m.index));
if(m[1]){var e2=line.indexOf('*/',m.index+2);if(e2<0)return{h:out+sp('c',line.slice(m.index)),s:'bc'};out+=sp('c',line.slice(m.index,e2+2));RX_CSS.lastIndex=e2+2;}
else if(m[2])out+=sp('s',m[0]);else if(m[3])out+=sp('s',m[0]);else if(m[4])out+=sp('n',m[0]);else if(m[5])out+=sp('k',m[0]);
else if(m[6])out+=sp('a',m[6])+esc(m[7]);
else out+=sel?sp('e',m[0]):esc(m[0]);
last=RX_CSS.lastIndex;}
if(last<line.length)out+=esc(line.slice(last));
return{h:out,s:0};}

var RX_YAML=/(#.*$)|(^\s*(?:-\s+)?[\w.\-"']+(?=\s*:(?:\s|$)))|("(?:[^"\\]|\\.)*"?|'[^']*'?)|(-?\b\d+(?:\.\d+)?\b)|\b(true|false|null|yes|no|on|off)\b/g;
function tokYaml(line){
var out='',m,last=0;RX_YAML.lastIndex=0;
while((m=RX_YAML.exec(line))){
if(m.index>last)out+=esc(line.slice(last,m.index));
out+=m[1]?sp('c',m[0]):m[2]?sp('a',m[0]):m[3]?sp('s',m[0]):m[4]?sp('n',m[0]):sp('k',m[0]);
last=RX_YAML.lastIndex;}
if(last<line.length)out+=esc(line.slice(last));
return{h:out,s:0};}

var RX_INI=/(^\s*[;#].*$)|(^\s*\[.*\]\s*$)|(^\s*[\w.\-]+(?=\s*[=:]))|("(?:[^"\\]|\\.)*"?|'[^']*'?)|(-?\b\d+(?:\.\d+)?\b)|\b(true|false)\b/g;
function tokIni(line){
var out='',m,last=0;RX_INI.lastIndex=0;
while((m=RX_INI.exec(line))){
if(m.index>last)out+=esc(line.slice(last,m.index));
out+=m[1]?sp('c',m[0]):m[2]?sp('t',m[0]):m[3]?sp('a',m[0]):m[4]?sp('s',m[0]):m[5]?sp('n',m[0]):sp('k',m[0]);
last=RX_INI.lastIndex;}
if(last<line.length)out+=esc(line.slice(last));
return{h:out,s:0};}

var RX_SQL=/(--.*$)|(\/\*)|('(?:[^']|'')*'?)|(\b\d+(?:\.\d+)?\b)|([A-Za-z_]\w*)/g;
function tokSql(line,st){
var out='',i=0;
if(st==='bc'){var e=line.indexOf('*/');if(e<0)return{h:sp('c',line),s:'bc'};out=sp('c',line.slice(0,e+2));i=e+2;}
RX_SQL.lastIndex=i;var m,last=i;
while((m=RX_SQL.exec(line))){
if(m.index>last)out+=esc(line.slice(last,m.index));
if(m[1])out+=sp('c',m[0]);
else if(m[2]){var e2=line.indexOf('*/',m.index+2);if(e2<0)return{h:out+sp('c',line.slice(m.index)),s:'bc'};out+=sp('c',line.slice(m.index,e2+2));RX_SQL.lastIndex=e2+2;}
else if(m[3])out+=sp('s',m[0]);else if(m[4])out+=sp('n',m[0]);
else out+=KW.sql[m[0].toLowerCase()]?sp('k',m[0]):esc(m[0]);
last=RX_SQL.lastIndex;}
if(last<line.length)out+=esc(line.slice(last));
return{h:out,s:0};}

function tok(line,st){
switch(LANG){
case 'cs':return tokC(line,st,KW.cs);
case 'clike':return tokC(line,st,KW.clike);
case 'js':return tokC(line,st,KW.js);
case 'py':return tokPy(line,st);
case 'json':return tokJson(line);
case 'xml':return tokXml(line,st);
case 'md':return tokMd(line,st);
case 'ps':return tokPs(line,st);
case 'sh':return tokSh(line);
case 'css':return tokCss(line,st);
case 'yaml':return tokYaml(line);
case 'ini':return tokIni(line);
case 'sql':return tokSql(line,st);
default:return{h:esc(line),s:0};}}

function renderAll(){
lines=area.value.split('\n');htmlC=new Array(lines.length);stC=new Array(lines.length);
var st=0;for(var i=0;i<lines.length;i++){var r=tok(lines[i],st);htmlC[i]=r.h;stC[i]=r.s;st=r.s;}
paint();}
// Recalcule seulement les lignes modifiées, puis s'arrête dès que l'état entrant redevient celui d'avant (commentaire fermé, etc.).
function renderInc(){
var nl=area.value.split('\n'),ol=lines,max=Math.min(nl.length,ol.length),a=0,b=0;
while(a<max&&nl[a]===ol[a])a++;
while(b<max-a&&nl[nl.length-1-b]===ol[ol.length-1-b])b++;
var delta=nl.length-ol.length,nh=htmlC.slice(0,a),ns=stC.slice(0,a),st=a>0?ns[a-1]:0;
for(var i=a;i<nl.length;i++){
if(i>=nl.length-b){var oi=i-delta,oldIn=oi>0?stC[oi-1]:0;
if(oldIn===st){for(var k=i;k<nl.length;k++){nh[k]=htmlC[k-delta];ns[k]=stC[k-delta];}break;}}
var r=tok(nl[i],st);nh[i]=r.h;ns[i]=r.s;st=r.s;}
lines=nl;htmlC=nh;stC=ns;paint();}

function paint(){
back.innerHTML=htmlC.join('\n')+'\n';
if(gutCount!==lines.length){var g='';for(var i=1;i<=lines.length;i++)g+=i+'\n';gut.textContent=g;gutCount=lines.length;}
schedMini();sync();}
// Mini-carte redessinée au plus une fois par image (et plus à chaque touche).
var miniQ=false;function schedMini(){if(miniQ)return;miniQ=true;requestAnimationFrame(function(){miniQ=false;drawMini();});}

function caretLine(){var p=area.selectionStart,v=area.value,c=0;for(var i=v.indexOf('\n');i>=0&&i<p;i=v.indexOf('\n',i+1))c++;return c;}
function updateCur(){
var ln=caretLine(),top=(PAD+ln*LH-area.scrollTop)+'px';
cur.style.top=top;gutA.style.top=top;gutA.textContent=String(ln+1);
cur.style.display=(caretByUser&&area.selectionStart===area.selectionEnd)?'block':'none';
gutA.style.display=caretByUser?'block':'none';}
function sync(){
back.style.right=(area.offsetWidth-area.clientWidth)+'px';back.style.bottom=(area.offsetHeight-area.clientHeight)+'px';
back.scrollTop=area.scrollTop;back.scrollLeft=area.scrollLeft;
gut.style.transform='translateY(-'+area.scrollTop+'px)';
if(miniOn){view.style.top=(area.scrollTop/LH*miniSc)+'px';view.style.height=Math.max(8,area.clientHeight/LH*miniSc)+'px';}
updateCur();}
function drawMini(){
if(!miniOn)return;
mini.width=mini.clientWidth;mini.height=mini.clientHeight;
var ctx=mini.getContext('2d');ctx.clearRect(0,0,mini.width,mini.height);
miniSc=Math.min(3,mini.height/Math.max(lines.length,1));ctx.fillStyle='rgba(200,205,215,.26)';
for(var i=0;i<lines.length;i++){var L=lines[i],t=L.replace(/^\s+/,''),ind=L.length-t.length,w=Math.min(t.length,90);
if(w>0)ctx.fillRect(6+ind*0.55,i*miniSc,w*0.55,Math.max(1,miniSc*0.6));}}

// Canal WebView2 (chrome.webview.postMessage) ; repli : ping moto:// intercepté par Navigating.
function post(m){var w=window.chrome&&window.chrome.webview;if(!w)return false;w.postMessage(m);return true;}
function ping(){pendingT=false;clearTimeout(pingT);if(!post('T'+VER+'\n'+area.value)){var i=new Image();i.src='moto://changed';}}
function pingSel(){var s=area.selectionStart,e=area.selectionEnd;
if(!post('S'+VER+','+s+','+e+','+(caretByUser?1:0)+'\n'+area.value.slice(s,e))){var i=new Image();i.src='moto://sel';}}
// Raccourcis de MOTO tapés dans le code : transmis à C# (le texte en attente part d'abord, pour que Ctrl+S enregistre la
// dernière frappe) ; le navigateur n'en fait rien de son côté.
var MOTO_KEYS={'ctrl+s':1,'ctrl+shift+p':1,'ctrl+shift+i':1,'ctrl+b':1,'f5':1,'f11':1};
document.addEventListener('keydown',function(e){
var c=(e.ctrlKey?'ctrl+':'')+(e.shiftKey?'shift+':'')+(e.altKey?'alt+':'')+String(e.key||'').toLowerCase();
if(!MOTO_KEYS[c])return;
e.preventDefault();e.stopPropagation();if(pendingT)ping();post('K'+c+'\n');},true);
function schedSel(){clearTimeout(selT);selT=setTimeout(function(){updateCur();pingSel();},60);}
function markUser(){caretByUser=true;}
function insert(t){area.focus();var ok=t===''?document.execCommand('delete'):document.execCommand('insertText',false,t);
if(!ok){area.setRangeText(t,area.selectionStart,area.selectionEnd,'end');area.dispatchEvent(new Event('input'));}}
function lineStart(p){return area.value.lastIndexOf('\n',p-1)+1;}
function indentLines(s,e,outdent){
var v=area.value,ls=lineStart(s),le=v.indexOf('\n',Math.max(s,e-1));if(le<0)le=v.length;
var block=v.slice(ls,le),nb=block.split('\n').map(function(l){return outdent?l.replace(/^( {1,4}|\t)/,''):(l.length?'    '+l:l);}).join('\n');
if(nb===block)return;area.setSelectionRange(ls,le);insert(nb);area.setSelectionRange(ls,ls+nb.length);}
function outdentLine(s){
var ls=lineStart(s),m=/^( {1,4}|\t)/.exec(area.value.slice(ls));if(!m)return;
area.setSelectionRange(ls,ls+m[0].length);insert('');var p=Math.max(ls,s-m[0].length);area.setSelectionRange(p,p);}

area.addEventListener('keydown',function(e){
markUser();
if(!AUTO_INDENT)return;
if(e.key==='Tab'&&!e.ctrlKey&&!e.altKey&&!e.metaKey){
e.preventDefault();
if(ghostText&&!e.shiftKey){var g=ghostText;setGhost('');insert(g);return;}
var s=area.selectionStart,en=area.selectionEnd;
if(s!==en&&area.value.slice(s,en).indexOf('\n')>=0)indentLines(s,en,e.shiftKey);
else if(e.shiftKey)outdentLine(s);
else insert('    ');
return;}
if(e.key==='Enter'&&!e.ctrlKey&&!e.altKey&&!e.shiftKey&&!e.metaKey&&!e.isComposing){
var p=area.selectionStart,line=area.value.slice(lineStart(p),p),ind=/^[ \t]*/.exec(line)[0],t=line.replace(/\s+$/,'');
if(LANG!=='plain'&&LANG!=='md'&&/[{\[(:]$/.test(t))ind+='    ';
e.preventDefault();insert('\n'+ind);return;}
if(e.key==='Escape'&&ghostText){setGhost('');}});
area.addEventListener('input',function(){renderInc();pendingT=true;clearTimeout(pingT);pingT=setTimeout(ping,lines.length>800?300:90);schedSel();});
area.addEventListener('scroll',sync,{passive:true});
area.addEventListener('mousedown',markUser);
area.addEventListener('focus',function(){markUser();schedSel();});
['mouseup','keyup','select'].forEach(function(n){area.addEventListener(n,schedSel);});
document.addEventListener('selectionchange',function(){if(document.activeElement===area)schedSel();});
gutter.addEventListener('wheel',function(e){area.scrollTop+=e.deltaY;area.scrollLeft+=e.deltaX;e.preventDefault();},{passive:false});
gutter.addEventListener('mousedown',function(e){
var r=gutter.getBoundingClientRect(),ln=Math.floor((e.clientY-r.top+area.scrollTop-PAD)/LH);
ln=Math.max(0,Math.min(ln,lines.length-1));var p=0;for(var i=0;i<ln;i++)p+=lines[i].length+1;
e.preventDefault();markUser();area.focus();area.setSelectionRange(p,p+lines[ln].length);schedSel();});
var drag=false;
mini.addEventListener('mousedown',function(e){drag=true;jump(e);});
window.addEventListener('mousemove',function(e){if(drag)jump(e);});
window.addEventListener('mouseup',function(){drag=false;});
window.addEventListener('resize',function(){drawMini();sync();});
function jump(e){var r=mini.getBoundingClientRect(),y=e.clientY-r.top;area.scrollTop=y/miniSc*LH-area.clientHeight/2;}

function setGhost(t){ghostText=t||'';gbar.textContent=ghostText?('✦  '+ghostText+'   —   Tab pour accepter · Échap pour ignorer'):'';gbar.style.display=ghostText?'block':'none';}
function setContent(t){
var keep=lines.length>1&&t.slice(0,160)===area.value.slice(0,160),top=area.scrollTop,left=area.scrollLeft;
area.value=t;caretByUser=false;renderAll();
if(keep){area.scrollTop=top;area.scrollLeft=left;}else{area.scrollTop=0;area.scrollLeft=0;}
sync();}
function setContentB64(b64,ver){if(ver!==undefined)VER=ver;pendingT=false;clearTimeout(pingT);var bin=atob(b64),bytes=new Uint8Array(bin.length);for(var i=0;i<bin.length;i++)bytes[i]=bin.charCodeAt(i);setContent(new TextDecoder('utf-8').decode(bytes));}
function getContent(){return JSON.stringify(area.value);}
function getSel(){var s=area.selectionStart,e=area.selectionEnd;return JSON.stringify(s===e?'':area.value.slice(s,e));}
function getSelInfo(){return area.selectionStart+','+area.selectionEnd+','+(caretByUser?1:0);}
function setLang(l){LANG=l||'plain';renderAll();}
function setFontSize(px){var r=document.documentElement.style;r.setProperty('--fs',px+'px');r.setProperty('--lh',Math.round(px*1.5)+'px');
LH=parseFloat(getComputedStyle(area).lineHeight)||Math.round(px*1.5);paint();}
function goLine(l){var p=0;for(var i=0;i<l-1&&i<lines.length;i++)p+=lines[i].length+1;
area.focus();area.setSelectionRange(p,p);area.scrollTop=Math.max(0,(l-1)*LH-area.clientHeight/2);sync();}
function setMini(on){miniOn=!!on;mini.style.display=on?'block':'none';view.style.display=on?'block':'none';wrap.style.right=on?'72px':'0';drawMini();sync();}
function setAutoIndent(on){AUTO_INDENT=!!on;}
renderAll();
</script></body></html>
"""";
    }
}
