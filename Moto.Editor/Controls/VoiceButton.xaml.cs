using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Moto.Core.Voice;

namespace Moto.Editor.Controls;

public sealed partial class VoiceButton : UserControl
{
    private VoiceEngine? _engine;

    public event Action<string>? OnTranscription;

    public VoiceButton()
    {
        this.InitializeComponent();
    }

    private async void OnVoiceToggled(object sender, RoutedEventArgs e)
    {
        _engine ??= App.Services?.GetRequiredService<VoiceEngine>();
        if (_engine == null) return;

        _engine.IsEnabled = true;
        _engine.OnTranscriptionReceived += text => OnTranscription?.Invoke(text);

        await _engine.StartListeningAsync();
    }

    private void OnVoiceUntoggled(object sender, RoutedEventArgs e)
    {
        _engine?.StopListening();
        _engine?.Dispose();
        _engine = null;
    }
}
