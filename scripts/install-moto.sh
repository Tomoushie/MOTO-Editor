#!/usr/bin/env bash
# scripts/install-moto.sh
# Installe MOTO Editor sur macOS (.pkg) ou Linux (AppImage)

set -e

OS="$(uname -s)"
ARCH="$(uname -m)"

# Détection de l'architecture
if [[ "$ARCH" == "x86_64" ]]; then
    ARCH_SUFFIX="x64"
elif [[ "$ARCH" == "arm64" ]] || [[ "$ARCH" == "aarch64" ]]; then
    ARCH_SUFFIX="arm64"
else
    echo "❌ Architecture non supportée : $ARCH"
    exit 1
fi

# URL de base pour les releases (à adapter selon votre repo)
BASE_URL="https://github.com/votre-org/moto-editor/releases/latest/download"

case "$OS" in
    Darwin)
        echo "🍎 macOS détecté ($ARCH) — Installation de MOTO Editor..."
        PKG_NAME="moto-editor-osx-${ARCH_SUFFIX}.pkg"
        DOWNLOAD_URL="${BASE_URL}/${PKG_NAME}"
        TEMP_PATH="/tmp/moto-editor.pkg"

        echo "⬇️  Téléchargement depuis $DOWNLOAD_URL..."
        curl -L "$DOWNLOAD_URL" -o "$TEMP_PATH"

        echo "📦 Installation du paquet PKG..."
        sudo installer -pkg "$TEMP_PATH" -target /

        # Nettoyer
        rm -f "$TEMP_PATH"

        echo "✅ MOTO Editor installé avec succès !"
        echo "🚀 Lancez MOTO Editor depuis le Launchpad ou Spotlight."
        ;;

    Linux)
        echo "🐧 Linux détecté ($ARCH) — Installation de MOTO Editor..."
        APPIMAGE_NAME="moto-editor-linux-${ARCH_SUFFIX}.AppImage"
        DOWNLOAD_URL="${BASE_URL}/${APPIMAGE_NAME}"
        INSTALL_DIR="$HOME/Applications"
        INSTALL_PATH="$INSTALL_DIR/moto-editor.AppImage"

        # Créer le répertoire d'installation si nécessaire
        mkdir -p "$INSTALL_DIR"

        echo "⬇️  Téléchargement depuis $DOWNLOAD_URL..."
        curl -L "$DOWNLOAD_URL" -o "$INSTALL_PATH"

        # Rendre exécutable
        chmod +x "$INSTALL_PATH"

        # Créer un raccourci dans ~/.local/bin si disponible
        if [[ -d "$HOME/.local/bin" ]]; then
            ln -sf "$INSTALL_PATH" "$HOME/.local/bin/moto-editor"
            echo "🔗 Lien symbolique créé : moto-editor"
        fi

        # Créer une entrée de menu .desktop
        DESKTOP_FILE="$HOME/.local/share/applications/moto-editor.desktop"
        mkdir -p "$(dirname "$DESKTOP_FILE")"
        cat > "$DESKTOP_FILE" <<EOF
[Desktop Entry]
Name=MOTO Editor
Comment=Éditeur de code IA local
Exec=$INSTALL_PATH
Icon=moto-editor
Type=Application
Categories=Development;IDE;
EOF

        echo "✅ MOTO Editor installé avec succès !"
        echo "🚀 Lancez avec : moto-editor ou depuis le menu Applications."
        ;;

    *)
        echo "❌ Système d'exploitation non supporté : $OS"
        exit 1
        ;;
esac
