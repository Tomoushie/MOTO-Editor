// Moto.Editor/Controls/FileTypeVisual.cs
// ★ AJOUT (25/09, passe « moyen → élevé ») : icône + couleur d'un fichier d'après son extension, partagées par les onglets
// de l'éditeur et l'explorateur (jusqu'ici : un emoji 📄 identique pour tous les fichiers, rien ne distinguait un .cs d'un
// .json d'un coup d'œil — ce que VS Code, Zed et JetBrains font tous). Glyphes Segoe Fluent Icons vérifiés (MotoIcons) ;
// couleurs reprises des teintes « de marque » habituelles de chaque langage, adoucies pour un fond sombre.
using System;
using System.IO;
using Microsoft.Maui.Graphics;

namespace Moto.Editor.Controls
{
    public static class FileTypeVisual
    {
        private static readonly Color Neutral = Color.FromArgb("#9CA3AF");

        /// <summary>Glyphe (police MotoIcons.FontFamily) : code source, données/configuration, ou document.</summary>
        public static string Glyph(string? pathOrName) => Kind(pathOrName) switch
        {
            FileKind.Code => MotoIcons.Code,
            FileKind.Data => MotoIcons.Page,
            _ => MotoIcons.Document,
        };

        /// <summary>Couleur du glyphe d'après l'extension (gris neutre si inconnue).</summary>
        public static Color Tint(string? pathOrName) => Extension(pathOrName) switch
        {
            ".cs" or ".csx" => Color.FromArgb("#A77BDB"),
            ".xaml" or ".xml" or ".csproj" or ".props" or ".targets" or ".config" or ".resx" or ".manifest" or ".appxmanifest" or ".nuspec" => Color.FromArgb("#E8894A"),
            ".json" or ".jsonc" => Color.FromArgb("#D4C14A"),
            ".md" or ".markdown" => Color.FromArgb("#5AA7D6"),
            ".js" or ".mjs" or ".cjs" or ".jsx" => Color.FromArgb("#E3CC4A"),
            ".ts" or ".tsx" => Color.FromArgb("#4A8FDB"),
            ".py" or ".pyw" => Color.FromArgb("#5A8FD0"),
            ".ps1" or ".psm1" or ".psd1" => Color.FromArgb("#5A9BD5"),
            ".sh" or ".bash" or ".bat" or ".cmd" => Color.FromArgb("#8CC96A"),
            ".html" or ".htm" => Color.FromArgb("#E86A45"),
            ".css" or ".scss" or ".less" => Color.FromArgb("#7C6BD0"),
            ".sql" => Color.FromArgb("#E0A040"),
            ".yml" or ".yaml" => Color.FromArgb("#D05A5A"),
            ".c" or ".h" or ".cpp" or ".hpp" or ".cc" => Color.FromArgb("#6A9FD8"),
            ".java" or ".kt" => Color.FromArgb("#D8894A"),
            ".go" => Color.FromArgb("#4FC0D8"),
            ".rs" => Color.FromArgb("#D08A6A"),
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".ico" or ".webp" => Color.FromArgb("#B07CD0"),
            _ => Neutral,
        };

        private enum FileKind { Code, Data, Document }

        private static FileKind Kind(string? pathOrName) => Extension(pathOrName) switch
        {
            ".cs" or ".csx" or ".xaml" or ".js" or ".mjs" or ".cjs" or ".jsx" or ".ts" or ".tsx" or ".py" or ".pyw" or ".ps1" or ".psm1"
                or ".sh" or ".bash" or ".bat" or ".cmd" or ".html" or ".htm" or ".css" or ".scss" or ".less" or ".sql" or ".c" or ".h"
                or ".cpp" or ".hpp" or ".cc" or ".java" or ".kt" or ".go" or ".rs" or ".swift" or ".php" or ".rb" or ".lua" => FileKind.Code,
            ".json" or ".jsonc" or ".xml" or ".csproj" or ".props" or ".targets" or ".config" or ".resx" or ".yml" or ".yaml" or ".ini"
                or ".toml" or ".env" or ".sln" or ".manifest" or ".appxmanifest" or ".nuspec" or ".editorconfig" or ".gitignore" => FileKind.Data,
            _ => FileKind.Document,
        };

        private static string Extension(string? pathOrName)
        {
            var name = Path.GetFileName(pathOrName ?? string.Empty);
            if (name.StartsWith('.') && name.IndexOf('.', 1) < 0) return name.ToLowerInvariant(); // .gitignore, .editorconfig…
            return Path.GetExtension(name).ToLowerInvariant();
        }
    }
}
