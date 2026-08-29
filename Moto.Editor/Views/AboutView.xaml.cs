using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace Moto.Editor.Views;

public partial class AboutView : ContentView
{
    private bool _pulseActive;

    public AboutView()
    {
        InitializeComponent();
        FillInfo();
        Loaded += OnLoaded;
        Unloaded += (_, _) => _pulseActive = false;
    }

    private void FillInfo()
    {
        try
        {
            VersionLabel.Text = $"Version {AppInfo.VersionString} (build {AppInfo.BuildString})";
            OsLabel.Text = $"{DeviceInfo.Platform} {DeviceInfo.VersionString}";
            ArchLabel.Text = RuntimeInformation.OSArchitecture.ToString();
            RuntimeLabel.Text = RuntimeInformation.FrameworkDescription;
        }
        catch
        {
            VersionLabel.Text = "Version (indisponible)";
        }
    }

    // ★ Animations d'apparition + pulsation néon
    private async void OnLoaded(object? sender, System.EventArgs e)
    {
        RootContent.Opacity = 0;
        RootContent.Scale = 0.94;

        await Task.WhenAll(
            RootContent.FadeTo(1, 260, Easing.CubicOut),
            RootContent.ScaleTo(1, 260, Easing.CubicOut));

        _pulseActive = true;
        _ = NeonPulseLoop();
    }

    private async Task NeonPulseLoop()
    {
        while (_pulseActive)
        {
            await GlowBorder.FadeTo(0.85, 650, Easing.SinInOut);
            await GlowBorder.FadeTo(0.25, 650, Easing.SinInOut);
        }
    }

    private async void OnCopyInfoClicked(object? sender, System.EventArgs e)
    {
        var info =
            $"MOTO Editor {AppInfo.VersionString} (build {AppInfo.BuildString})\n" +
            $"OS : {DeviceInfo.Platform} {DeviceInfo.VersionString}\n" +
            $"Architecture : {RuntimeInformation.OSArchitecture}\n" +
            $"Runtime : {RuntimeInformation.FrameworkDescription}";
        await Clipboard.SetTextAsync(info);
    }
}
