// Moto.Editor/Pages/AiSettingsPage.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.AI;
using Moto.Core.AI.Models;
using Moto.Core.Security;

namespace Moto.Editor.Pages
{
    /// <summary>
    /// Page de paramètres pour configurer les providers IA.
    /// </summary>
    public partial class AiSettingsPage : ContentPage
    {
        private readonly FallbackEngine _fallbackEngine;
        private readonly ObservableCollection<ProviderStatusItem> _providerStatus;

        public AiSettingsPage(FallbackEngine fallbackEngine)
        {
            InitializeComponent();

            _fallbackEngine = fallbackEngine ?? new FallbackEngine();
            _providerStatus = new ObservableCollection<ProviderStatusItem>();

            ProviderStatusList.ItemsSource = _providerStatus;

            LoadExistingConfig();
        }

        /// <summary>
        /// Charge la configuration existante.
        /// </summary>
        private void LoadExistingConfig()
        {
            // Ollama
            var ollamaConfig = _fallbackEngine.ProviderManager.GetConfig(AiProviderType.Ollama);
            if (ollamaConfig != null)
            {
                OllamaUrlEntry.Text = ollamaConfig.EndpointUrl;
                OllamaModelEntry.Text = ollamaConfig.ModelName;
            }

            // OpenAI
            var openAiConfig = _fallbackEngine.ProviderManager.GetConfig(AiProviderType.OpenAI);
            if (openAiConfig != null)
            {
                OpenAiModelEntry.Text = openAiConfig.ModelName;
            }

            // Anthropic
            var anthropicConfig = _fallbackEngine.ProviderManager.GetConfig(AiProviderType.Anthropic);
            if (anthropicConfig != null)
            {
                AnthropicModelEntry.Text = anthropicConfig.ModelName;
            }

            // Mistral
            var mistralConfig = _fallbackEngine.ProviderManager.GetConfig(AiProviderType.Mistral);
            if (mistralConfig != null)
            {
                MistralModelEntry.Text = mistralConfig.ModelName;
            }

            // Indicateur de clés existantes.
            if (_fallbackEngine.HasApiKey(AiProviderType.OpenAI))
                OpenAiKeyEntry.Placeholder = "✅ Clé configurée";

            if (_fallbackEngine.HasApiKey(AiProviderType.Anthropic))
                AnthropicKeyEntry.Placeholder = "✅ Clé configurée";

            if (_fallbackEngine.HasApiKey(AiProviderType.Mistral))
                MistralKeyEntry.Placeholder = "✅ Clé configurée";
        }

        private async void OnCheckProvidersClicked(object sender, EventArgs e)
        {
            CheckProvidersButton.IsEnabled = false;
            CheckProvidersButton.Text = "Vérification...";

            _providerStatus.Clear();

            try
            {
                var results = await _fallbackEngine.ProviderManager.CheckAllProvidersAsync();

                foreach (var kv in results)
                {
                    _providerStatus.Add(new ProviderStatusItem
                    {
                        DisplayName = kv.Key.ToString(),
                        Status = kv.Value ? "✅ Disponible" : "❌ Indisponible",
                        StatusColor = kv.Value ? Colors.LimeGreen : Colors.OrangeRed
                    });
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erreur", ex.Message, "OK");
            }
            finally
            {
                CheckProvidersButton.IsEnabled = true;
                CheckProvidersButton.Text = "Vérifier tous les providers";
            }
        }

        private async void OnTestOllamaClicked(object sender, EventArgs e)
        {
            TestOllamaButton.IsEnabled = false;

            try
            {
                var config = AiProviderConfig.DefaultOllama();
                config.EndpointUrl = OllamaUrlEntry.Text;
                config.ModelName = OllamaModelEntry.Text;

                _fallbackEngine.ProviderManager.ConfigureProvider(config);

                var result = await _fallbackEngine.ProviderManager.CheckAllProvidersAsync();

                if (result.TryGetValue(AiProviderType.Ollama, out var available) && available)
                {
                    await DisplayAlert("Ollama", "✅ Ollama est accessible !", "OK");
                }
                else
                {
                    await DisplayAlert("Ollama", "❌ Ollama n'est pas accessible. Vérifiez qu'il est lancé.", "OK");
                }
            }
            finally
            {
                TestOllamaButton.IsEnabled = true;
            }
        }

        private async void OnTestOpenAiClicked(object sender, EventArgs e)
        {
            var key = OpenAiKeyEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(key))
            {
                await DisplayAlert("OpenAI", "Entrez d'abord une clé API.", "OK");
                return;
            }

            TestOpenAiButton.IsEnabled = false;

            try
            {
                var config = AiProviderConfig.DefaultOpenAI();
                config.ApiKey = key;
                config.ModelName = OpenAiModelEntry.Text;

                _fallbackEngine.ProviderManager.ConfigureProvider(config);

                var result = await _fallbackEngine.ProviderManager.CheckAllProvidersAsync();

                if (result.TryGetValue(AiProviderType.OpenAI, out var available) && available)
                {
                    await DisplayAlert("OpenAI", "✅ Clé API valide !", "OK");
                }
                else
                {
                    await DisplayAlert("OpenAI", "❌ Clé API invalide ou service inaccessible.", "OK");
                }
            }
            finally
            {
                TestOpenAiButton.IsEnabled = true;
            }
        }

        private async void OnTestAnthropicClicked(object sender, EventArgs e)
        {
            var key = AnthropicKeyEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(key))
            {
                await DisplayAlert("Anthropic", "Entrez d'abord une clé API.", "OK");
                return;
            }

            TestAnthropicButton.IsEnabled = false;

            try
            {
                var config = AiProviderConfig.DefaultAnthropic();
                config.ApiKey = key;
                config.ModelName = AnthropicModelEntry.Text;

                _fallbackEngine.ProviderManager.ConfigureProvider(config);

                await DisplayAlert("Anthropic", "✅ Configuration sauvegardée. Testez avec une génération.", "OK");
            }
            finally
            {
                TestAnthropicButton.IsEnabled = true;
            }
        }

        private async void OnTestMistralClicked(object sender, EventArgs e)
        {
            var key = MistralKeyEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(key))
            {
                await DisplayAlert("Mistral", "Entrez d'abord une clé API.", "OK");
                return;
            }

            TestMistralButton.IsEnabled = false;

            try
            {
                var config = AiProviderConfig.DefaultMistral();
                config.ApiKey = key;
                config.ModelName = MistralModelEntry.Text;

                _fallbackEngine.ProviderManager.ConfigureProvider(config);

                await DisplayAlert("Mistral", "✅ Configuration sauvegardée.", "OK");
            }
            finally
            {
                TestMistralButton.IsEnabled = true;
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                // Sauvegarder Ollama.
                var ollamaConfig = AiProviderConfig.DefaultOllama();
                ollamaConfig.EndpointUrl = OllamaUrlEntry.Text;
                ollamaConfig.ModelName = OllamaModelEntry.Text;
                _fallbackEngine.ProviderManager.ConfigureProvider(ollamaConfig);

                // Sauvegarder OpenAI.
                if (!string.IsNullOrWhiteSpace(OpenAiKeyEntry.Text))
                {
                    _fallbackEngine.SaveApiKey(AiProviderType.OpenAI, OpenAiKeyEntry.Text.Trim());

                    var openAiConfig = AiProviderConfig.DefaultOpenAI();
                    openAiConfig.ApiKey = OpenAiKeyEntry.Text.Trim();
                    openAiConfig.ModelName = OpenAiModelEntry.Text;
                    _fallbackEngine.ProviderManager.ConfigureProvider(openAiConfig);
                }

                // Sauvegarder Anthropic.
                if (!string.IsNullOrWhiteSpace(AnthropicKeyEntry.Text))
                {
                    _fallbackEngine.SaveApiKey(AiProviderType.Anthropic, AnthropicKeyEntry.Text.Trim());

                    var anthropicConfig = AiProviderConfig.DefaultAnthropic();
                    anthropicConfig.ApiKey = AnthropicKeyEntry.Text.Trim();
                    anthropicConfig.ModelName = AnthropicModelEntry.Text;
                    _fallbackEngine.ProviderManager.ConfigureProvider(anthropicConfig);
                }

                // Sauvegarder Mistral.
                if (!string.IsNullOrWhiteSpace(MistralKeyEntry.Text))
                {
                    _fallbackEngine.SaveApiKey(AiProviderType.Mistral, MistralKeyEntry.Text.Trim());

                    var mistralConfig = AiProviderConfig.DefaultMistral();
                    mistralConfig.ApiKey = MistralKeyEntry.Text.Trim();
                    mistralConfig.ModelName = MistralModelEntry.Text;
                    _fallbackEngine.ProviderManager.ConfigureProvider(mistralConfig);
                }

                await DisplayAlert("MOTO AI", "✅ Configuration sauvegardée et chiffrée.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erreur", ex.Message, "OK");
            }
        }

        private async void OnClearClicked(object sender, EventArgs e)
        {
            var confirmed = await DisplayAlert(
                "MOTO AI",
                "Effacer toutes les clés API sauvegardées ?",
                "Oui, effacer",
                "Annuler");

            if (!confirmed) return;

            _fallbackEngine.TokenStore.DeleteToken("OpenAI");
            _fallbackEngine.TokenStore.DeleteToken("Anthropic");
            _fallbackEngine.TokenStore.DeleteToken("Mistral");

            OpenAiKeyEntry.Text = string.Empty;
            AnthropicKeyEntry.Text = string.Empty;
            MistralKeyEntry.Text = string.Empty;

            OpenAiKeyEntry.Placeholder = "sk-...";
            AnthropicKeyEntry.Placeholder = "sk-ant-...";
            MistralKeyEntry.Placeholder = "Clé API Mistral";

            await DisplayAlert("MOTO AI", "Toutes les clés ont été effacées.", "OK");
        }

        /// <summary>
        /// Modèle pour l'affichage du statut des providers.
        /// </summary>
        private class ProviderStatusItem
        {
            public string DisplayName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public Color StatusColor { get; set; } = Colors.Gray;
        }
    }
}
