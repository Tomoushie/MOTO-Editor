// Core/Workspace.cs
using System;

namespace Moto.Editor.Core
{
    /// <summary>
    /// Représente le workspace ouvert dans MOTO Editor.
    /// Le workspace est un dossier racine, pas une solution complexe.
    /// Cela garde l'éditeur simple, portable et léger.
    /// </summary>
    public class Workspace
    {
        /// <summary>
        /// Chemin absolu du dossier ouvert.
        /// </summary>
        public string RootPath { get; private set; } = string.Empty;

        /// <summary>
        /// Événement déclenché lorsqu'un workspace est ouvert.
        /// </summary>
        public event Action<string> Opened;

        /// <summary>
        /// Ouvre un dossier comme workspace.
        /// </summary>
        public void OpenFolder(string path)
        {
            RootPath = path;
            Opened?.Invoke(path);
        }
    }
}
