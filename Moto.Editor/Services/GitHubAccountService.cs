// Moto.Editor/Services/GitHubAccountService.cs
// ★ AJOUT (01/09, point 10 de Tom) : connexion réelle à un compte GitHub via le
// "device flow" OAuth (pas de secret client nécessaire — flux standard pour les
// apps desktop, documenté par GitHub). Le Client ID n'est PAS une donnée
// sensible : il est fait pour être public/embarqué dans une app desktop (à la
// différence d'un "Client Secret", qu'on ne stocke jamais côté client — ce flux
// n'en a justement pas besoin).
//
// Honnêteté sur la portée : ce fichier construit la CONNEXION réelle (obtenir
// un jeton, retrouver le nom d'utilisateur, le sauvegarder/l'effacer). Publier
// un projet sur GitHub ou en importer un ne sont PAS construits ici — Tom a
// décrit ces usages comme la raison d'être de la connexion, mais ce sont des
// chantiers séparés, plus gros (créer un dépôt, pousser des fichiers, lister
// les dépôts existants...). Cette brique-ci est le prérequis commun aux deux.
//
// APIs utilisées : uniquement HttpClient/FormUrlEncodedContent/JsonDocument
// (BCL .NET standard) — aucune dépendance externe, aucune classe inventée.
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.Settings;

namespace Moto.Editor.Services
{
    public sealed class GitHubDeviceFlowInfo
    {
        public string DeviceCode { get; init; } = "";
        public string UserCode { get; init; } = "";
        public string VerificationUri { get; init; } = "";
        public int Interval { get; init; } = 5;
        public int ExpiresIn { get; init; } = 900;
    }

    public sealed class GitHubAccountService
    {
        // ★ Fourni par Tom (31/08) — App OAuth "MOTO Editor Local" enregistrée sur
        // github.com/settings/applications/3828904. Un Client ID d'app OAuth
        // desktop est public par nature (visible dans le trafic réseau de
        // n'importe quel client), donc sans problème à garder en dur ici.
        private const string ClientId = "Ov23lihSSLRCxh33SbnF";

        private static readonly HttpClient Http = new();

        /// <summary>Vrai si un jeton a déjà été obtenu et sauvegardé.</summary>
        public bool IsConnected => !string.IsNullOrEmpty(SettingsEngine.Shared.GetString("github.token", ""));

        /// <summary>Nom d'utilisateur GitHub connecté (vide si non connecté).</summary>
        public string Username => SettingsEngine.Shared.GetString("github.username", "");

        /// <summary>Démarre le device flow : retourne le code à afficher à l'utilisateur.</summary>
        public async Task<GitHubDeviceFlowInfo> StartDeviceFlowAsync()
        {
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["scope"] = "repo read:user"
            });
            var resp = await Http.PostAsync("https://github.com/login/device/code", form);
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            return new GitHubDeviceFlowInfo
            {
                DeviceCode = root.GetProperty("device_code").GetString() ?? "",
                UserCode = root.GetProperty("user_code").GetString() ?? "",
                VerificationUri = root.GetProperty("verification_uri").GetString() ?? "",
                Interval = root.TryGetProperty("interval", out var i) ? i.GetInt32() : 5,
                ExpiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 900
            };
        }

        /// <summary>
        /// Interroge GitHub jusqu'à obtention du jeton (l'utilisateur doit avoir
        /// validé le code dans son navigateur entre-temps), ou null si expiré/annulé.
        /// </summary>
        public async Task<string?> PollForTokenAsync(GitHubDeviceFlowInfo info, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow.AddSeconds(info.ExpiresIn);
            var interval = Math.Max(1, info.Interval);

            while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), ct);

                var form = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = ClientId,
                    ["device_code"] = info.DeviceCode,
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
                });
                var resp = await Http.PostAsync("https://github.com/login/oauth/access_token", form, ct);
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                var root = doc.RootElement;

                if (root.TryGetProperty("access_token", out var tokenEl))
                    return tokenEl.GetString();

                var error = root.TryGetProperty("error", out var errEl) ? errEl.GetString() : null;
                if (error is "authorization_pending") continue; // pas encore validé, on réessaie
                if (error is "slow_down") { interval += 5; continue; }
                return null; // expired_token, access_denied, ou autre échec définitif
            }
            return null;
        }

        /// <summary>Récupère le nom d'utilisateur associé au jeton.</summary>
        public async Task<string> FetchUsernameAsync(string token)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
            req.Headers.Add("Authorization", $"Bearer {token}");
            req.Headers.Add("User-Agent", "MotoEditor");
            var resp = await Http.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("login").GetString() ?? "";
        }

        /// <summary>Sauvegarde la connexion (jeton + nom d'utilisateur).</summary>
        public void SaveConnection(string token, string username)
        {
            SettingsEngine.Shared.Set("github.token", token);
            SettingsEngine.Shared.Set("github.username", username);
        }

        /// <summary>Déconnecte le compte (efface le jeton et le nom d'utilisateur).</summary>
        public void Disconnect()
        {
            SettingsEngine.Shared.Set("github.token", "");
            SettingsEngine.Shared.Set("github.username", "");
        }
    }
}
