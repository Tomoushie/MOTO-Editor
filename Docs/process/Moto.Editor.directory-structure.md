Structure des dossiers — carte claire pour Claude Code

# MOTO Editor — Directory Structure

Ce document décrit l’arborescence complète du projet.

---

# 1. Racine

/
├── Moto.Editor/
├── Moto.Core/
├── Snake2000.Engine/
├── Moto.Installer/
├── Moto.SignTool/
├── Shared/
├── Docs/
├── scripts/
├── dist/
└── keys/

---

# 2. Moto.Editor/

Moto.Editor/
├── Views/
├── Controls/
├── Services/
├── Windows/
├── Platforms/
│   ├── Windows/
│   ├── Mac/
│   └── Linux/
├── Resources/
├── Themes/
└── DependencyInjection/


---

# 3. Moto.Core/

Moto.Core/
├── AI/
│   ├── Cortex/
│   ├── Neural/
│   ├── Orchestration/
│   ├── Speculative/
│   ├── Workspace/
│   └── Suggestions/
├── Settings/
├── Logging/
├── Security/
├── Plugins/
└── Services/


---

# 4. Snake2000.Engine/

Snake2000.Engine/
├── AgentIntegrated/
│   └── Pipeline/
├── Rendering/
├── Gameplay/
└── Systems/


---

# 5. Shared/

Shared/
├── PayloadExtractor.cs
├── UpdateManifest.cs
├── Sha256Helper.cs
├── Ed25519.cs
├── PayloadVerifier.cs
├── ResumableDownloader.cs
└── AtomicUpdater.cs


---

# 6. Moto.Installer/

Moto.Installer/
├── Program.cs
├── Ui.cs
├── Platform.cs
└── Moto.Installer.csproj


---

# 7. Moto.SignTool/

Moto.SignTool/
├── Program.cs
└── Moto.SignTool.csproj


---

# 8. Docs/

Docs/
├── Moto.Editor.slnf
├── Moto.Editor.workspace.json
├── Moto.Editor.projectmap.json
├── Moto.Editor.modules.json
├── Moto.Editor.architecture.md
├── Moto.Editor.update-pipeline.md
├── Moto.Editor.installation-flow.md
├── Moto.Editor.code-style.md
├── Moto.Editor.naming-conventions.md
└── Moto.Editor.directory-structure.md


---

# 9. scripts/

scripts/
├── build-installer.ps1
├── build-update-manifest.ps1
├── build-msix.ps1
└── create-signing-cert.ps1


---

# 10. dist/

dist/
├── payload/
├── payload.zip
├── updates/
└── msix/
