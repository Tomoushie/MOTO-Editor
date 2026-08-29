// Moto.Tools.LanguageGen/LanguagePackCli.cs
// Outil CLI : dotnet run -- generate-all --output ./packs
using System;
using System.CommandLine;
using System.IO;
using System.Text.Json;
using Moto.Core.I18n;

namespace Moto.Tools.LanguageGen
{
    public static class LanguagePackCli
    {
        public static int Main(string[] args)
        {
            var rootCommand = new RootCommand("MOTO Language Pack Generator");

            // Commande : générer tous les packs
            var generateAll = new Command("generate-all", "Génère les 50+ packs de langues");
            var outputOption = new Option<string>("--output", () => "./packs", "Dossier de sortie");
            generateAll.AddOption(outputOption);

            generateAll.SetHandler((output) =>
            {
                Console.WriteLine($"🌐 Génération des packs vers : {output}");
                var generator = new LanguagePackGenerator(new Microsoft.Extensions.Logging.Abstractions.NullLogger<LanguagePackGenerator>());
                generator.ExportAllPacks(output);
                Console.WriteLine($"✅ Terminé. {Directory.GetFiles(output, "*.json").Length} packs générés.");
            }, outputOption);

            // Commande : générer un pack spécifique
            var generateOne = new Command("generate", "Génère un pack spécifique");
            var codeArg = new Argument<string>("code", "Code ISO (ex: es)");
            var nativeArg = new Argument<string>("native", "Nom natif (ex: Español)");
            var flagArg = new Argument<string>("flag", "Drapeau (ex: 🇪🇸)");
            generateOne.AddArgument(codeArg);
            generateOne.AddArgument(nativeArg);
            generateOne.AddArgument(flagArg);

            generateOne.SetHandler((code, native, flag) =>
            {
                var generator = new LanguagePackGenerator(new Microsoft.Extensions.Logging.Abstractions.NullLogger<LanguagePackGenerator>());
                var pack = generator.GeneratePack(code, native, flag);

                Directory.CreateDirectory("./packs");
                var path = $"./packs/{code}.json";
                File.WriteAllText(path, JsonSerializer.Serialize(pack, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"✅ Pack {code} généré : {path}");
            }, codeArg, nativeArg, flagArg);

            // Commande : lister les langues supportées
            var listCmd = new Command("list", "Liste les langues supportées");
            listCmd.SetHandler(() =>
            {
                var generator = new LanguagePackGenerator(new Microsoft.Extensions.Logging.Abstractions.NullLogger<LanguagePackGenerator>());
                var packs = generator.GenerateAllPacks();
                Console.WriteLine($"📋 {packs.Count} langues :");
                foreach (var p in packs)
                    Console.WriteLine($"  {p.Flag} {p.Id,-4} {p.NativeName}");
            });

            rootCommand.AddCommand(generateAll);
            rootCommand.AddCommand(generateOne);
            rootCommand.AddCommand(listCmd);

            return rootCommand.Invoke(args);
        }
    }
}
