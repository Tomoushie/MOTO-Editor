// Moto.Core/Collab/PatchEngine.cs
using System;

namespace Moto.Core.Collab
{
    /// <summary>
    /// CRDT-light : patch de texte horodaté.
    /// Chaque utilisateur a un ID unique ; en cas de conflit,
    /// le dernier timestamp gagne (last-write-wins par plage).
    /// Suffisant pour du co-working en petit comité.
    /// </summary>
    public class TextPatch
    {
        public Guid AuthorId { get; set; }
        public long TimestampUtcTicks { get; set; }
        public int Start { get; set; }
        public int DeleteCount { get; set; }
        public string InsertText { get; set; } = string.Empty;
    }

    public class PatchEngine
    {
        /// <summary>Applique un patch à un texte. Retourne le nouveau texte.</summary>
        public string Apply(string text, TextPatch patch)
        {
            if (patch == null) return text;

            int start = Math.Clamp(patch.Start, 0, text?.Length ?? 0);
            int del = Math.Clamp(patch.DeleteCount, 0, (text?.Length ?? 0) - start);
            var before = text?.Substring(0, start) ?? "";
            var after = text?.Substring(start + del) ?? "";

            return before + (patch.InsertText ?? "") + after;
        }

        /// <summary>Crée un patch à partir de deux versions consécutives.</summary>
        public TextPatch Diff(string oldText, string newText, Guid authorId)
        {
            oldText ??= "";
            newText ??= "";

            int i = 0;
            while (i < oldText.Length && i < newText.Length && oldText[i] == newText[i]) i++;

            int jOld = oldText.Length - 1;
            int jNew = newText.Length - 1;

            while (jOld > i && jNew > i && oldText[jOld] == newText[jNew])
            {
                jOld--;
                jNew--;
            }

            return new TextPatch
            {
                AuthorId = authorId,
                TimestampUtcTicks = DateTime.UtcNow.Ticks,
                Start = i,
                DeleteCount = jOld - i + 1,
                InsertText = newText.Substring(i, jNew - i + 1)
            };
        }
    }
}
