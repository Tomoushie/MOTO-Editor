// Moto.Editor/Views/PluginGalleryView.cs — AJOUTS
private ISignatureVerifier? _signatureVerifier;

public void SetSignatureVerifier(ISignatureVerifier verifier)
{
    _signatureVerifier = verifier;
}

private async Task<bool> VerifyPackSignatureAsync(string packJson, string signature)
{
    if (_signatureVerifier == null)
        return false;

    var isValid = _signatureVerifier.Verify(packJson, signature);

    if (!isValid)
    {
        await Application.Current!.MainPage!.DisplayAlert(
            "⚠️ Signature invalide",
            "Ce pack a une signature invalide et pourrait être compromis.",
            "OK");
    }

    return isValid;
}
