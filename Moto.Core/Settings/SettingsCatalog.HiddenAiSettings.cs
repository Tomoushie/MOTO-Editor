// Moto.Core/Settings/SettingsCatalog.HiddenAiSettings.cs
// ★ AJOUT (02/09, chantier Réglages — 126 réglages IA cachés) : ces 123
// réglages (sur les ~126 recensés — 3 exclus, voir plus bas) existaient déjà
// et tournaient réellement (SettingItem<T>, un système à part entière et
// authentique, utilisé par une quarantaine de vrais services backend —
// GitService, FeatureFlagService, CoherenceGuardService, PedagogyEngine,
// AiProfileService, StyleLearningService, AiUxService, etc. — vérifié par une
// revue croisée avant cette passe), mais n'étaient JAMAIS ajoutés à
// SettingsCatalog.All : invisibles de tout écran, y compris de la fenêtre
// Réglages fraîchement rebranchée sur le vrai catalogue. Enregistrés ici avec
// les MÊMES id de persistance (settings.json), pour que cet écran lise/écrive
// exactement les mêmes valeurs que les vrais services qui les consomment déjà
// — aucune nouvelle clé, aucun renommage.
//
// Ids/valeurs par défaut copiés VERBATIM des fichiers sources
// (SettingsCatalog.Ai.Advanced.cs, .Ai.Agents.cs, .Ai.Hardening.cs,
// .Ai.Profiles.cs, .Ai.ResourceGovernor.cs, .Collab.cs, .DevOps.cs,
// .Editor.Update.cs [copie Moto.Core, PAS l'autre dans Moto.Installer —
// fragment invalide, hors sujet], .Editor.Ux.cs, .Editor.UxAdvanced.cs,
// .Git.cs, .Marketplace.cs, .Mcp.cs, .McpAdvanced.cs) — Category/Section/
// Title/Description/bornes Min-Max ajoutés ici (absents du système
// SettingItem<T> d'origine, qui n'a qu'Id/Défaut/Description).
//
// Catégories choisies pour éviter d'ajouter de nouveaux noms proches de ceux
// qui créent déjà une confusion connue ("Agent" vs "AI") : la plupart de ces
// réglages rejoignent des catégories EXISTANTES (AI, Collaboration,
// Developer, Général, Éditeur, Version Control) avec de nouvelles sections ;
// seules 2 catégories sont réellement nouvelles ("Marketplace", "MCP"), sans
// équivalent existant.
//
// Points relevés pendant l'extraction, à connaître (pas corrigés ici — hors
// scope de "rendre visible", ce sont des questions de contenu/produit) :
// - editor.update.releaseUrl a pour valeur par défaut une URL contenant
//   "votre-org" — un placeholder de modèle jamais rempli avec le vrai dépôt
//   GitHub. Enregistré tel quel (verbatim) ; à corriger séparément si/quand
//   la vérification de mise à jour est réellement branchée.
// - mcp.permissionMode et mcp.adv.permissionMode sont deux réglages distincts
//   (ids différents) à la description quasi identique — pas fusionnés ici,
//   United behavior à trancher séparément si besoin.
// - editor.ux.* et editor.uxa.* (UxAdvanced) définissent chacun leur propre
//   variante de CompactMode/FocusMode/AdaptiveFont/KeyboardOnboarding/
//   ThemeMicroTuning, avec des ids et descriptions proches mais distincts —
//   exposés comme 2 réglages séparés (fidèle à ce qui existe réellement dans
//   le code), pas fusionnés.
// - 3 réglages du système SettingItem<T> NE sont PAS repris ici : le fichier
//   Moto.Installer/SettingsCatalog.Editor.Update.cs (fragment syntaxiquement
//   invalide, jamais compilé, sans rapport avec la vraie copie Moto.Core
//   utilisée ci-dessous) — ~126 recensés au total, 123 réellement exploitables.
namespace Moto.Core.Settings
{
    public static partial class SettingsCatalog
    {
        static partial void RegisterHiddenAiSettings();

        static partial void RegisterHiddenAiSettings()
        {
            // ==================== AI — Décodage avancé (Ai.Advanced.cs) ====================
            T("ai.speculative.enabled", "AI", "Décodage avancé", "Décodage spéculatif", "Active le décodage spéculatif basé sur la vérification des logits pour accélérer l'inférence.", true);
            I("ai.circuit.threshold", "AI", "Décodage avancé", "Seuil du circuit breaker", "Nombre d'erreurs consécutives tolérées avant l'ouverture du circuit (circuit breaker).", 3, 1, 20);
            I("ai.prefetch.maxConcurrent", "AI", "Décodage avancé", "Prefetch simultané max", "Nombre maximal de slots de préchargement exécutés en parallèle (gestion de la contre-pression).", 3, 1, 16);
            T("ai.perf.maxMode", "AI", "Décodage avancé", "Mode performance maximale", "Désactive les optimisations d'économie d'énergie pour prioriser la performance brute.", false);

            // ==================== AI — Agents (Ai.Agents.cs) ====================
            T("ai.agents.enabled", "AI", "Agents", "Agents spécialisés", "Active les agents IA spécialisés dans l'éditeur.", true);
            T("ai.agents.explainability", "AI", "Agents", "Explicabilité des agents", "Journalise les décisions prises par les agents IA à des fins d'audit.", true);
            T("ai.agents.localRl", "AI", "Agents", "Apprentissage par renforcement local", "Active une boucle de feedback par renforcement (RL) exécutée localement, sans dépendance au cloud.", true);
            I("ai.agents.sandboxTimeoutSec", "AI", "Agents", "Timeout sandbox agent", "Délai maximal, en secondes, accordé au sandbox LLM local avant interruption.", 30, 5, 300, 5);

            // ==================== AI — Sécurité IA (Ai.Hardening.cs) ====================
            T("ai.telemetry.enabled", "AI", "Sécurité IA", "Télémétrie", "Active une télémétrie respectueuse de la vie privée, désactivée par défaut (opt-in).", false);
            T("ai.prefetch.adaptive", "AI", "Sécurité IA", "Préfetch adaptatif", "Ajuste dynamiquement le préchargement selon la charge, avec gestion de la contre-pression.", true);
            I("ai.speculative.logitsTopK", "AI", "Sécurité IA", "Top-K spéculatif", "Nombre de candidats logits (top-K) utilisés pour la vérification lors du décodage spéculatif.", 5, 1, 50);
            D("ai.speculative.acceptThreshold", "AI", "Sécurité IA", "Seuil d'acceptation spéculative", "Seuil de confiance, entre 0 et 1, au-delà duquel une prédiction spéculative est acceptée.", 0.6, 0.0, 1.0);

            // ==================== AI — Performance IA (Ai.ResourceGovernor.cs) ====================
            T("ai.resource.cooperativeMode", "AI", "Performance IA", "Mode coopératif ressources", "Réduit l'empreinte mémoire et CPU de MOTO lorsqu'un modèle local externe est détecté.", true);
            I("ai.resource.scanIntervalSec", "AI", "Performance IA", "Intervalle de détection", "Intervalle, en secondes, entre deux analyses de détection de modèles externes.", 5, 1, 120);
            T("ai.resource.trimMemory", "AI", "Performance IA", "Libération mémoire coopérative", "Libère la mémoire inutilisée lorsque le mode coopératif est actif.", true);
            T("ai.resource.lowerPriority", "AI", "Performance IA", "Priorité réduite coopérative", "Abaisse la priorité du processus MOTO lorsque le mode coopératif est actif.", true);

            // ==================== AI — Profils IA (Ai.Profiles.cs, 35 réglages) ====================
            // -- Pédagogie --
            T("ai.profiles.pedagogy", "AI", "Profils IA — Pédagogie", "Mode pédagogique", "Active le mode pédagogique actif de l'IA (explications renforcées).", false);
            T("ai.profiles.explainOnly", "AI", "Profils IA — Pédagogie", "Explication seule", "L'IA explique sans générer de code.", false);
            T("ai.profiles.antiMagic", "AI", "Profils IA — Pédagogie", "Anti code magique", "Rend les explications obligatoires pour tout code généré, afin d'éviter le \"code magique\".", true);
            T("ai.profiles.tutorial", "AI", "Profils IA — Pédagogie", "Tutoriel interactif", "Active un tutoriel interactif guidé par l'IA.", false);
            T("ai.profiles.checklist", "AI", "Profils IA — Pédagogie", "Checklist de progression", "Affiche une checklist de progression pendant les sessions IA.", true);
            // -- Comportement --
            T("ai.profiles.minimalist", "AI", "Profils IA — Comportement", "Mode minimaliste", "Limite l'IA à des suggestions ultra-courtes.", false);
            T("ai.profiles.refactorOnly", "AI", "Profils IA — Comportement", "Refactor uniquement", "Restreint l'IA aux opérations de refactorisation uniquement.", false);
            T("ai.profiles.silentArchitect", "AI", "Profils IA — Comportement", "Architecte silencieux", "L'IA ne produit que des diagrammes, sans code ni texte explicatif.", false);
            T("ai.profiles.strictCSharp", "AI", "Profils IA — Comportement", "C# strict", "Force un style C# strictement idiomatique dans les suggestions.", false);
            T("ai.profiles.gameEngine", "AI", "Profils IA — Comportement", "Focus moteur de jeu", "Oriente l'IA vers les patterns propres aux moteurs de jeu.", false);
            T("ai.profiles.noExternalDeps", "AI", "Profils IA — Comportement", "Aucune dépendance externe", "Interdit à l'IA de proposer des bibliothèques externes.", true);
            T("ai.profiles.debugCoach", "AI", "Profils IA — Comportement", "Coach de débogage", "Active un mode coach pour accompagner le débogage.", false);
            // -- UX --
            T("ai.profiles.zenAi", "AI", "Profils IA — UX", "Mode zen IA", "Affiche une interface épurée pour les interactions IA.", false);
            T("ai.profiles.noSuggestions", "AI", "Profils IA — UX", "Réponses seules", "L'IA fournit uniquement des réponses, sans suggestions annexes.", false);
            T("ai.profiles.showCost", "AI", "Profils IA — UX", "Afficher les coûts", "Affiche les coûts estimés des opérations IA.", true);
            T("ai.profiles.timeline", "AI", "Profils IA — UX", "Timeline des décisions", "Affiche une timeline des décisions prises par l'IA.", true);
            // -- Modèles --
            T("ai.profiles.use3B", "AI", "Profils IA — Modèles", "Modèle 3B par défaut", "Utilise le modèle 3B par défaut, et le modèle 7B seulement sur demande.", true);
            T("ai.profiles.lowRam", "AI", "Profils IA — Modèles", "Mode basse RAM", "Active un mode optimisé pour les systèmes à faible RAM.", false);
            I("ai.profiles.ramThresholdGb", "AI", "Profils IA — Modèles", "Seuil RAM pour 7B", "Seuil de RAM (en Go) au-delà duquel le modèle 7B est autorisé.", 16, 4, 128);
            T("ai.profiles.sharedModel", "AI", "Profils IA — Modèles", "Modèle partagé", "Partage un même modèle entre plusieurs instances de l'éditeur.", false);
            T("ai.profiles.noGpu", "AI", "Profils IA — Modèles", "Mode CPU uniquement", "Force l'exécution des modèles IA sur CPU uniquement, sans GPU.", false);
            T("ai.profiles.nightlyHeavy", "AI", "Profils IA — Modèles", "Tâches lourdes nocturnes", "Planifie les tâches IA lourdes pendant la nuit.", true);
            // -- Cohérence --
            T("ai.profiles.coherenceContract", "AI", "Profils IA — Cohérence", "Contrat de cohérence", "Impose un contrat de cohérence entre les générations successives de l'IA.", true);
            T("ai.profiles.dependencyAudit", "AI", "Profils IA — Cohérence", "Audit des dépendances", "Fait auditer les dépendances par l'IA avant de générer du code.", true);
            T("ai.profiles.responsibilityMap", "AI", "Profils IA — Cohérence", "Carte des responsabilités", "Maintient une carte des responsabilités des modules du projet.", true);
            T("ai.profiles.magicCodeDetection", "AI", "Profils IA — Cohérence", "Détection de code magique", "Détecte automatiquement le code magique généré par l'IA.", true);
            T("ai.profiles.logicalDuplication", "AI", "Profils IA — Cohérence", "Contrôle de duplication", "Contrôle la duplication logique introduite par les suggestions IA.", true);
            T("ai.profiles.apiContractFirst", "AI", "Profils IA — Cohérence", "API contract-first", "Impose une approche contract-first pour la conception des API.", false);
            T("ai.profiles.architectureJournal", "AI", "Profils IA — Cohérence", "Journal d'architecture", "Tient un journal des décisions d'architecture prises avec l'IA.", true);
            // -- Style --
            T("ai.profiles.styleLearningLocal", "AI", "Profils IA — Style", "Apprentissage de style local", "Apprend le style de code localement à partir du projet.", true);
            T("ai.profiles.imitateOldCode", "AI", "Profils IA — Style", "Imiter l'ancien code", "Fait imiter le style de l'ancien code existant par l'IA.", false);
            T("ai.profiles.styleConsistencyScore", "AI", "Profils IA — Style", "Score de cohérence de style", "Calcule un score de cohérence de style pour le code généré.", true);
            T("ai.profiles.styleDiff", "AI", "Profils IA — Style", "Vue diff de style", "Affiche une vue de type diff pour comparer les styles de code.", true);
            T("ai.profiles.noAutoFormat", "AI", "Profils IA — Style", "Désactiver l'auto-format", "Désactive le formatage automatique du code par l'IA.", false);
            T("ai.profiles.styleMentor", "AI", "Profils IA — Style", "Mentor de style", "Active un mode mentor pour guider vers un style de code cohérent.", true);

            // ==================== Version Control — Intégration Git (Git.cs) ====================
            T("git.enabled", "Version Control", "Intégration Git", "Intégration Git", "Active l'intégration Git dans l'éditeur.", true);
            T("git.autoFetch", "Version Control", "Intégration Git", "Fetch automatique", "Récupère automatiquement les changements du dépôt distant.", true);
            I("git.autoFetchIntervalMin", "Version Control", "Intégration Git", "Intervalle de fetch", "Intervalle, en minutes, entre deux récupérations automatiques (fetch).", 5, 1, 60);
            T("git.confirmBeforePush", "Version Control", "Intégration Git", "Confirmation avant push", "Demande une confirmation avant d'exécuter un push Git.", true);

            // ==================== Marketplace (Marketplace.cs — nouvelle catégorie) ====================
            T("marketplace.trial.enabled", "Marketplace", "Essai & Paiements", "Période d'essai", "Active la période d'essai gratuite du Marketplace.", true);
            I("marketplace.trial.days", "Marketplace", "Essai & Paiements", "Durée de l'essai", "Durée de la période d'essai, en jours.", 14, 1, 90);
            T("marketplace.donations.enabled", "Marketplace", "Essai & Paiements", "Micro-dons OSS", "Active les micro-dons destinés aux plugins open source.", true);
            S("marketplace.payment.currency", "Marketplace", "Essai & Paiements", "Devise des paiements", "Devise utilisée pour les paiements effectués sur le Marketplace.", "EUR");
            T("marketplace.sandbox.enabled", "Marketplace", "Sécurité plugins", "Sandbox plugins", "Exécute les plugins non vérifiés dans un environnement isolé (sandbox).", true);
            T("marketplace.vulnscan.auto", "Marketplace", "Sécurité plugins", "Scan de vulnérabilités automatique", "Analyse automatiquement les plugins à la recherche de vulnérabilités.", true);

            // ==================== Collaboration — Fonctionnalités avancées (Collab.cs) ====================
            T("collab.review.lanesEnabled", "Collaboration", "Fonctionnalités avancées", "Lanes de review", "Active des lanes de review légères, sans passer par une pull request.", true);
            T("collab.review.offlineQueue", "Collaboration", "Fonctionnalités avancées", "File de review hors-ligne", "Met en file d'attente les commentaires de review effectués hors-ligne pour synchronisation ultérieure.", true);
            T("collab.scratchpads.enabled", "Collaboration", "Fonctionnalités avancées", "Scratchpads partagés", "Active les scratchpads partagés en pair-à-pair (P2P) entre collaborateurs.", true);
            T("collab.annotations.enabled", "Collaboration", "Fonctionnalités avancées", "Couches d'annotation", "Active les couches d'annotation par ligne de code.", true);
            T("collab.annotations.meetingNotes", "Collaboration", "Fonctionnalités avancées", "Notes de réunion liées", "Lie les notes de réunion aux fichiers concernés.", true);
            T("collab.pair.sessionsEnabled", "Collaboration", "Fonctionnalités avancées", "Sessions de pair programming", "Active les sessions de pair programming limitées dans le temps (time-boxed).", true);
            I("collab.pair.defaultMinutes", "Collaboration", "Fonctionnalités avancées", "Durée par défaut d'une session pair", "Durée par défaut d'une session de pair programming, en minutes.", 25, 5, 120, 5);
            T("collab.presence.gateHeavyAi", "Collaboration", "Fonctionnalités avancées", "Suspension IA sur forte présence", "Suspend automatiquement l'IA lourde lorsque trop de collaborateurs sont présents simultanément.", true);
            I("collab.presence.heavyAiThreshold", "Collaboration", "Fonctionnalités avancées", "Seuil de collaborateurs (IA lourde)", "Nombre de collaborateurs simultanés au-delà duquel l'IA lourde est suspendue.", 3, 1, 20);
            T("collab.pr.enabled", "Collaboration", "Fonctionnalités avancées", "Intégration PR légère", "Active l'intégration légère avec les pull requests (ouverture/fermeture/commentaire).", true);
            T("collab.runconfigs.enabled", "Collaboration", "Fonctionnalités avancées", "Configurations de lancement partagées", "Active le partage des configurations de lancement entre collaborateurs.", true);
            T("collab.whiteboard.enabled", "Collaboration", "Fonctionnalités avancées", "Tableau blanc", "Active le tableau blanc vectoriel léger intégré.", true);
            T("collab.roles.enabled", "Collaboration", "Fonctionnalités avancées", "UI adaptée au rôle", "Adapte l'interface selon le rôle de l'utilisateur (éditeur/relecteur/observateur).", true);

            // ==================== Developer — DevOps (DevOps.cs) ====================
            T("devops.perfgate.enabled", "Developer", "DevOps", "CI perf gate", "Active la porte de performance (perf gate) dans la CI.", true);
            D("devops.perfgate.startupMs", "Developer", "DevOps", "Seuil de démarrage (ms)", "Seuil de temps de démarrage en millisecondes au-delà duquel la CI perf gate échoue.", 2000, 100, 30000);
            D("devops.perfgate.memoryMb", "Developer", "DevOps", "Seuil mémoire (Mo)", "Seuil de consommation mémoire en mégaoctets au-delà duquel la CI perf gate échoue.", 512, 64, 8192);
            T("devops.journeys.enabled", "Developer", "DevOps", "User journeys synthétiques", "Active l'exécution de parcours utilisateurs synthétiques automatisés.", true);
            T("devops.fuzzing.enabled", "Developer", "DevOps", "Fuzzing plugins", "Active le fuzzing automatique des plugins.", true);
            T("devops.crashtriage.enabled", "Developer", "DevOps", "Triage automatique des crashes", "Active le triage automatique des rapports de crash.", true);
            T("devops.featureflags.enabled", "Developer", "DevOps", "Feature flags", "Active le système de feature flags.", true);
            T("devops.telemetry.privacySandbox", "Developer", "DevOps", "Sandbox télémétrie", "Active le bac à sable de confidentialité pour la télémétrie.", true);
            D("devops.perfalert.thresholdPercent", "Developer", "DevOps", "Seuil de régression perf (%)", "Pourcentage de régression de performance au-delà duquel une alerte est déclenchée.", 15, 0, 100);

            // ==================== Général — Mise à jour (Editor.Update.cs, copie Moto.Core) ====================
            T("editor.update.autoCheck", "Général", "Mise à jour", "Vérification auto. des mises à jour", "Vérifie automatiquement les mises à jour au démarrage de l'éditeur.", true);
            // ★ NOTE : "votre-org" est un placeholder jamais rempli dans le code source
            // d'origine — copié verbatim, pas corrigé ici (question de contenu, pas de
            // branchement). Voir le commentaire en tête de fichier.
            S("editor.update.releaseUrl", "Général", "Mise à jour", "URL de release", "URL utilisée pour récupérer les informations de la dernière release.", "https://api.github.com/repos/votre-org/moto-editor/releases/latest");
            S("editor.update.channel", "Général", "Mise à jour", "Canal de mise à jour", "Canal de mise à jour utilisé (stable ou beta).", "stable");

            // ==================== MCP — Général (Mcp.cs — nouvelle catégorie) ====================
            T("mcp.enabled", "MCP", "Général", "MCP activé", "Active le Model Context Protocol (MCP).", true);
            S("mcp.serversPath", "MCP", "Général", "Chemin des serveurs MCP", "Chemin du fichier de configuration des serveurs MCP.", ".moto/mcp-servers.json");
            T("mcp.subagents", "MCP", "Général", "Subagents activés", "Active l'utilisation de subagents.", true);
            I("mcp.maxSubagentDepth", "MCP", "Général", "Profondeur max. des subagents", "Profondeur maximale autorisée pour l'imbrication de subagents.", 3, 1, 10);
            T("mcp.promptInjectionProtection", "MCP", "Général", "Protection injection de prompt", "Active la protection contre les injections de prompt.", true);
            S("mcp.permissionMode", "MCP", "Général", "Mode de permission", "Mode de gestion des permissions MCP (ask/auto/deny).", "ask");

            // ==================== MCP — Avancé (McpAdvanced.cs) ====================
            S("mcp.adv.permissionMode", "MCP", "Avancé", "Mode de permission (avancé)", "Mode de gestion avancé des permissions MCP (ask/auto/deny).", "ask");
            S("mcp.adv.managedPolicyPath", "MCP", "Avancé", "Chemin managed policy", "Chemin du fichier de politique managée (équivalent d'un CLAUDE.md).", ".moto/mcp-policy.json");
            T("mcp.adv.hooksEnabled", "MCP", "Avancé", "Hooks pre/post tool", "Active les hooks exécutés avant/après l'appel d'un tool.", true);
            T("mcp.adv.checkpointing", "MCP", "Avancé", "Checkpointing des sessions MCP", "Active le checkpointing (sauvegarde de points de reprise) des sessions MCP.", true);
            I("mcp.adv.maxToolCallsPerTurn", "MCP", "Avancé", "Limite d'appels tools par tour", "Nombre maximal d'appels d'outils autorisés par tour.", 20, 1, 100);
            S("mcp.adv.defaultLanguage", "MCP", "Avancé", "Langue par défaut", "Langue par défaut utilisée pour l'interface utilisateur.", "fr");

            // ==================== Éditeur — UX (Editor.Ux.cs) ====================
            T("editor.ux.compactMode", "Éditeur", "UX", "Mode compact", "Réduit les paddings et marges pour une interface plus dense.", false);
            T("editor.ux.focusMode", "Éditeur", "UX", "Mode focus", "Masque les panneaux latéraux pour se concentrer sur l'édition.", false);
            T("editor.ux.inlineDiff", "Éditeur", "UX", "Prévisualisation diff inline", "Affiche un aperçu du diff en ligne avant application.", true);
            T("editor.ux.sessionBookmarks", "Éditeur", "UX", "Signets de session", "Mémorise les onglets et positions ouverts entre les sessions.", true);
            T("editor.ux.adaptiveFont", "Éditeur", "UX", "Rendu de police adaptatif", "Adapte le rendu des polices selon le DPI de l'écran.", true);
            T("editor.ux.keyboardOnboarding", "Éditeur", "UX", "Onboarding clavier", "Propose un onboarding orienté clavier au premier lancement.", true);
            I("editor.ux.themeBrightness", "Éditeur", "UX", "Luminosité du thème", "Micro-ajustement de la luminosité du thème.", 0, -10, 10);
            T("editor.ux.commandPalette", "Éditeur", "UX", "Palette de commandes", "Active la palette de commandes.", true);
            T("editor.ux.proactiveSuggestions", "Éditeur", "UX", "Suggestions proactives", "Active les suggestions proactives dans l'éditeur.", true);
            T("editor.ux.contextEngine", "Éditeur", "UX", "Moteur de contexte", "Active le moteur de contexte de l'éditeur.", true);

            // ==================== Éditeur — UX avancée (Editor.UxAdvanced.cs) ====================
            // ★ NOTE : variantes distinctes (ids différents) des réglages "UX"
            // ci-dessus — pas fusionnées, fidèle au code source réel. Voir le
            // commentaire en tête de fichier.
            T("editor.uxa.compactMode", "Éditeur", "UX avancée", "Mode compact avancé", "Mode compact à densité maximale.", false);
            T("editor.uxa.focusMode", "Éditeur", "UX avancée", "Mode focus avancé", "Mode focus avec isolation complète de l'interface.", false);
            T("editor.uxa.diffInlineAdv", "Éditeur", "UX avancée", "Diff inline avancé", "Prévisualisation de diff inline avec fonctionnalités avancées.", true);
            T("editor.uxa.visualBookmarks", "Éditeur", "UX avancée", "Signets visuels", "Active les signets visuels dans l'éditeur.", true);
            T("editor.uxa.refactorPreview", "Éditeur", "UX avancée", "Aperçu de refactoring inline", "Affiche un aperçu inline lors d'un refactoring.", true);
            T("editor.uxa.accessibilityAudit", "Éditeur", "UX avancée", "Audit d'accessibilité", "Active l'audit d'accessibilité de l'interface.", true);
            T("editor.uxa.contextualHelp", "Éditeur", "UX avancée", "Aide contextuelle", "Affiche une surcouche d'aide contextuelle.", true);
            T("editor.uxa.workspaceTemplates", "Éditeur", "UX avancée", "Modèles de workspace", "Active les modèles de configuration de workspace.", true);
            T("editor.uxa.dynamicPanels", "Éditeur", "UX avancée", "Panneaux dynamiques", "Active les panneaux dynamiques réorganisables.", true);
            T("editor.uxa.adaptiveFont", "Éditeur", "UX avancée", "Rendu de police adaptatif", "Adapte le rendu des polices selon le DPI de l'écran.", true);
            T("editor.uxa.keyboardOnboarding", "Éditeur", "UX avancée", "Onboarding clavier avancé", "Onboarding orienté clavier avec options avancées.", true);
            I("editor.uxa.themeMicroTuning", "Éditeur", "UX avancée", "Micro-réglage du thème", "Ajustement fin du thème.", 0, -10, 10);
            T("editor.uxa.fluidInteractions", "Éditeur", "UX avancée", "Interactions fluides", "Active des interactions fluides dans l'interface.", true);
            T("editor.uxa.microAnimations", "Éditeur", "UX avancée", "Micro-animations", "Active les micro-animations d'interface.", true);
            I("editor.uxa.animationSpeedMs", "Éditeur", "UX avancée", "Vitesse des animations", "Durée des animations d'interface, en millisecondes.", 150, 0, 1000, 10);
        }
    }
}
