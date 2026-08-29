// Moto.Core/Export/PresentationEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Moto.Core.Export
{
    public enum PresentationKind
    {
        ProjectPresentation,   // Présentation générale du projet
        CommercialEstimate,    // Estimation commerciale / devis
        Pitch,                 // Pitch court (investisseurs, clients)
        Slides,                // Diapositives techniques
        Summary                // Résumé exécutif
    }

    public class PresentationRequest
    {
        public PresentationKind Kind { get; set; } = PresentationKind.ProjectPresentation;
        public string ProjectName { get; set; } = "Projet";
        public string Author { get; set; } = "MOTO Editor";
        public string Context { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
    }

    public class PresentationResult
    {
        public bool Success { get; set; }
        public string HtmlPath { get; set; } = string.Empty;
        public string MarkdownPath { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 3. MOTEUR DE PRÉSENTATIONS PROFESSIONNELLES.
    /// Génère pour chaque kind : un fichier HTML autonome type reveal.js
    /// (aucune dépendance : CSS/JS inline) + un Markdown source.
    /// </summary>
    public class PresentationEngine
    {
        public PresentationResult Generate(PresentationRequest request)
        {
            var result = new PresentationResult();

            try
            {
                var title = $"{request.ProjectName} — {LabelFor(request.Kind)}";
                var slides = BuildSlides(request);

                // Markdown source
                var mdPath = Path.ChangeExtension(request.TargetPath, ".md");
                File.WriteAllText(mdPath, ToMarkdown(title, request.Author, slides));
                result.MarkdownPath = mdPath;

                // HTML autonome type reveal.js (tout inline)
                var htmlPath = Path.ChangeExtension(request.TargetPath, ".html");
                File.WriteAllText(htmlPath, ToRevealHtml(title, request.Author, slides));
                result.HtmlPath = htmlPath;

                result.Success = true;
                result.Message = $"✅ Présentation générée : {slides.Count} slides.";
            }
            catch (Exception ex)
            {
                result.Message = "❌ Échec : " + ex.Message;
            }

            return result;
        }

        private List<(string Title, string Body)> BuildSlides(PresentationRequest r)
        {
            return r.Kind switch
            {
                PresentationKind.ProjectPresentation => ProjectSlides(r),
                PresentationKind.CommercialEstimate => EstimateSlides(r),
                PresentationKind.Pitch => PitchSlides(r),
                PresentationKind.Slides => TechSlides(r),
                PresentationKind.Summary => SummarySlides(r),
                _ => ProjectSlides(r)
            };
        }

        private List<(string, string)> ProjectSlides(PresentationRequest r) => new()
        {
            ($"# {r.ProjectName}", $"**{LabelFor(r.Kind)}**\n\nPar {r.Author}\n\n{r.Context}"),
            ("🎯 Vision", "Notre mission : apporter une solution claire, rapide et fiable."),
            ("🧩 Fonctionnalités", "• Fonctionnalité 1\n• Fonctionnalité 2\n• Fonctionnalité 3"),
            ("🏗 Architecture", "Architecture modulaire et scalable."),
            ("📅 Roadmap", "Phase 1 → MVP\nPhase 2 → Extension\nPhase 3 → Internationalisation"),
            ("💬 Contact", $"Pour en savoir plus : {r.Author}"),
        };

        private List<(string, string)> EstimateSlides(PresentationRequest r) => new()
        {
            ($"# {r.ProjectName} — Estimation", $"Préparée par {r.Author}"),
            ("📋 Périmètre", $"**Contexte :** {r.Context}\n\nLots fonctionnels et techniques inclus."),
            ("💶 Budget", "• Conception : 15%\n• Développement : 55%\n• Tests : 20%\n• Déploiement : 10%"),
            ("📆 Planning", "Durée estimée : 3 à 6 mois selon périmètre."),
            ("🛡 Garanties", "Maintenance incluse 6 mois, support réactif."),
            ("✅ Prochaines étapes", "Validation → Signature → Kick-off."),
        };

        private List<(string, string)> PitchSlides(PresentationRequest r) => new()
        {
            ($"# {r.ProjectName}", $"**Pitch** — {r.Author}"),
            ("😣 Le problème", "Le marché souffre de solutions trop lentes et trop chères."),
            ("💡 Notre solution", $"{r.ProjectName} apporte rapidité, simplicité et coût maîtrisé."),
            ("🎯 Marché cible", "Utilisateurs cibles et volume du marché."),
            ("💼 Modèle économique", "Revenus récurrents + services premium."),
            ("👥 Équipe", "Experts motivés, complémentaires."),
            ("📢 Ask", "Rejoignez-nous pour transformer le marché !"),
        };

        private List<(string, string)> TechSlides(PresentationRequest r) => new()
        {
            ($"# {r.ProjectName}", "Présentation technique"),
            ("Stack", "• Backend : C# / .NET\n• Frontend : MAUI\n• IA : MOTO AI + XENO-SSS∞"),
            ("Architecture", "Micro-services, pipeline multi-agents, lazy loading."),
            ("Performance", "Cache SHA256, compilation incrémentale, sandbox."),
            ("Sécurité", "Verrouillage par mot de passe, tokens chiffrés."),
            ("Démo", "Démonstration en direct."),
        };

        private List<(string, string)> SummarySlides(PresentationRequest r) => new()
        {
            ($"# {r.ProjectName} — Résumé", $"Par {r.Author}"),
            ("🎯 En une phrase", $"{r.ProjectName} résout un problème concret, rapidement."),
            ("📊 Points clés", "• Innovation\n• Fiabilité\n• ROI mesurable"),
            ("🚀 Prochaine étape", "Validation du périmètre et lancement."),
        };

        private static string LabelFor(PresentationKind kind) => kind switch
        {
            PresentationKind.ProjectPresentation => "Présentation projet",
            PresentationKind.CommercialEstimate => "Estimation commerciale",
            PresentationKind.Pitch => "Pitch",
            PresentationKind.Slides => "Slides techniques",
            PresentationKind.Summary => "Résumé exécutif",
            _ => "Présentation"
        };

        private string ToMarkdown(string title, string author, List<(string Title, string Body)> slides)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {title}\n");
            sb.AppendLine($"*Par {author} — généré par MOTO Editor*\n");

            foreach (var (t, body) in slides)
            {
                sb.AppendLine($"---\n\n## {t}\n\n{body}\n");
            }

            return sb.ToString();
        }

        /// <summary>
        /// HTML autonome type reveal.js : CSS + navigation clavier + transitions,
        /// tout inline (aucune dépendance CDN).
        /// </summary>
        private string ToRevealHtml(string title, string author, List<(string Title, string Body)> slides)
        {
            var sb = new StringBuilder();
            sb.Append("<div class='slides'>");

            for (int i = 0; i < slides.Count; i++)
            {
                var (t, body) = slides[i];
                var htmlBody = body
                    .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                    .Replace("\n", "<br>")
                    .Replace("**", "<strong>").Replace("**", "</strong>");

                sb.Append($"<section class='slide' data-idx='{i}'>");
                sb.Append($"<h2>{t.Replace("<", "&lt;").Replace(">", "&gt;")}</h2>");
                sb.Append($"<div class='body'>{htmlBody}</div>");
                sb.Append("</section>");
            }

            sb.Append("</div>");
            var slidesHtml = sb.ToString();

            return $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'><title>{title}</title>
<style>
html,body{{margin:0;padding:0;height:100%;background:#0f1115;color:#e8eaed;
font-family:'Segoe UI',system-ui,sans-serif;overflow:hidden;}}
.slides{{position:relative;width:100vw;height:100vh;}}
.slide{{position:absolute;inset:0;display:none;padding:8vh 10vw;
flex-direction:column;justify-content:center;box-sizing:border-box;}}
.slide.active{{display:flex;}}
.slide h2{{font-size:3.5em;margin:0 0 .5em 0;border-left:6px solid #0078cc;padding-left:.3em;}}
.slide .body{{font-size:1.4em;line-height:1.55;}}
.slide .body strong{{color:#4da3ff;}}
.nav{{position:fixed;bottom:2em;right:2em;display:flex;gap:.5em;z-index:10;}}
.nav button{{background:#202126;color:#fff;border:1px solid #3a3b40;
padding:.6em 1.2em;cursor:pointer;border-radius:6px;font-size:1em;}}
.meta{{position:fixed;bottom:1em;left:1em;font-size:.8em;opacity:.5;}}
.counter{{position:fixed;top:1em;right:1em;font-size:.9em;opacity:.7;}}
</style></head><body>
{slidesHtml}
<div class='counter'><span id='idx'>1</span> / {slides.Count}</div>
<div class='meta'>Par {author} — MOTO Editor</div>
<div class='nav'>
<button onclick='go(-1)'>◀ Précédent</button>
<button onclick='go(1)'>Suivant ▶</button>
</div>
<script>
var cur=0;
function show(i){{var slides=document.querySelectorAll('.slide');
slides.forEach(function(s,j){{s.classList.toggle('active',j===i);}});
document.getElementById('idx').textContent=(i+1);}}
function go(d){{cur=Math.max(0,Math.min({slides.Count - 1},cur+d));show(cur);}}
document.addEventListener('keydown',function(e){{
if(e.key==='ArrowRight'||e.key===' ')go(1);
if(e.key==='ArrowLeft')go(-1);}});
show(0);
</script></body></html>";
        }
    }
}
