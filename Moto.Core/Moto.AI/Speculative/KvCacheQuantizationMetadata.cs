using System.IO;
using System.Text.Json;

namespace Moto.Core.AI.Speculative;

/// <summary>
/// BONUS — Métadonnées de quantification du KV-cache.
/// Persiste min/max/scale pour éviter la dérive de précision à la décompression.
/// </summary>
public sealed class KvCacheQuantizationMetadata
{
    public float Min { get; set; }
    public float Max { get; set; }
    public float Scale { get; set; }
    public int LayerCount { get; set; }

    public static KvCacheQuantizationMetadata Compute(float[] values)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (var v in values)
        {
            if (v < min) min = v;
            if (v > max) max = v;
        }
        float range = max - min;
        return new KvCacheQuantizationMetadata
        {
            Min = min,
            Max = max,
            Scale = range == 0f ? 1f : range / 255f,
            LayerCount = values.Length
        };
    }

    public void Save(string path)
        => File.WriteAllText(path, JsonSerializer.Serialize(this));

    public static KvCacheQuantizationMetadata? Load(string path)
        => File.Exists(path) ? JsonSerializer.Deserialize<KvCacheQuantizationMetadata>(File.ReadAllText(path)) : null;
}
