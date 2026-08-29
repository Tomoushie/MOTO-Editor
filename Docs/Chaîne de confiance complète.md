[Build]  SignTool --gen-keys          → update.priv (secret) / update.pub
         build-update-manifest.ps1    → manifest.json (hash SHA256 par fichier + payload)
         SignTool --sign-manifest     → manifest.Signature = Ed25519(hash, priv)
         SignTool --emit-buildkeys    → Shared/BuildKeys.cs (pub embarquée)

[Update] AutoUpdateService télécharge payload.zip + manifest.json
         Installateur --update        → PayloadVerifier (SHA256 + Ed25519 avec BuildKeys.pub)
                                        → extraction temp → AtomicUpdater (swap + rollback)
