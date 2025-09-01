#!/usr/bin/env bash
set -euo pipefail

# ----------------------------------------
# Pi 5 homelab initializer
# - Installs Docker & Compose
# - Creates /srv layout
# ----------------------------------------

LOG() { printf "\n\033[1;32m==> %s\033[0m\n" "$*"; }
WARN(){ printf "\n\033[1;33m[!] %s\033[0m\n" "$*"; }
ERR() { printf "\n\033[1;31m[✗] %s\033[0m\n" "$*"; exit 1; }

# Detect target user (who should be in the docker group)
TARGET_USER="${SUDO_USER:-${USER:-pi}}"
[[ "$EUID" -ne 0 ]] && { LOG "Re-running as root…"; exec sudo -E bash "$0" "$@"; }

LOG "Updating system packages"
apt-get update -y
DEBIAN_FRONTEND=noninteractive apt-get full-upgrade -y

# Optional but avoids serial device conflicts with Zigbee sticks
if systemctl list-unit-files | grep -q ModemManager.service; then
  LOG "Disabling ModemManager (to avoid grabbing USB radios)"
  systemctl disable --now ModemManager || true
fi

LOG "Installing Docker (convenience script)"
curl -fsSL https://get.docker.com | sh

LOG "Enabling & starting Docker"
systemctl enable --now docker

LOG "Adding user '$TARGET_USER' to docker group"
usermod -aG docker "$TARGET_USER" || true

# ----- Folder layout -----
LOG "Creating /srv folder layout"
mkdir -p \
  /srv/compose \
  /srv/homeassistant/config \
  /srv/nextcloud/{app,data,db,redis} \
  /srv/glance

chown -R "$TARGET_USER:$TARGET_USER" /srv

# Helpful notice
LOG "All set."
echo
echo "Folders: /srv/{compose,homeassistant,nextcloud,glance}"
echo "Compose:  /srv/compose/docker-compose.yml"
echo "Env:      /srv/compose/.env   (CHANGE the DB passwords!)"
echo
echo "Next steps (as $TARGET_USER):"
echo "  cd /srv/compose"
echo "  docker compose pull"
echo "  docker compose up -d"
echo
WARN "You must log out/in (or reboot) for '$TARGET_USER' to use Docker without sudo."