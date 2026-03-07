#!/bin/sh
set -e

[ "$(id -u)" -ne 0 ] && exec sudo "$0" "$@"

LOG() { printf "\n\033[1;32m==> %s\033[0m\n" "$*"; }
WARN(){ printf "\n\033[1;33m[!] %s\033[0m\n" "$*"; }

# ----- System packages -----

LOG "Updating system packages"
apt-get update -y
DEBIAN_FRONTEND=noninteractive apt-get full-upgrade -y

LOG "Installing required packages"
apt-get install --no-install-recommends -y \
  samba \
  restic \
  kodi \
  libcec6 \
  cec-utils

if systemctl list-unit-files | grep -q ModemManager.service; then
  LOG "Disabling ModemManager"
  systemctl disable --now ModemManager || true
fi

if ! command -v docker > /dev/null 2>&1; then
  LOG "Installing Docker"
  curl -fsSL https://get.docker.com | sh
  systemctl enable --now docker
else
  LOG "Docker already installed, skipping"
fi

LOG "Adding pi to docker group"
usermod -aG docker pi || true

# ----- /srv folder layout -----

LOG "Creating /srv folder layout"
mkdir -p \
  /srv/samba/backup \
  /srv/samba/MyVault \
  /srv/homeassistant/config \
  /srv/vaultwarden \
  /srv/qbittorrent/appdata \
  /srv/downloads \
  /srv/syncthing \
  /srv/glance/assets \
  /srv/caddy/config \
  /srv/caddy/data

chown -R pi:pi /srv

# ----- .env template -----

ENV_FILE="/home-server/.env"

if [ -f "$ENV_FILE" ]; then
  LOG ".env already exists, skipping"
else
  LOG "Creating .env template"
  ZIG_PATH="$(ls -1 /dev/serial/by-id/* 2>/dev/null | head -n1 || true)"

  cat > "$ENV_FILE" << ENVEOF
ZIGBEE_DEVICE=$ZIG_PATH
RESTIC_PASSWORD=
ENVEOF

  chown pi:pi "$ENV_FILE"
  chmod 600 "$ENV_FILE"
  LOG ".env template created at $ENV_FILE"
fi

# ----- Symlinks + services -----

LOG "Running sync"
/home-server/scripts/sync.sh

LOG "Enabling samba"
systemctl enable --now smbd

systemctl daemon-reload

# ----- Done -----

LOG "Init complete"
echo
echo "Next steps:"
echo "  1. Fill in /home-server/.env"
echo "  2. Start containers: metis up <service>"
WARN "Log out and back in (or reboot) for pi to use Docker without sudo"
