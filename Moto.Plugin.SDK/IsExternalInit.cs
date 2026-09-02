// Moto.Plugin.SDK/IsExternalInit.cs
// ★ AJOUT (02/09, "vrai système de plugins") : netstandard2.0 ne fournit pas le
// type System.Runtime.CompilerServices.IsExternalInit que le compilateur exige
// pour accepter les propriétés `init` (ex. PluginSettingDefinition.Key { get;
// init; } dans Contracts/Models.cs). Polyfill standard, une seule déclaration
// vide suffit — le compilateur ne regarde que sa présence, jamais son contenu.
// Sans ce fichier : erreur CS0518 "Predefined type ... is not defined".
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
