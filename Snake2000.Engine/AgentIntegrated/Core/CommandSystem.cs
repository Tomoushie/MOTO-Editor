// Core/CommandSystem.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Moto.Editor.Core
{
    /// <summary>
    /// Système de commandes de MOTO Editor.
    /// Toutes les actions importantes doivent être exposées comme commandes :
    /// ouvrir un dossier, sauvegarder, lancer XENO, tester Ollama, etc.
    /// </summary>
    public class CommandSystem
    {
        private readonly Dictionary<string, Action> _commands =
            new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Liste des noms de commandes disponibles.
        /// </summary>
        public IReadOnlyCollection<string> Names => _commands.Keys.ToArray();

        /// <summary>
        /// Enregistre ou remplace une commande.
        /// </summary>
        public void Register(string name, Action action)
        {
            if (string.IsNullOrWhiteSpace(name) || action == null)
            {
                return;
            }

            _commands[name] = action;
        }

        /// <summary>
        /// Exécute une commande par son nom.
        /// </summary>
        public bool TryExecute(string name)
        {
            if (!string.IsNullOrWhiteSpace(name) && _commands.TryGetValue(name, out var action))
            {
                action();
                return true;
            }

            return false;
        }
    }
}
