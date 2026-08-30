// Moto.Editor/ModuleDiagnostics.cs
// Filet de sécurité (30/08) : un ModuleInitializer s'exécute automatiquement dès que
// l'assembly Moto.Editor.dll est chargée par le runtime — avant même Main(). Utile pour
// vérifier que l'assembly se charge correctement si un problème de démarrage revient un jour.
using System.Runtime.CompilerServices;

namespace Moto.Editor
{
    internal static class ModuleDiagnostics
    {
        [ModuleInitializer]
        public static void Init()
        {
            App.Breadcrumb("ModuleInitializer — Moto.Editor.dll chargée");
        }
    }
}
