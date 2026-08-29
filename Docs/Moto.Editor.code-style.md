Style de code officiel de MOTO Editor — clair, strict, cohérent

# MOTO Editor — Code Style

Ce document définit les règles de style utilisées dans l’ensemble du projet :
- Moto.Editor
- Moto.Core
- Snake2000.Engine
- Shared
- Moto.Installer
- Moto.SignTool

---

# 1. Langage & version
- C# 12
- .NET 8
- MAUI + WinUI 3

---

# 2. Organisation des fichiers
Ordre des blocs dans chaque fichier :

using directives
namespace
class / struct / record
fields
constructors
public methods
internal methods
private methods



---

# 3. Règles de nommage
- Classes : `PascalCase`
- Méthodes : `PascalCase`
- Propriétés : `PascalCase`
- Champs privés : `_camelCase`
- Paramètres : `camelCase`
- Variables locales : `camelCase`
- Interfaces : `IName`
- Enums : `PascalCase`

---

# 4. Règles de structure
### 4.1. Pas de logique dans les constructeurs
Les constructeurs doivent uniquement :
- assigner les champs
- valider les paramètres
- initialiser les structures simples

### 4.2. Méthodes courtes
Objectif :
- < 40 lignes
- < 3 niveaux d’imbrication

### 4.3. Pas de `static` global
Uniquement pour :
- helpers
- crypto
- utilitaires purs

---

# 5. Règles MAUI / WinUI
- Pas de logique dans XAML.cs → utiliser des services
- Pas de code-behind lourd
- Pas de singletons UI
- Toujours passer par DI

---

# 6. Règles IA
- Aucun moteur IA ne doit dépendre de l’UI
- Aucun moteur IA ne doit dépendre de l’installateur
- Aucun moteur IA ne doit dépendre de SignTool
- IA = pure logique

---

# 7. Règles de sécurité
- Jamais de clé privée dans le code
- Jamais de chemin absolu
- Jamais de dépendance externe non contrôlée

---

# 8. Règles de performance
- Pas de LINQ dans les boucles critiques
- Pas de allocations inutiles
- Utiliser `Span<T>` si nécessaire
- Utiliser `MemoryMappedFile` pour les modèles IA

---

# 9. Règles de documentation
- Chaque classe publique doit avoir un résumé XML
- Chaque méthode publique doit avoir un commentaire XML
- Pas de commentaires inutiles

---

# 10. Résumé
- ✔ Style strict
- ✔ Cohérence
- ✔ Lisibilité
- ✔ Maintenabilité
