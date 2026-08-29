<!-- Docs/CONTRIBUTING.md -->
# 🤝 Contribuer

## Conventions
- **Moto.Core** = logique pure, aucune référence UI/MAUI.
- **Moto.Editor** = UI uniquement, délègue tout à Moto.Core.
- Un moteur = un fichier = une classe `*Engine` avec événements.
- Commentaires pédagogiques en français, XML `<summary>` sur API publique.
- Jamais de travail synchrone lourd sur le thread UI (`Task.Run` + debounce).

## Ajouter une fonctionnalité
1. Créer le moteur dans `Moto.Core/<Domaine>/`.
2. Exposer événements + méthodes `async`.
3. Ajouter un panneau `*View.xaml` dans `Moto.Editor/Views/`.
4. Câbler dans `MainPage.xaml.cs` (toolbar + overlay).
5. Ajouter les paramètres dans `SettingsCatalog.<Cat>.cs`.
6. Mettre à jour `FEATURES.md`.

## Ajouter un paramètre
```csharp
T("mon_param", "Catégorie", "Section", "Titre", "Description.", true);