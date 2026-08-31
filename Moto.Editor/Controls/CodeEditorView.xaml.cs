// Moto.Editor/Controls/CodeEditorView.xaml.cs (v4 — WebView + ghost text intégré)
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Moto.Editor.Controls
{
    /// <summary>
    /// Éditeur de code maison (WebView) :
    /// numéros de ligne, coloration syntaxique, ascenseur horizontal,
    /// mini-map interactive, ghost text (Tab pour accepter), pont JS ↔ C#.
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

        private bool _loaded;
        private bool _suppress;
        private double _pendingFontSize = 14.0;
        // ★ AJOUT (31/08) : même piège que _pendingFontSize, jamais corrigé pour la
        // mini-map — SetMinimapVisible ne faisait rien tant que le WebView n'avait pas
        // fini de charger (_loaded), SANS jamais rattraper la valeur ensuite. Comme
        // WireSettings() (MainPage) applique les réglages au tout début du
        // constructeur, bien avant que le WebView ait fini de charger, cette perte se
        // produisait quasi systématiquement — "aucune mini-map ne fonctionne" (Tom).
        private bool _pendingMinimapVisible = true;
        private string _lastSelection = string.Empty;

        public CodeEditorView()
        {
            InitializeComponent();

            Web.Source = new HtmlWebViewSource { Html = EditorHtml };

            // Pings JS → C# (moto://changed, moto://sel) interceptés et annulés.
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
                await PushContentAsync();
                await Web.EvaluateJavaScriptAsync($"setFontSize({_pendingFontSize})");
                await Web.EvaluateJavaScriptAsync($"setMini({(_pendingMinimapVisible ? "true" : "false")})");
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

        /// <summary>Affiche/masque la mini-map (paramètre minimap_show).</summary>
        public async void SetMinimapVisible(bool visible)
        {
            // ★ CORRECTION (31/08) : mémorisée AVANT le if — sinon perdue pour de bon
            // si appelée avant la fin du chargement du WebView (voir Navigated ci-dessus
            // et le commentaire sur _pendingMinimapVisible).
            _pendingMinimapVisible = visible;
            if (_loaded)
                await Web.EvaluateJavaScriptAsync($"setMini({(visible ? "true" : "false")})");
        }

        /// <summary>
        /// GHOST TEXT (Pair Programming) : affiche une suggestion grise ;
        /// l'utilisateur accepte avec Tab (géré côté JS).
        /// </summary>
        public async void SetGhost(string suggestion)
        {
            var json = JsonSerializer.Serialize(suggestion ?? "");
            await Web.EvaluateJavaScriptAsync($"setGhost({json})");
        }

        /// <summary>Texte sélectionné (pour /selection du chat) — valeur mise en cache.</summary>
        public string GetSelectedText() => _lastSelection;

        // ------------------------------------------------------------------
        // Sync C# → JS
        // ------------------------------------------------------------------

        private static void OnTextChanged(BindableObject b, object old, object neu)
        {
            var view = (CodeEditorView)b;
            if (!view._suppress) _ = view.PushContentAsync();
        }

        private static void OnFontSizeChanged(BindableObject b, object old, object neu)
        {
            var view = (CodeEditorView)b;
            view._pendingFontSize = (double)neu;

            if (view._loaded)
                _ = view.Web.EvaluateJavaScriptAsync($"setFontSize({(double)neu})");
        }

        private async Task PushContentAsync()
        {
            if (!_loaded) return;
            var json = JsonSerializer.Serialize(Text ?? string.Empty);
            await Web.EvaluateJavaScriptAsync($"setContent({json})");
        }

        // ------------------------------------------------------------------
        // Sync JS → C#
        // ------------------------------------------------------------------

        private async Task PullContentAsync()
        {
            var json = await Web.EvaluateJavaScriptAsync("getContent()");

            if (string.IsNullOrEmpty(json)) return;

            var text = JsonSerializer.Deserialize<string>(json);

            if (text != Text)
            {
                _suppress = true;
                Text = text;
                _suppress = false;

                EditorChanged?.Invoke(this, text);
            }
        }

        private async Task PullSelectionAsync()
        {
            var json = await Web.EvaluateJavaScriptAsync("getSel()");

            if (!string.IsNullOrEmpty(json))
            {
                _lastSelection = JsonSerializer.Deserialize<string>(json) ?? string.Empty;
            }
        }

        // ------------------------------------------------------------------
        // HTML/JS embarqué : gutter + highlight + minimap + h-scroll + GHOST
        // (aucun guillemet double dans le HTML → verbatim string sûr)
        // ------------------------------------------------------------------

        private const string EditorHtml = @"<!DOCTYPE html>
<html><head><meta charset='utf-8'/>
<style>
html,body{margin:0;height:100%;background:#1e2025;font:14px/1.5 Consolas,'Courier New',monospace;}
#wrap{position:absolute;inset:0 90px 0 0;}
#gutter{position:absolute;left:0;top:0;bottom:0;width:52px;overflow:hidden;background:#1a1c20;color:#6b7280;text-align:right;box-sizing:border-box;padding:8px 8px 8px 0;}
#gutter div{white-space:pre;}
#back,#area{position:absolute;left:52px;top:0;right:0;bottom:0;margin:0;border:0;padding:8px;box-sizing:border-box;white-space:pre;overflow:auto;font:inherit;tab-size:4;}
#back{pointer-events:none;color:#dcdfe4;}
#area{background:transparent;color:transparent;caret-color:#fff;resize:none;outline:none;}
#mini{position:absolute;right:0;top:0;bottom:0;width:90px;background:#17181c;cursor:pointer;}
#view{position:absolute;right:0;width:90px;background:rgba(255,255,255,.14);pointer-events:none;}
#gbar{position:fixed;left:52px;right:90px;bottom:0;background:#202126;color:#8a8f98;font:12px Consolas,monospace;padding:4px 8px;display:none;border-top:1px solid #3A3B40;}
.k{color:#569cd6}.s{color:#ce9178}.c{color:#6a9955}.n{color:#b5cea8}
</style></head><body>
<div id='wrap'><div id='gutter'><div id='gutI'></div></div>
<div id='back'></div><textarea id='area' spellcheck='false'></textarea></div>
<canvas id='mini'></canvas><div id='view'></div>
<script>
var area=document.getElementById('area'),back=document.getElementById('back'),
gutI=document.getElementById('gutI'),mini=document.getElementById('mini'),
view=document.getElementById('view');
var ghostText='';
var gbar=document.createElement('div');gbar.id='gbar';document.body.appendChild(gbar);
function esc(s){return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');}
var RX=/(\/\/.*)|(\x22[^\x22]*\x22)|\b(\d+(?:\.\d+)?)\b|\b(public|private|protected|internal|static|void|string|int|bool|double|float|var|class|interface|namespace|using|return|if|else|for|foreach|while|switch|case|break|continue|new|async|await|true|false|null|this|get|set|readonly)\b/g;
function hl(s){return esc(s).replace(RX,function(m,c,st,n,k){
if(c)return '<span class=c>'+m+'</span>';
if(st)return '<span class=s>'+m+'</span>';
if(n)return '<span class=n>'+m+'</span>';
return '<span class=k>'+m+'</span>';});}
function render(){var L=area.value.split('\n');var h='';
for(var i=0;i<L.length;i++)h+=hl(L[i])+'\n';
back.innerHTML=h;var g='';
for(var i=1;i<=L.length;i++)g+=i+'\n';
gutI.textContent=g;drawMini(L);sync();}
function sync(){back.scrollTop=area.scrollTop;back.scrollLeft=area.scrollLeft;
gutI.style.transform='translateY(-'+area.scrollTop+'px)';
var sh=area.scrollHeight||1;
view.style.top=(area.scrollTop/sh*mini.clientHeight)+'px';
view.style.height=(area.clientHeight/sh*mini.clientHeight)+'px';}
function drawMini(L){mini.width=mini.clientWidth;mini.height=mini.clientHeight;
var ctx=mini.getContext('2d');ctx.clearRect(0,0,mini.width,mini.height);
var sc=mini.height/Math.max(L.length,1);ctx.fillStyle='#7f8690';
for(var i=0;i<L.length;i++){ctx.fillRect(2,i*sc,Math.min(L[i].length,100)*0.8,Math.max(1,sc*0.7));}}
function ping(){var i=new Image();i.src='moto://changed';}
function pingSel(){var i=new Image();i.src='moto://sel';}
function setGhost(t){ghostText=t||'';
gbar.textContent=ghostText?('💡 '+ghostText+'  (Tab pour accepter)'):'';
gbar.style.display=ghostText?'block':'none';}
area.addEventListener('scroll',sync);
area.addEventListener('input',function(){render();ping();});
area.addEventListener('select',pingSel);
area.addEventListener('keyup',pingSel);
area.addEventListener('keydown',function(e){
if(e.key==='Tab'&&ghostText){e.preventDefault();
var p=area.selectionStart;
area.value=area.value.slice(0,p)+ghostText+area.value.slice(area.selectionEnd);
area.selectionStart=area.selectionEnd=p+ghostText.length;
setGhost('');render();ping();}});
var drag=false;
mini.addEventListener('mousedown',function(e){drag=true;jump(e);});
window.addEventListener('mousemove',function(e){if(drag)jump(e);});
window.addEventListener('mouseup',function(){drag=false;});
function jump(e){var r=mini.getBoundingClientRect();
var y=(e.clientY-r.top)/r.height;
area.scrollTop=y*area.scrollHeight-area.clientHeight/2;}
function setContent(t){area.value=t;render();}
function getContent(){return JSON.stringify(area.value);}
function setFontSize(px){area.style.fontSize=px+'px';back.style.fontSize=px+'px';}
function getSel(){var s=area.selectionStart,e=area.selectionEnd;
return JSON.stringify(s===e?'':area.value.slice(s,e));}
function goLine(l){var L=area.value.split('\n');var p=0;
for(var i=0;i<l-1&&i<L.length;i++)p+=L[i].length+1;
area.focus();area.setSelectionRange(p,p);
area.scrollTop=Math.max(0,(l-1)*21-area.clientHeight/2);}
function setMini(on){mini.style.display=on?'block':'none';
view.style.display=on?'block':'none';
document.getElementById('wrap').style.right=on?'90px':'0';}
</script></body></html>";
    }
}
