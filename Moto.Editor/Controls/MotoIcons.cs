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

        // ★ AJOUT (25/09, passe « moyen → élevé ») : jeu complet pour remplacer les emojis du chrome quotidien (onglets,
        // barre d'outils de l'éditeur, explorateur, barre de statut, chat, boîtes de dialogue). Chaque codepoint a été rendu
        // depuis la vraie police (C:\Windows\Fonts\SegoeIcons.ttf) et regardé avant d'être retenu. Écrits en \uXXXX pour
        // rester lisibles dans le code. Piège noté : E8B1 est « lecture aléatoire », pas une branche git (aucun glyphe de
        // branche n'existe dans cette police).
        public const string ChevronRight      = "\uE76C";
        public const string ChevronDown       = "\uE70D";
        public const string ChevronRightSmall = "\uE970";
        public const string ChevronDownSmall  = "\uE96E";
        public const string FolderOpen        = "\uE838";
        public const string NewFolder         = "\uE8F4";
        public const string Document          = "\uE8A5";
        public const string Page              = "\uE7C3";
        public const string Code              = "\uE943";
        public const string Error             = "\uE783";
        public const string ErrorBadge        = "\uEA39";
        public const string Warning           = "\uE7BA";
        public const string Info              = "\uE946";
        public const string Send              = "\uE724";
        public const string Stop              = "\uE71A";
        public const string Attach            = "\uE723";
        public const string Copy              = "\uE8C8";
        public const string Delete            = "\uE74D";
        public const string Terminal          = "\uE756";
        public const string Chat              = "\uE8F2";
        public const string FullScreen        = "\uE740";
        public const string BackToWindow      = "\uE73F";
        public const string Back              = "\uE72B";
        public const string Forward           = "\uE72A";
        public const string Download          = "\uE896";
        public const string Globe             = "\uE774";
        public const string Undo              = "\uE7A7";
        public const string Redo              = "\uE7A6";
        public const string More              = "\uE712";
        public const string Pin               = "\uE718";
        public const string History           = "\uE81C";
        public const string Sync              = "\uE895";
        public const string Bulb              = "\uEA80";
        public const string OpenInNew         = "\uE8A7";
        public const string DockLeft          = "\uE90C";
        public const string DockRight         = "\uE90D";
        public const string OpenPane          = "\uE8A0";
        public const string ClosePane         = "\uE89F";
        public const string Edit              = "\uE70F";
        public const string Accept            = "\uE8FB";
        public const string Cancel            = "\uE711";
        public const string CheckMark         = "\uE73E";
        public const string Help              = "\uE9CE";
        public const string Lightning         = "\uE945";
        public const string Robot             = "\uE99A";
        public const string Package           = "\uF158";
        public const string Layers            = "\uE81E";
        public const string Processing        = "\uE9F5";
        public const string Network           = "\uE968";
        public const string Minimize          = "\uE921";
        public const string Maximize          = "\uE922";
        public const string Restore           = "\uE923";
        public const string Close             = "\uE8BB";
        public const string Key               = ""; // clé (Permissions) — vérifié par rendu le 25/09
        public const string Repair            = ""; // clé à molette — vérifié par rendu le 25/09
        public const string Swap              = "\uE8AB"; // \u21C4 \u00AB Changer de c\u00F4t\u00E9 \u00BB (v\u00E9rifi\u00E9 le 25/09)
    }
}
