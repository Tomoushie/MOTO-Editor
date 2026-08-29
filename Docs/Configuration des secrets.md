# 1. Génère la paire de clés en local (JAMAIS dans le repo)
dotnet run --project Moto.SignTool -- --gen-keys --out keys

# 2. GitHub → Settings → Secrets and variables → Actions :
#    MOTO_UPDATE_PRIV_KEY = contenu de keys/update.priv
#    MOTO_UPDATE_PUB_KEY  = contenu de keys/update.pub

# 3. Committe Shared/BuildKeys.cs (clé publique, non sensible)
#    keys/*.priv est déjà exclu par .gitignore (géré par le script)
