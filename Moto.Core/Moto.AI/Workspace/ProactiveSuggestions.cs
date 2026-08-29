// ── AJOUT : dépendance
private readonly Moto.Core.Collab.PresenceAwareSuggestionGate _presenceGate;

// ── CONSTRUCTEUR : ajouter le paramètre
public ProactiveSuggestions(/* ...paramètres existants... */,
                            Moto.Core.Collab.PresenceAwareSuggestionGate presenceGate)
{
    // ... affectations existantes ...
    _presenceGate = presenceGate;
}

// ── POINT D'ENTRÉE des suggestions proactives : garde EN TÊTE
public void EvaluateAndSuggest(/* ...paramètres existants... */)
{
    // ★ Hook PresenceAware
    if (!_presenceGate.ShouldRunHeavyAi())
        return; // on ne lance pas le pipeline lourd

    // ... logique existante inchangée ...
}
