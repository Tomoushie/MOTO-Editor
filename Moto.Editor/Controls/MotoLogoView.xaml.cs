using Microsoft.Maui.Controls;

namespace Moto.Editor.Controls;

/// <summary>
/// Badge logo MOTO réutilisable (sidebar, accueil, à-propos, panneaux).
/// Taille contrôlée via la propriété Size.
/// </summary>
public partial class MotoLogoView : ContentView
{
    public static readonly BindableProperty SizeProperty =
        BindableProperty.Create(nameof(Size), typeof(double), typeof(MotoLogoView), 32.0,
            propertyChanged: (b, _, newValue) =>
            {
                if (b is MotoLogoView view && newValue is double d)
                    view.ApplySize(d);
            });

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public MotoLogoView()
    {
        InitializeComponent();
        ApplySize(Size);
    }

    private void ApplySize(double size)
    {
        LogoImage.WidthRequest = size;
        LogoImage.HeightRequest = size;
    }
}
