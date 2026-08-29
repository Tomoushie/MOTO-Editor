using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;

namespace Moto.Editor.Controls;

/// <summary>
/// Actions contextuelles (⚡) dans le gutter : refactor, run test, explain, quick fix.
/// S'affiche sur la ligne courante, disparaît au blur.
/// </summary>
public class GutterQuickActions : ContentView
{
    public event Action<QuickActionType, int>? ActionTriggered;

    private readonly VerticalStackLayout _container = new() { Spacing = 2 };
    public int CurrentLine { get; private set; }

    public GutterQuickActions()
    {
        Content = _container;
        IsVisible = false;
    }

    /// <summary>Affiche les actions pertinentes pour la ligne courante.</summary>
    public void ShowForLine(int line, QuickActionContext context)
    {
        CurrentLine = line;
        _container.Children.Clear();

        var actions = BuildActionsFor(context);
        foreach (var action in actions)
        {
            _container.Children.Add(CreateActionButton(action));
        }

        IsVisible = actions.Count > 0;
    }

    public void Hide() => IsVisible = false;

    private static List<(QuickActionType Type, string Icon, string Label)> BuildActionsFor(
        QuickActionContext ctx)
    {
        var list = new List<(QuickActionType, string, string)>();

        if (ctx.IsInsideTestMethod)
            list.Add((QuickActionType.RunTest, "▶", "Run test"));

        if (ctx.HasDiagnosticOnLine)
            list.Add((QuickActionType.QuickFix, "💡", "Quick fix"));

        if (ctx.IsRefactorable)
            list.Add((QuickActionType.Refactor, "🔧", "Refactor"));

        list.Add((QuickActionType.Explain, "🧠", "Explain"));
        return list;
    }

    private View CreateActionButton((QuickActionType Type, string Icon, string Label) action)
    {
        var btn = new Button
        {
            Text = action.Icon,
            FontSize = 11,
            Padding = new Thickness(2),
            WidthRequest = 22,
            HeightRequest = 22,
            BackgroundColor = Colors.Transparent,
            BorderColor = Colors.Transparent,
            Command = new Command(() => ActionTriggered?.Invoke(action.Type, CurrentLine))
        };
        ToolTipProperties.SetText(btn, action.Label);
        return btn;
    }
}

public enum QuickActionType { RunTest, QuickFix, Refactor, Explain, Document }

public class QuickActionContext
{
    public bool IsInsideTestMethod { get; init; }
    public bool HasDiagnosticOnLine { get; init; }
    public bool IsRefactorable { get; init; }
    public string Language { get; init; } = "csharp";
}
