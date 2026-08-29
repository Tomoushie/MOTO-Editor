┌─────────────────────────────────────────────┐
│ Moto.Editor (MAUI)                          │ ← UI cross-platform
│ MainPage v21 · EditorPane · CodeEditor      │
│ (WebView maison) · 20+ panneaux overlays 	  │
├─────────────────────────────────────────────┤
│ Moto.Core (logique) 						  │ ← portable, sans UI
│ AI/ · Doc/ · Platform/ · Export/ · Remote/  │
│ Collab/ · Services/ · Performance/ ·		  │
│ Settings/ 								  │
├─────────────────────────────────────────────┤
│ Snake2000.Engine.AgentIntegrated 			  │ ← XENO-SSS∞
│ Scanner · Analyzer · Synthesizer · 		  │
│ Connector · Validator · Pipeline 			  │
└─────────────────────────────────────────────┘

Scanner → Analyzer → Synthesizer → Connector → Validator
│ │ │ │ │
│ │ │ │ └─ cohérence finale
│ │ │ └─ using/DI/appels/connexions
│ │ └─ génération de code
│ └─ incohérences, dépendances
└─ carte du projet (fichiers, symboles)