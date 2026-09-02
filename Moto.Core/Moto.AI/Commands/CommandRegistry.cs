// Moto.Core/AI/Commands/CommandRegistry.cs
// ★ AJOUT (02/09, chantier "modularité façon Zed/VS Code", fondation choisie par
// Tom en retour de la sonde du même jour — voir CLAUDE.md, section "Modularité
// façon Zed/VS Code", domaine "Commandes et raccourcis").
//
// AVANT ce fichier, MainPage.OnMenuCommanded (Moto.Editor) était un switch(id)
// d'une trentaine de cas codés en dur, chacun appelant directement une méthode
// privée de MainPage — aucune indirection possible : ajouter une commande
// obligeait à éditer ce switch à la main et recompiler. CommandRegistry
// remplace ce switch par une vraie table id → action, remplie une fois au
// démarrage (voir MainPage.Routing.cs, RegisterMenuCommands()).
//
// Ce que CE fichier fait : le registre lui-même, réutilisable indépendamment
// de MainPage. Ce qu'il NE fait PAS (hors scope de cette étape, restant à
// faire si Tom le demande un jour) : brancher un vrai système de plugins
// dessus (IPlugin n'a aujourd'hui aucune méthode pour proposer ses propres
// commandes — voir CLAUDE.md, "Gap B"), ni unifier les 2 autres routeurs
// disjoints (OnGearMenuItemSelected, OnAiCommandSubmitted) qui restent des
// switchs/if-chains séparés pour l'instant.
using System;
using System.Collections.Generic;

namespace Moto.Core.AI.Commands
{
    /// <summary>
    /// Table id → action. Register() une fois par commande connue au démarrage ;
    /// Execute() à chaque invocation (palette, menu, raccourci clavier...).
    /// Aucune hypothèse sur QUI appelle Register() — un plugin pourra un jour y
    /// ajouter les siennes sans jamais modifier le code de l'hôte, comme c'est
    /// déjà le cas pour les réglages (voir Moto.Core.Plugins.IPlugin.Settings).
    /// </summary>
    public sealed class CommandRegistry
    {
        private readonly Dictionary<string, Action> _commands = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Enregistre (ou remplace si le même id existe déjà) l'action
        /// associée à cet identifiant.</summary>
        public void Register(string id, Action action)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("L'identifiant de commande ne peut pas être vide.", nameof(id));
            _commands[id] = action ?? throw new ArgumentNullException(nameof(action));
        }

        /// <summary>Retire une commande du registre (ex. désactivation d'un plugin,
        /// pas utilisé pour l'instant côté menus — prévu pour un futur Gap B).</summary>
        public void Unregister(string id) => _commands.Remove(id);

        public bool IsRegistered(string id) => !string.IsNullOrEmpty(id) && _commands.ContainsKey(id);

        /// <summary>Exécute la commande si elle existe. Retourne false pour un id
        /// inconnu plutôt que de lever — une faute de frappe dans un futur manifeste
        /// de plugin, ou un id de commande retiré entre-temps, ne doit jamais faire
        /// planter l'appelant (même esprit que les switchs qu'il remplace : un id
        /// non reconnu n'y faisait déjà rien).</summary>
        public bool Execute(string id)
        {
            if (!string.IsNullOrEmpty(id) && _commands.TryGetValue(id, out var action))
            {
                action();
                return true;
            }
            return false;
        }
    }
}
