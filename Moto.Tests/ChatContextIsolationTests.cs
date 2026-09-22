using System.Linq;
using Moto.Editor.Services;
using Xunit;

namespace Moto.Tests;

/// <summary>
/// Vérifie l'isolation des pièces jointes PAR CONVERSATION
/// (<c>ChatService._pendingByThread</c> — correctif du 22/09, commit 01b7788).
///
/// Ce que ces tests protègent : avant le correctif, <c>ChatService.Contexts</c>
/// était une collection UNIQUE partagée par toutes les conversations. Attacher
/// un fichier dans une conversation, en changer, puis envoyer faisait voyager
/// la pièce jointe vers la mauvaise. C'était aussi le prérequis bloquant de la
/// vue fractionnée (deux conversations affichées en même temps ne peuvent pas
/// partager un seul sac).
///
/// Aucun appel IA n'est déclenché ici : on ne teste que le rattachement et
/// l'affichage des pièces jointes, jamais le routage vers un modèle.
/// </summary>
public class ChatContextIsolationTests
{
    private static ChatService NewChat() => new(@"C:\__moto_tests__", null, null);

    /// <summary>Non-régression : le cas mono-conversation doit se comporter
    /// exactement comme avant le correctif.</summary>
    [Fact]
    public void MonoConversation_AttacheEtAfficheCommeAvant()
    {
        var chat = NewChat();

        chat.AddFile("seul.txt");

        Assert.Single(chat.Contexts);
        Assert.Equal("seul.txt", chat.Contexts[0].Path);
    }

    /// <summary>Le cœur du correctif : une pièce jointe attachée dans une
    /// conversation ne doit JAMAIS apparaître dans une autre.</summary>
    [Fact]
    public void PieceJointe_NeSuitPasLeChangementDeConversation()
    {
        var chat = NewChat();

        var conversationA = chat.CreateThread();
        chat.AddFile("a.txt");
        Assert.Single(chat.Contexts);

        // Créer une conversation la rend active (convention « le plus récent
        // en tête »). Les pièces jointes de A ne doivent PAS suivre.
        chat.CreateThread();
        Assert.Empty(chat.Contexts);

        chat.AddFile("b.txt");
        Assert.Single(chat.Contexts);
        Assert.Equal("b.txt", chat.Contexts[0].Path);

        // Retour sur A : c'est SA pièce jointe qui doit réapparaître.
        chat.SwitchThread(conversationA);
        Assert.Single(chat.Contexts);
        Assert.Equal("a.txt", chat.Contexts[0].Path);
    }

    /// <summary>Une conversation sans pièce jointe affiche une liste vide —
    /// l'affichage ne doit pas conserver les restes de la précédente.</summary>
    [Fact]
    public void ConversationSansPieceJointe_AfficheUneListeVide()
    {
        var chat = NewChat();
        var conversationA = chat.CreateThread();
        chat.AddFile("a.txt");

        chat.CreateThread();
        chat.SwitchThread(conversationA);
        Assert.Single(chat.Contexts);

        chat.CreateThread();
        Assert.Empty(chat.Contexts);
    }

    /// <summary>Chaque conversation garde les SIENNES, plusieurs fois de suite —
    /// un aller-retour ne doit rien perdre ni rien mélanger.</summary>
    [Fact]
    public void AllerRetourEntreDeuxConversations_NeMelangeRien()
    {
        var chat = NewChat();

        var a = chat.CreateThread();
        chat.AddFile("a1.txt");

        var b = chat.CreateThread();
        chat.AddFile("b1.txt");
        chat.AddFile("b2.txt");
        Assert.Equal(2, chat.Contexts.Count);

        chat.SwitchThread(a);
        Assert.Single(chat.Contexts);
        Assert.Equal("a1.txt", chat.Contexts[0].Path);

        chat.SwitchThread(b);
        Assert.Equal(2, chat.Contexts.Count);
        Assert.Contains(chat.Contexts, c => c.Path == "b1.txt");
        Assert.Contains(chat.Contexts, c => c.Path == "b2.txt");

        // A n'a pas été polluée par les deux attachements de B.
        chat.SwitchThread(a);
        Assert.Single(chat.Contexts);
        Assert.Equal("a1.txt", chat.Contexts[0].Path);
    }
}
