Snake2000.Engine.AgentIntegrated/
├── Core/
│   ├── AgentContext.cs
│   └── AgentResult.cs
└── Scanner/
    └── AgentScanner.cs

Moto.Core/
├── AI/
│   └── Internal/
│       ├── Models/
│       │   ├── AiModels.cs
│       │   └── ProjectModel.cs
│       ├── ProjectUnderstandingEngine.cs
│       ├── IntentDetector.cs
│       ├── CodeGenerationEngine.cs
│       ├── CodeFixEngine.cs
│       ├── CodeImprovementEngine.cs
│       ├── PedagogyEngine.cs
│       ├── ActionEngine.cs
│       └── MotoAiKernel.cs
└── Integration/
    └── XenoGateway.cs

Moto.Editor/
├── Services/
│   └── MotoAiService.cs
└── Pages/
    ├── MotoAiPage.xaml
    └── MotoAiPage.xaml.cs

Règles d’architecture :

MOTO Editor : édite, affiche, ouvre, sauvegarde, lance des commandes.
MOTO AI : assiste l’utilisateur, prédit, propose, route les demandes.
Ollama : modèles locaux pour complétion, génération légère, chat.
XENO-SSS∞ : opérations structurées sur projet complet.

MOTO Editor ne doit pas :
parser profondément le code ;
générer une architecture projet ;
connecter des dépendances ;
valider des namespaces ;
inventer des systèmes.

MOTO Editor appelle XENO comme :
VS Code appelle Copilot ;
Cursor appelle son backend IA ;
Zed appelle Claude Code.
