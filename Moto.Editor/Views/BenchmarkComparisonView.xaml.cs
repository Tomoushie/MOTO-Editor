using Moto.Core.AI.Internal;

namespace Moto.Editor.Views;

/// <summary>
/// Vue de comparaison des benchmarks (tous les tiers côte à côte).
/// </summary>
public partial class BenchmarkComparisonView : ContentView
{
    private readonly AiOptimizationsBenchmark _benchmark;

    public BenchmarkComparisonView(AiOptimizationsBenchmark benchmark)
    {
        InitializeComponent();
        _benchmark = benchmark;
    }

    /// <summary>
    /// Affiche les résultats de comparaison.
    /// </summary>
    public void DisplayResults(BenchmarkSuiteResult results)
    {
        ComparisonContainer.Children.Clear();

        // En-tête du tableau
        var header = CreateComparisonHeader();
        ComparisonContainer.Children.Add(header);

        // Lignes par tier
        foreach (var result in results.Results)
        {
            var row = CreateComparisonRow(result);
            ComparisonContainer.Children.Add(row);
        }

        // Graphique de comparaison
        var chart = CreateComparisonChart(results);
        ComparisonContainer.Children.Add(chart);
    }

    private static Grid CreateComparisonHeader()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            BackgroundColor = Color.FromArgb("#2D2D30"),
            Padding = new Thickness(8)
        };

        grid.Children.Add(CreateHeaderLabel("Tier"), 0, 0);
        grid.Children.Add(CreateHeaderLabel("Tokens/s"), 1, 0);
        grid.Children.Add(CreateHeaderLabel("RAM (MB)"), 2, 0);
        grid.Children.Add(CreateHeaderLabel("Latency (ms)"), 3, 0);

        return grid;
    }

    private static Label CreateHeaderLabel(string text) => new()
    {
        Text = text,
        FontAttributes = FontAttributes.Bold,
        TextColor = Colors.White,
        HorizontalOptions = LayoutOptions.Center
    };

    private Grid CreateComparisonRow(BenchmarkResult result)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            BackgroundColor = Color.FromArgb("#1E1E1E"),
            Padding = new Thickness(8)
        };

        grid.Children.Add(new Label { Text = result.Tier, TextColor = Colors.White }, 0, 0);
        grid.Children.Add(new Label { Text = $"{result.TokensPerSecond:F2}", TextColor = Colors.LightGreen }, 1, 0);
        grid.Children.Add(new Label { Text = $"{result.RamUsageMb}", TextColor = Colors.LightBlue }, 2, 0);
        grid.Children.Add(new Label { Text = $"{result.LatencyMs}", TextColor = Colors.Orange }, 3, 0);

        return grid;
    }

    private GraphicsView CreateComparisonChart(BenchmarkSuiteResult results)
    {
        return new GraphicsView
        {
            HeightRequest = 200,
            Drawable = new ComparisonChartDrawable(results.Results)
        };
    }
}

/// <summary>
/// Dessine le graphique de comparaison.
/// </summary>
public class ComparisonChartDrawable : IDrawable
{
    private readonly List<BenchmarkResult> _results;

    public ComparisonChartDrawable(List<BenchmarkResult> results) => _results = results;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Colors.Transparent;
        canvas.FillRectangle(dirtyRect);

        if (_results.Count == 0) return;

        var maxTps = _results.Max(r => r.TokensPerSecond);
        var barWidth = dirtyRect.Width / (_results.Count * 2);
        var colors = new[] { Colors.LightGreen, Colors.LightBlue, Colors.Orange };

        for (var i = 0; i < _results.Count; i++)
        {
            var result = _results[i];
            var barHeight = (float)(result.TokensPerSecond / maxTps) * (dirtyRect.Height - 40);
            var x = i * barWidth * 2 + barWidth / 2;
            var y = dirtyRect.Height - barHeight - 20;

            canvas.FillColor = colors[i % colors.Length];
            canvas.FillRectangle(x, y, barWidth, barHeight);

            canvas.FontSize = 10;
            canvas.DrawString(result.Tier, x, dirtyRect.Height - 5, barWidth, 15,
                HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}
