// Moto.Core/Integration/OrchestratorClient.cs
// Vrai client HTTP vers l'orchestrateur XENO-SSS∞ (MOTO-Xeno-Desktop/orchestrator.py).
//
// ★ AJOUT (08/09) : remplace 4 fichiers morts (XenoBridge.cs, XenoClient.cs,
// XenoTaskService.cs, MotoAi.cs) qui pointaient tous vers un hôte (127.0.0.1:8377)
// et des routes (/xeno/task, /xeno/run, /xeno/ping) qui n'ont jamais existé côté
// orchestrateur — confirmé par grep exhaustif : 0 route de ce nom dans
// orchestrator.py, et aucun des 4 fichiers n'était lui-même instancié nulle part
// (ni DI, ni `new` direct) en dehors de leur propre chaîne. Code mort sur toute
// la ligne.
//
// Contrat vérifié EN DIRECT le 08/09 contre le process réel
// (`start_orchestrator_studio.py --os-v5 --port 5001`, Flask, orchestrator.py) :
// - GET  /health   → statut réel + liste des routes.
// - POST /chat     → un seul appel modèle (aucune écriture disque).
// - POST /generate-batch avec "ecrire": false (défaut) → génère, valide,
//   dépose le résultat en brouillon SOUS BROUILLONS_DIR, n'écrit JAMAIS le
//   vrai fichier. C'est le seul mode compatible avec la règle déjà posée par
//   BeginnerAssistant ("Toujours affiché à l'utilisateur avant application") :
//   ce client ne déclenche donc jamais /generate-and-integrate (qui écrit
//   directement sur disque côté serveur, sans confirmation) tant que personne
//   n'a tranché ce choix explicitement.
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Moto.Core.Integration
{
    /// <summary>Résultat de GET /health.</summary>
    public sealed class OrchestratorHealth
    {
        public bool Reachable { get; set; }
        public bool Ok { get; set; }
        public bool ModeleJoignable { get; set; }
        public string[] Routes { get; set; } = Array.Empty<string>();
        public string? Error { get; set; }
    }

    /// <summary>Résultat de POST /chat.</summary>
    public sealed class OrchestratorChatResult
    {
        public bool Success { get; set; }
        public string Texte { get; set; } = string.Empty;
        public string ModeleReel { get; set; } = string.Empty;
        public double CoutUsd { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>Requête pour une génération SANS écriture (voir GenerateWithoutWritingAsync).</summary>
    public sealed class OrchestratorGenerateRequest
    {
        public string FilePath { get; set; } = string.Empty;
        public string Instruction { get; set; } = string.Empty;
        public string? Tache { get; set; }
        public string? Modele { get; set; }
    }

    /// <summary>
    /// Résultat d'une génération sans écriture : le code proposé (relu depuis le
    /// brouillon déposé par l'orchestrateur) attend une confirmation utilisateur
    /// côté MOTO Editor avant d'être écrit sur le vrai fichier.
    /// </summary>
    public sealed class OrchestratorGenerateResult
    {
        public bool Success { get; set; }
        public bool Valide { get; set; }
        public string Raison { get; set; } = string.Empty;
        public string ModeleUtilise { get; set; } = string.Empty;
        public string? DraftPath { get; set; }
        public string? ProposedContent { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>Contrat du client vers l'orchestrateur XENO-SSS∞.</summary>
    public interface IOrchestratorClient
    {
        Task<OrchestratorHealth> GetHealthAsync(CancellationToken ct = default);
        Task<OrchestratorChatResult> ChatAsync(string modeleOuRole, string prompt, CancellationToken ct = default);
        Task<OrchestratorGenerateResult> GenerateWithoutWritingAsync(OrchestratorGenerateRequest request, CancellationToken ct = default);
    }

    /// <summary>
    /// Client HTTP réel vers l'orchestrateur. MOTO Editor reste un client :
    /// il ne réimplémente ni le routage de modèles, ni la génération, ni la
    /// validation — tout ça vit côté orchestrateur.
    /// </summary>
    public sealed class OrchestratorClient : IOrchestratorClient
    {
        private readonly HttpClient _http;

        /// <summary>Port réel confirmé en direct le 08/09 (pas 5002 : c'est le
        /// serveur WebSocket du sync engine, un processus séparé. Pas 8377 :
        /// n'a jamais existé côté orchestrateur).</summary>
        public string Endpoint { get; set; } = "http://127.0.0.1:5001";

        public OrchestratorClient(HttpClient? httpClient = null)
        {
            _http = httpClient ?? new HttpClient();
        }

        public async Task<OrchestratorHealth> GetHealthAsync(CancellationToken ct = default)
        {
            try
            {
                using var response = await _http.GetAsync($"{Endpoint.TrimEnd('/')}/health", ct).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    return new OrchestratorHealth { Reachable = true, Ok = false, Error = $"HTTP {(int)response.StatusCode}: {body}" };
                }

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                var ok = root.TryGetProperty("status", out var statusEl) && statusEl.GetString() == "ok";
                var joignable = root.TryGetProperty("modele_joignable", out var mj) && mj.ValueKind == JsonValueKind.True;
                var routes = Array.Empty<string>();
                if (root.TryGetProperty("routes", out var routesEl) && routesEl.ValueKind == JsonValueKind.Array)
                {
                    var list = new System.Collections.Generic.List<string>();
                    foreach (var item in routesEl.EnumerateArray())
                    {
                        var s = item.GetString();
                        if (s != null) list.Add(s);
                    }
                    routes = list.ToArray();
                }

                return new OrchestratorHealth { Reachable = true, Ok = ok, ModeleJoignable = joignable, Routes = routes };
            }
            catch (Exception ex)
            {
                return new OrchestratorHealth { Reachable = false, Ok = false, Error = ex.Message };
            }
        }

        public async Task<OrchestratorChatResult> ChatAsync(string modeleOuRole, string prompt, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(modeleOuRole) || string.IsNullOrWhiteSpace(prompt))
            {
                return new OrchestratorChatResult { Success = false, Error = "modeleOuRole et prompt sont requis." };
            }

            try
            {
                var payload = new { modele_ou_role = modeleOuRole, prompt };
                var json = JsonSerializer.Serialize(payload);

                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await _http.PostAsync($"{Endpoint.TrimEnd('/')}/chat", content, ct).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (!response.IsSuccessStatusCode)
                {
                    var err = root.TryGetProperty("error", out var errEl) ? errEl.GetString() : body;
                    return new OrchestratorChatResult { Success = false, Error = err };
                }

                var reussi = root.TryGetProperty("reussi", out var r) && r.ValueKind == JsonValueKind.True;
                var texte = root.TryGetProperty("texte", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                var modeleReel = root.TryGetProperty("modele_reel", out var mr) ? mr.GetString() ?? string.Empty : string.Empty;
                var cout = root.TryGetProperty("cout_usd", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetDouble() : 0.0;

                return new OrchestratorChatResult
                {
                    Success = reussi,
                    Texte = texte,
                    ModeleReel = modeleReel,
                    CoutUsd = cout
                };
            }
            catch (Exception ex)
            {
                return new OrchestratorChatResult { Success = false, Error = ex.Message };
            }
        }

        public async Task<OrchestratorGenerateResult> GenerateWithoutWritingAsync(OrchestratorGenerateRequest request, CancellationToken ct = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.FilePath) || string.IsNullOrWhiteSpace(request.Instruction))
            {
                return new OrchestratorGenerateResult { Success = false, Error = "FilePath et Instruction sont requis." };
            }

            try
            {
                var tache = new
                {
                    filepath = request.FilePath,
                    instruction = request.Instruction,
                    tache = request.Tache,
                    modele = request.Modele
                };
                // ★ "ecrire": false explicite (déjà le défaut côté serveur) : le
                // code généré part en brouillon, jamais sur le vrai fichier.
                var payload = new { taches = new[] { tache }, ecrire = false };
                var json = JsonSerializer.Serialize(payload);

                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await _http.PostAsync($"{Endpoint.TrimEnd('/')}/generate-batch", content, ct).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (!response.IsSuccessStatusCode)
                {
                    var err = root.TryGetProperty("error", out var errEl) ? errEl.GetString() : body;
                    return new OrchestratorGenerateResult { Success = false, Error = err };
                }

                if (!root.TryGetProperty("resultats", out var resultatsEl) ||
                    resultatsEl.ValueKind != JsonValueKind.Array ||
                    resultatsEl.GetArrayLength() == 0)
                {
                    return new OrchestratorGenerateResult { Success = false, Error = "Réponse de l'orchestrateur sans résultat exploitable." };
                }

                var verdict = resultatsEl[0];
                var valide = verdict.TryGetProperty("valide", out var v) && v.ValueKind == JsonValueKind.True;
                var raison = verdict.TryGetProperty("raison", out var ra) ? ra.GetString() ?? string.Empty : string.Empty;
                var modele = verdict.TryGetProperty("modele", out var m) ? m.GetString() ?? string.Empty : string.Empty;
                var draftPath = verdict.TryGetProperty("brouillon", out var b) ? b.GetString() : null;

                string? proposedContent = null;
                if (!string.IsNullOrWhiteSpace(draftPath))
                {
                    try
                    {
                        if (File.Exists(draftPath))
                        {
                            proposedContent = await File.ReadAllTextAsync(draftPath, ct).ConfigureAwait(false);
                        }
                    }
                    catch
                    {
                        // Brouillon illisible (chemin distant, permissions...) : on
                        // renvoie quand même le verdict, sans le contenu proposé.
                    }
                }

                return new OrchestratorGenerateResult
                {
                    Success = true,
                    Valide = valide,
                    Raison = raison,
                    ModeleUtilise = modele,
                    DraftPath = draftPath,
                    ProposedContent = proposedContent
                };
            }
            catch (Exception ex)
            {
                return new OrchestratorGenerateResult { Success = false, Error = ex.Message };
            }
        }
    }
}
