tag v1.2.0 (ou dispatch)
  → publish editor (dist/payload)
  → clés injectées depuis secrets (runner éphémère, cleanup always())
  → build-update-manifest.ps1
      ├─ payload.zip (compressé si absent)
      ├─ payload.json (hashs par fichier + PayloadSha256)
      ├─ signature Ed25519 (Moto.SignTool --sign-manifest)
      └─ BuildKeys.cs régénéré (idempotent, même paire de clés)
  → installateur single-file (payload embarqué)
  → release GitHub : payload.zip + payload.json + MotoEditor-Setup.exe + checksums.txt
  → AutoUpdateService (éditeur) trouve l'asset "Setup.exe" → télécharge (resume + mirrors)
  → installateur --update → vérif SHA256 + Ed25519 → extraction temp → swap atomique + rollback
