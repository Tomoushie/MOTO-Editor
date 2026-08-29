# MotoPluginTemplate

Plugin pour MOTO Editor.

## Installation

1. Compiler le projet : `dotnet build`
2. Copier `MotoPluginTemplate.dll` et `plugin.json` dans `%AppData%/MotoEditor/plugins/`
3. Redémarrer MOTO Editor

## Utilisation

Commande disponible : `/hello [args]`

## Développement

```bash
dotnet new motoplugin -n MonPlugin --Author "Mon Nom" --Description "Ma description"
