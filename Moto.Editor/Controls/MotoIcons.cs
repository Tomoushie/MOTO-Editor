// Moto.Editor/Controls/MotoIcons.cs
// ★ AJOUT (31/08) : glyphes vectoriels (police système "Segoe Fluent Icons",
// native Windows 11) pour remplacer une partie des emojis — demandé par Tom
// via une proposition de Qwen ("rendu jouet"). Chaque codepoint ci-dessous a
// été VÉRIFIÉ visuellement (rendu réel dans un navigateur, police système
// chargée) avant d'être retenu — plusieurs codes proposés par Qwen rendaient
// autre chose que prévu (ex. "AI"=E946 est en réalité une icône "Info", pas
// du tout liée à l'IA) et ont été écartés. Seuls les emojis remplacés ci-
// dessous ont un glyphe confirmé correspondre visuellement à son usage ;
// les autres (🤖 Panneau IA, 🧵 Threads, 🡺 changer de côté) restent en emoji
// faute d'un glyphe vérifié qui leur corresponde vraiment.
using System;

namespace Moto.Editor.Controls
{
    public static class MotoIcons
    {
        public const string FontFamily = "Segoe Fluent Icons";

        public const string Folder    = "";
        public const string Search    = "";
        public const string Settings  = "";
        public const string Refresh   = "";
        public const string SignOut   = "";
        public const string Person    = ""; // "Utilisateur"
        public const string Add       = ""; // "Nouveau fichier"
        public const string Devices   = ""; // "Local"
        public const string Keyboard  = ""; // "Raccourcis"
        public const string Palette   = ""; // "Thèmes"
        public const string Photo     = ""; // "Thèmes d'icônes"
        public const string Puzzle    = ""; // "Extensions"
        public const string Tiles     = ""; // "Disposition des panneaux"
        public const string Comment   = ""; // "Chat"
        public const string People    = ""; // "Organisation" (2 silhouettes, vérifié — plus adapté que Person pour ce sens)
        public const string Mic       = ""; // Microphone (vérifié — glyphe propre, remplace l'emoji 🎤)
    }
}
