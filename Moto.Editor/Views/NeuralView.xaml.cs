// Moto.Editor/Views/NeuralView.xaml.cs
using System;
using Microsoft.Maui.Controls;
using Moto.Core.AI.Neural;

namespace Moto.Editor.Views
{
    /// <summary>
    /// UI du Neural Mode : mini-modèle IA local spécialisé.
    /// L'utilisateur tape une intention, le modèle génère selon son style.
    /// </summary>
    public partial class NeuralView : ContentView
    {
        private readonly NeuralMode _neural;

        /// <summary>Déclenché quand le modèle génère du code.</summary>
        public event Action<string> CodeGenerated;

        public NeuralView(NeuralMode neural)
        {
            InitializeComponent();
            _neural = neural;
        }

        private async void OnGenerateClicked(object sender, EventArgs e)
        {
            var intent = IntentEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(intent)) return;

            StatusLabel.Text = $"🧬 Génération : {intent}…";
            ResultLabel.Text = "…";

            try
            {
                var code = await System.Threading.Tasks.Task.Run(
                    () => _neural.Generate(intent));

                ResultLabel.Text = code;
                StatusLabel.Text = $"✅ Généré ({code.Split('\n').Length} lignes).";
                CodeGenerated?.Invoke(code);
            }
            catch (Exception ex)
            {
                ResultLabel.Text = "❌ Erreur : " + ex.Message;
                StatusLabel.Text = "Génération échouée.";
            }
        }

        private async void OnRetrainClicked(object sender, EventArgs e)
        {
            StatusLabel.Text = "🔄 Ré-entraînement en cours…";

            try
            {
                await System.Threading.Tasks.Task.Run(() => _neural.Train());
                StatusLabel.Text = "✅ Modèle ré-entraîné.";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "❌ " + ex.Message;
            }
        }
    }
}
