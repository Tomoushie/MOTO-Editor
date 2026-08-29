# scripts/create-signing-cert.ps1
$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=MOTO" `
    -KeyUsage DigitalSignature -FriendlyName "MOTO Signing" `
    -CertStoreLocation "Cert:\CurrentUser\My"

$pwd = ConvertTo-SecureString -String "moto" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "moto-signing.pfx" -Password $pwd
Export-Certificate -Cert $cert -FilePath "moto-signing.cer"

# Pour sideloader sur une machine : installer le .cer dans TrustedPeople
# Import-Certificate -FilePath moto-signing.cer -CertStoreLocation "Cert:\LocalMachine\TrustedPeople"
