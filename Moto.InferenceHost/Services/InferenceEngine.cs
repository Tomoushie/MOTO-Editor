using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Text.Json;

namespace Moto.InferenceHost.Services;

/// <summary>
/// Moteur d'inférence ONNX avec support du batch (speculative decoding).
/// </summary>
public sealed class InferenceEngine : IDisposable
{
    private readonly ILogger<InferenceEngine> _logger;
    private readonly ModelRegistry _registry;

    public InferenceEngine(ILogger<InferenceEngine> logger, ModelRegistry registry)
    {
        _logger = logger;
        _registry = registry;
    }

    /// <summary>
    /// Génère jusqu'à <paramref name="maxTokens"/> tokens.
    /// Compatible avec la vérification batch du SpeculativeDecoder.
    /// </summary>
    public async Task<object> InferAsync(
        string modelId,
        string prompt,
        int maxTokens,
        CancellationToken ct)
    {
        var session = _registry.GetSession(modelId);
        if (session is null)
            return new { error = $"Modèle '{modelId}' non chargé." };

        return await Task.Run(() =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var tokens = new List<int>();

            // Tokenisation simplifiée (à remplacer par le tokenizer réel du modèle)
            var inputIds = Tokenize(prompt);
            var generated = 0;

            while (generated < maxTokens && !ct.IsCancellationRequested)
            {
                var inputTensor = new DenseTensor<int>(
                    new[] { 1, inputIds.Count });

                for (var i = 0; i < inputIds.Count; i++)
                    inputTensor[0, i] = inputIds[i];

                using var outputs = session.Run(new[]
                {
                    NamedOnnxValue.CreateFromTensor("input_ids", inputTensor)
                });

                var logits = outputs.First().AsTensor<float>();
                var nextToken = SampleNextToken(logits);

                tokens.Add(nextToken);
                inputIds.Add(nextToken);
                generated++;
            }

            sw.Stop();
            return new
            {
                tokens_generated = generated,
                elapsed_ms = sw.ElapsedMilliseconds,
                tokens_per_second = generated > 0 ? Math.Round(generated * 1000.0 / sw.ElapsedMilliseconds, 2) : 0,
                output_ids = tokens
            };
        }, ct);
    }

    private static List<int> Tokenize(string text)
    {
        // Placeholder : à remplacer par le tokenizer spécifique au modèle
        return text.Split(' ')
                   .Where(w => !string.IsNullOrWhiteSpace(w))
                   .Select(w => Math.Abs(w.GetHashCode()) % 32000)
                   .ToList();
    }

    private static int SampleNextToken(Tensor<float> logits)
    {
        // Greedy sampling (à remplacer par top-k/top-p si nécessaire)
        var span = logits.GetSpan<float>();
        var maxIdx = 0;
        var maxVal = float.MinValue;
        for (var i = 0; i < span.Length; i++)
        {
            if (span[i] > maxVal)
            {
                maxVal = span[i];
                maxIdx = i;
            }
        }
        return maxIdx;
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
