#if WINDOWS
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;
#endif

namespace Moto.Core.Voice;

/// <summary>
/// Moteur vocal ultra-léger utilisant les API natives Windows :
/// - TTS : Windows.Media.SpeechSynthesis (0 MB, intégré OS)
/// - STT : Windows.Media.SpeechRecognition (0 MB, intégré OS)
/// Désactivé par défaut, activable via SettingsCatalog.
/// </summary>
public sealed class VoiceEngine : IDisposable
{
    private bool _isEnabled;

#if WINDOWS
    private SpeechSynthesizer? _synthesizer;
    private SpeechRecognizer? _recognizer;
#endif

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            if (value) InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            else DisposeInternal();
        }
    }

    public event Action<string>? OnTranscriptionReceived;
    public event Action? OnSpeechStarted;
    public event Action? OnSpeechEnded;

    private async Task InitializeAsync()
    {
#if WINDOWS
        _synthesizer = new SpeechSynthesizer();

        // Liste les voix disponibles et choisit la première française
        var voices = SpeechSynthesizer.AllVoices;
        var frVoice = voices.FirstOrDefault(v => v.Language.StartsWith("fr")) ?? voices.FirstOrDefault();
        if (frVoice != null) _synthesizer.Voice = frVoice;

        _recognizer = new SpeechRecognizer(new Windows.Globalization.Language("fr-FR"));
        await _recognizer.CompileConstraintsAsync();
#endif
    }

    /// <summary>
    /// Lit du texte à voix haute (TTS).
    /// </summary>
    public async Task SpeakAsync(string text)
    {
        if (!_isEnabled) return;
#if WINDOWS
        if (_synthesizer == null) return;
        await _synthesizer.SpeakTextAsync(text);
#endif
    }

    /// <summary>
    /// Lit du code à voix haute (ajoute pauses et prononciation adaptée).
    /// </summary>
    public async Task SpeakCodeAsync(string code)
    {
        if (!_isEnabled) return;

        // Adaptation pour la lecture de code
        var spoken = code
            .Replace("{", " accolade ouvrante ")
            .Replace("}", " accolade fermante ")
            .Replace("(", " parenthèse ouvrante ")
            .Replace(")", " parenthèse fermante ")
            .Replace(";", " point-virgule ")
            .Replace("=>", " flèche ")
            .Replace("==", " égal égal ")
            .Replace("!=", " différent de ")
            .Replace("<", " inférieur ")
            .Replace(">", " supérieur ");

        await SpeakAsync(spoken);
    }

    /// <summary>
    /// Démarre l'écoute du micro (STT).
    /// </summary>
    public async Task StartListeningAsync()
    {
        if (!_isEnabled) return;
#if WINDOWS
        if (_recognizer == null) return;

        OnSpeechStarted?.Invoke();

        var result = await _recognizer.RecognizeAsync();

        if (result.Status == SpeechRecognitionResultStatus.Success)
        {
            OnTranscriptionReceived?.Invoke(result.Text);
        }

        OnSpeechEnded?.Invoke();
#endif
    }

    /// <summary>
    /// Arrête l'écoute.
    /// </summary>
    public void StopListening()
    {
#if WINDOWS
        _recognizer?.StopRecognitionAsync().AsTask().Wait();
#endif
    }

    private void DisposeInternal()
    {
#if WINDOWS
        _synthesizer?.Dispose();
        _synthesizer = null;
        _recognizer?.Dispose();
        _recognizer = null;
#endif
    }

    public void Dispose() => DisposeInternal();
}
