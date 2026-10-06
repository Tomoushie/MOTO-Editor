using System;
using Moto.Core.Settings;

namespace Moto.Editor.Settings;

/// <summary>
/// ★ AJOUT (06/10, vue fractionnée B1) : lit les réglages « Pane Split Direction »
/// (<c>vertical_split_direction</c>, <c>horizontal_split_direction</c>) — décoratifs
/// jusqu'ici. La vue fractionnée EMPILE les deux conversations (le système de
/// panneaux modulaires empile verticalement), donc seul <c>horizontal_split_direction</c>
/// (Down/Up) est réellement appliqué : il choisit si la 2e conversation est AU-DESSUS
/// ou EN-DESSOUS de la 1re. <c>vertical_split_direction</c> (Right/Left) décrirait un
/// côte-à-côte qui exige un layout Grid 2 colonnes — non implémenté (documenté).
/// </summary>
internal static class SplitSettings
{
    internal static string DeclaredString(string id)
        => SettingsCatalog.ById(id)?.Default as string ?? string.Empty;

    /// <summary><c>horizontal_split_direction</c> = « Up » : la 2e conversation s'affiche au-dessus.</summary>
    internal static bool SecondAbove(SettingsEngine s)
        => string.Equals(s.GetString("horizontal_split_direction", DeclaredString("horizontal_split_direction")), "Up", StringComparison.OrdinalIgnoreCase);
}
