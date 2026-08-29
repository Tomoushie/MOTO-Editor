// Moto.Editor/AI/Builders/BuilderModels.cs
using System;
using System.Collections.Generic;

namespace Moto.Editor.AI.Builders
{
    /// <summary>
    /// Type de génération demandée par l'utilisateur.
    /// </summary>
    public enum BuilderKind
    {
        Blueprint,    // Projet complet
        Module,       // Module ECS (Interface + Component + System)
        Behavior,     // Comportement (System + Component)
        FixAll        // Réparation complète du projet
    }

    /// <summary>
    /// Résultat de la génération.
    /// </summary>
    public class BuilderResult
    {
        public bool Success { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;

        /// <summary>
        /// Fichiers générés. Toujours affichés avant écriture.
        /// </summary>
        public List<GeneratedFile> Files { get; } = new List<GeneratedFile>();

        /// <summary>
        /// Actions d'intégration à valider (using, DI, appels).
        /// </summary>
        public List<IntegrationAction> Integrations { get; } = new List<IntegrationAction>();

        /// <summary>
        /// Erreurs ou avertissements.
        /// </summary>
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// Fichier généré par le builder.
    /// </summary>
    public class GeneratedFile
    {
        public string RelativePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Action d'intégration à effectuer dans le projet existant.
    /// </summary>
    public class IntegrationAction
    {
        public string TargetFile { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string CodeSnippet { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Description d'un module ECS à générer.
    /// </summary>
    public class ModuleDescriptor
    {
        /// <summary>Nom du module (ex: "Health").</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Description en langage naturel.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Propriétés du composant (ex: "int MaxHp, int CurrentHp").</summary>
        public List<string> ComponentProperties { get; } = new List<string>();

        /// <summary>Méthodes du système (ex: "ApplyDamage", "Regenerate").</summary>
        public List<string> SystemMethods { get; } = new List<string>();

        /// <summary>Dépendances vers d'autres modules.</summary>
        public List<string> Dependencies { get; } = new List<string>();
    }

    /// <summary>
    /// Description d'un comportement à générer.
    /// </summary>
    public class BehaviorDescriptor
    {
        /// <summary>Sujet (ex: "Enemy").</summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>Action (ex: "Follow").</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>Cible (ex: "Player").</summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>Paramètres du comportement.</summary>
        public Dictionary<string, string> Parameters { get; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Description d'un projet complet à générer.
    /// </summary>
    public class BlueprintDescriptor
    {
        /// <summary>Nom du projet.</summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>Type de projet (jeu, app, lib, etc.).</summary>
        public string ProjectType { get; set; } = string.Empty;

        /// <summary>Description en langage naturel.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Modules à générer.</summary>
        public List<ModuleDescriptor> Modules { get; } = new List<ModuleDescriptor>();

        /// <summary>Comportements à générer.</summary>
        public List<BehaviorDescriptor> Behaviors { get; } = new List<BehaviorDescriptor>();
    }
}
