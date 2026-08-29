using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.Collab;

namespace Moto.Editor.Views;

/// <summary>
/// Item 70 — Tableau blanc vectoriel léger (P3). Rendu via IDrawable (MAUI Graphics).
/// </summary>
public partial class WhiteboardView : ContentView
{
    private readonly WhiteboardService _service;
    private readonly Random _rng = new();

    public WhiteboardView(WhiteboardService service)
    {
        InitializeComponent();
        _service = service;

        Board.Drawable = new WhiteboardDrawable(_service);
        _service.WhiteboardChanged += (_, _) => MainThread.BeginInvokeOnMainThread(Board.Invalidate);
    }

    private string RandomColor() =>
        $"#{_rng.Next(120, 255):X2}{_rng.Next(120, 255):X2}{_rng.Next(120, 255):X2}";

    private void OnAddRect(object? sender, EventArgs e)
    {
        _service.AddElement(new WhiteboardElement
        {
            Shape = WhiteboardShape.Rectangle,
            X = _rng.Next(20, 200), Y = _rng.Next(20, 200),
            Width = 120, Height = 70, ColorHex = RandomColor()
        });
    }

    private void OnAddEllipse(object? sender, EventArgs e)
    {
        _service.AddElement(new WhiteboardElement
        {
            Shape = WhiteboardShape.Ellipse,
            X = _rng.Next(20, 200), Y = _rng.Next(20, 200),
            Width = 100, Height = 100, ColorHex = RandomColor()
        });
    }

    private void OnAddArrow(object? sender, EventArgs e)
    {
        double x = _rng.Next(20, 200), y = _rng.Next(20, 200);
        _service.AddElement(new WhiteboardElement
        {
            Shape = WhiteboardShape.Arrow,
            X = x, Y = y, Width = 120, Height = 0, ColorHex = RandomColor()
        });
    }

    private void OnClear(object? sender, EventArgs e) => _service.Clear();
}

/// <summary>Dessine les éléments du WhiteboardService de façon vectorielle.</summary>
public sealed class WhiteboardDrawable : IDrawable
{
    private readonly WhiteboardService _service;
    public WhiteboardDrawable(WhiteboardService service) => _service = service;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        foreach (var el in _service.GetElements())
        {
            canvas.StrokeColor = Color.FromArgb(el.ColorHex);
            canvas.StrokeSize = 2f;

            float x = (float)el.X, y = (float)el.Y;
            float w = (float)el.Width, h = (float)el.Height;

            switch (el.Shape)
            {
                case WhiteboardShape.Rectangle:
                    canvas.DrawRectangle(x, y, w, h);
                    break;

                case WhiteboardShape.Ellipse:
                    canvas.DrawEllipse(x, y, w, h);
                    break;

                case WhiteboardShape.Arrow:
                    canvas.DrawLine(x, y, x + w, y);
                    // pointe de flèche
                    canvas.DrawLine(x + w, y, x + w - 8, y - 5);
                    canvas.DrawLine(x + w, y, x + w - 8, y + 5);
                    break;

                case WhiteboardShape.Text when el.Text is not null:
                    canvas.FontColor = canvas.StrokeColor;
                    canvas.DrawString(x, y, el.Text);
                    break;
            }
        }
    }
}
