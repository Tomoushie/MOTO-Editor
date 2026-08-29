// Moto.Core/Export/ExportEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Moto.Core.Export
{
    /// <summary>Format d'export pris en charge.</summary>
    public enum ExportFormat
    {
        Txt, Markdown, Html, Pdf, Docx, Odt, Rtf, Json, Csv
    }

    public class ExportRequest
    {
        public string SourcePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = "MOTO Editor";
        public ExportFormat Format { get; set; } = ExportFormat.Txt;
    }

    public class ExportResult
    {
        public bool Success { get; set; }
        public string TargetPath { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 1 & 2. Moteur d'export universel.
    /// Utilisé par le bouton ⬇ de l'éditeur et par l'IA (commande /export).
    /// </summary>
    public class ExportEngine
    {
        /// <summary>Liste des extensions supportées (pour l'UI et les prompts IA).</summary>
        public static IReadOnlyDictionary<ExportFormat, string> Extensions { get; } =
            new Dictionary<ExportFormat, string>
            {
                [ExportFormat.Txt] = "txt",
                [ExportFormat.Markdown] = "md",
                [ExportFormat.Html] = "html",
                [ExportFormat.Pdf] = "pdf",
                [ExportFormat.Docx] = "docx",
                [ExportFormat.Odt] = "odt",
                [ExportFormat.Rtf] = "rtf",
                [ExportFormat.Json] = "json",
                [ExportFormat.Csv] = "csv"
            };

        public static ExportFormat? ParseFormat(string text)
        {
            var lower = (text ?? "").ToLowerInvariant();

            foreach (var kv in Extensions)
            {
                if (lower.Contains(kv.Value) || lower.Contains(kv.Key.ToString().ToLowerInvariant()))
                    return kv.Key;
            }

            return null;
        }

        public ExportResult Export(ExportRequest request)
        {
            var result = new ExportResult();

            try
            {
                var content = request.Content;

                if (string.IsNullOrEmpty(content) && File.Exists(request.SourcePath))
                    content = File.ReadAllText(request.SourcePath);

                if (string.IsNullOrEmpty(content))
                {
                    result.Message = "Aucun contenu à exporter.";
                    return result;
                }

                var title = string.IsNullOrWhiteSpace(request.Title)
                    ? Path.GetFileNameWithoutExtension(request.SourcePath)
                    : request.Title;

                var ext = Extensions[request.Format];
                var target = Path.Combine(
                    Path.GetDirectoryName(request.SourcePath) ?? Environment.CurrentDirectory,
                    $"{title}.{ext}");

                switch (request.Format)
                {
                    case ExportFormat.Txt:
                        File.WriteAllText(target, content);
                        break;

                    case ExportFormat.Markdown:
                        File.WriteAllText(target, $"# {title}\n\n{content}");
                        break;

                    case ExportFormat.Html:
                        File.WriteAllText(target, BuildHtml(title, request.Author, content));
                        break;

                    case ExportFormat.Pdf:
                        // PDF imprimable : HTML avec CSS @page (ouvrir et Ctrl+P).
                        File.WriteAllText(target.Replace(".pdf", ".html"),
                            BuildPdfReadyHtml(title, request.Author, content));
                        target = target.Replace(".pdf", ".html");
                        break;

                    case ExportFormat.Docx:
                        WriteDocx(target, title, request.Author, content);
                        break;

                    case ExportFormat.Odt:
                        WriteOdt(target, title, request.Author, content);
                        break;

                    case ExportFormat.Rtf:
                        File.WriteAllText(target, BuildRtf(title, content));
                        break;

                    case ExportFormat.Json:
                        File.WriteAllText(target,
                            System.Text.Json.JsonSerializer.Serialize(new
                            {
                                title, author = request.Author,
                                content, exportedAt = DateTime.UtcNow
                            }));
                        break;

                    case ExportFormat.Csv:
                        // Une ligne par ligne de texte (format tableau simple).
                        File.WriteAllLines(target,
                            new[] { "Ligne;Texte" }
                            .Concat(content.Split('\n')
                                .Select((l, i) => $"{i + 1};{l.Replace(";", ",")}")));
                        break;
                }

                result.Success = true;
                result.TargetPath = target;
                result.Message = $"✅ Exporté en {ext.ToUpperInvariant()} : {target}";
            }
            catch (Exception ex)
            {
                result.Message = "❌ Échec : " + ex.Message;
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Formats
        // ------------------------------------------------------------------

        private string BuildHtml(string title, string author, string content)
        {
            var escaped = Escape(content);
            return $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'><title>{Escape(title)}</title>
<meta name='author' content='{Escape(author)}'>
<style>
body{{font:16px/1.6 Georgia,serif;max-width:760px;margin:2em auto;padding:0 1em;color:#222;}}
h1{{border-bottom:2px solid #333;padding-bottom:.3em;}}
pre{{background:#f4f4f4;padding:12px;overflow:auto;border-radius:6px;}}
code{{background:#f4f4f4;padding:2px 6px;border-radius:3px;}}
</style></head><body>
<h1>{Escape(title)}</h1>
<p><em>Par {Escape(author)} — exporté depuis MOTO Editor</em></p>
{FormatBody(escaped)}
</body></html>";
        }

        private string BuildPdfReadyHtml(string title, string author, string content)
        {
            return $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'><title>{Escape(title)}</title>
<style>
@page{{size:A4;margin:2cm;}}
body{{font:12pt/1.5 Georgia,serif;color:#000;}}
h1{{font-size:18pt;border-bottom:1pt solid #000;}}
pre{{background:#eee;padding:8pt;page-break-inside:avoid;}}
.pagebreak{{page-break-after:always;}}
</style></head><body>
<h1>{Escape(title)}</h1>
<p><em>Auteur : {Escape(author)} — Exporté depuis MOTO Editor le {DateTime.Now:dd/MM/yyyy}</em></p>
{FormatBody(Escape(content))}
<div class='pagebreak'></div>
<p style='text-align:center;font-size:10pt;'>Fin du document</p>
</body></html>";
        }

        private string BuildRtf(string title, string content)
        {
            // RTF minimaliste (ASCII, échappement RTF basique).
            var body = content
                .Replace("\\", "\\\\")
                .Replace("{", "\\{")
                .Replace("}", "\\}")
                .Replace("\n", "\\par\n");

            return $@"{{\rtf1\ansi\deff0
{{\fonttbl{{\f0 Calibri;}}}}
{{\info{{\title {title}}}{{\author MOTO Editor}}}}
\f0\fs24
{{\b {title}\b0}}\par\par
{body}
}}";
        }

        /// <summary>
        /// DOCX minimal : archive ZIP contenant les 4 fichiers XML nécessaires.
        /// S'ouvre dans Word, LibreOffice, Google Docs.
        /// </summary>
        private void WriteDocx(string path, string title, string author, string content)
        {
            using var stream = File.Create(path);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Create);

            AddEntry(zip, "[Content_Types].xml", DocxContentTypes);
            AddEntry(zip, "_rels/.rels", DocxRels);
            AddEntry(zip, "word/document.xml", BuildDocxDocument(title, author, content));
            AddEntry(zip, "word/_rels/document.xml.rels", DocxDocRels);
        }

        private void WriteOdt(string path, string title, string author, string content)
        {
            using var stream = File.Create(path);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Create);

            AddEntry(zip, "mimetype", "application/vnd.oasis.opendocument.text");
            AddEntry(zip, "META-INF/manifest.xml", OdtManifest);
            AddEntry(zip, "meta.xml", $@"<?xml version='1.0'?>
<office:document-meta xmlns:office='urn:oasis:names:tc:opendocument:xmlns:office:1.0'
 xmlns:dc='http://purl.org/dc/elements/1.1/'
 xmlns:meta='urn:oasis:names:tc:opendocument:xmlns:meta:1.0'>
<office:meta><dc:title>{Escape(title)}</dc:title>
<dc:creator>{Escape(author)}</dc:creator></office:meta></office:document-meta>");
            AddEntry(zip, "content.xml", BuildOdtContent(title, content));
        }

        private string BuildDocxDocument(string title, string author, string content)
        {
            var paragraphs = content.Split('\n').Select(line =>
                $@"<w:p><w:r><w:t xml:space='preserve'>{Escape(line)}</w:t></w:r></w:p>");

            return $@"<?xml version='1.0' encoding='UTF-8' standalone='yes'?>
<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'>
<w:body>
<w:p><w:pPr><w:pStyle w:val='Title'/></w:pPr>
<w:r><w:rPr><w:b/></w:rPr><w:t>{Escape(title)}</w:t></w:r></w:p>
<w:p><w:r><w:rPr><w:i/></w:rPr><w:t>Par {Escape(author)}</w:t></w:r></w:p>
{string.Join("\n", paragraphs)}
</w:body></w:document>";
        }

        private string BuildOdtContent(string title, string content)
        {
            var paragraphs = content.Split('\n').Select(line =>
                $"<text:p>{Escape(line)}</text:p>");

            return $@"<?xml version='1.0' encoding='UTF-8'?>
<office:document-content xmlns:office='urn:oasis:names:tc:opendocument:xmlns:office:1.0'
 xmlns:text='urn:oasis:names:tc:opendocument:xmlns:text:1.0'>
<office:body><office:text>
<text:h text:outline-level='1'>{Escape(title)}</text:h>
{string.Join("\n", paragraphs)}
</office:text></office:body></office:document-content>";
        }

        private static void AddEntry(ZipArchive zip, string path, string content)
        {
            var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(content);
        }

        private static string Escape(string text) =>
            (text ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        /// <summary>
        /// Formate le corps HTML : détecte ```code```, `inline`, et lignes.
        /// </summary>
        private static string FormatBody(string escaped)
        {
            var sb = new StringBuilder();

            foreach (var line in escaped.Split('\n'))
            {
                if (line.StartsWith("```"))
                {
                    sb.Append(line.StartsWith("```", StringComparison.Ordinal) ? "<pre>" : "</pre>");
                    continue;
                }

                var withInline = Regex.Replace(line, @"`([^`]+)`", "<code>$1</code>");
                sb.Append("<p>").Append(withInline).Append("</p>\n");
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // Constantes DOCX / ODT
        // ------------------------------------------------------------------

        private const string DocxContentTypes = @"<?xml version='1.0' encoding='UTF-8' standalone='yes'?>
<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'>
<Default Extension='rels' ContentType='application/vnd.openxmlformats-package.relationships+xml'/>
<Default Extension='xml' ContentType='application/xml'/>
<Override PartName='/word/document.xml'
 ContentType='application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml'/>
</Types>";

        private const string DocxRels = @"<?xml version='1.0' encoding='UTF-8' standalone='yes'?>
<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'>
<Relationship Id='rId1'
 Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument'
 Target='word/document.xml'/>
</Relationships>";

        private const string DocxDocRels = @"<?xml version='1.0' encoding='UTF-8' standalone='yes'?>
<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'>
</Relationships>";

        private const string OdtManifest = @"<?xml version='1.0' encoding='UTF-8'?>
<manifest:manifest xmlns:manifest='urn:oasis:names:tc:opendocument:xmlns:manifest:1.0'>
<manifest:file-entry manifest:media-type='application/vnd.oasis.opendocument.text' manifest:full-path='/'/>
<manifest:file-entry manifest:media-type='text/xml' manifest:full-path='content.xml'/>
<manifest:file-entry manifest:media-type='text/xml' manifest:full-path='meta.xml'/>
</manifest:manifest>";
    }
}
