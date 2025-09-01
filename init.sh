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

LOG "Updating system packages"
apt-get update -y
DEBIAN_FRONTEND=noninteractive apt-get full-upgrade -y

# Optional but avoids serial device conflicts with Zigbee sticks
if systemctl list-unit-files | grep -q ModemManager.service; then
  LOG "Disabling ModemManager (to avoid grabbing USB radios)"
  systemctl disable --now ModemManager || true
fi

if ! command -v docker >/dev/null 2>&1; then
  LOG "Installing Docker"
  curl -fsSL https://get.docker.com | sh
  systemctl enable --now docker
else
  LOG "Docker already installed, skipping"
fi

LOG "Adding user '$TARGET_USER' to docker group"
usermod -aG docker "$TARGET_USER" || true

# ----- Folder layout -----
LOG "Creating /srv folder layout"
mkdir -p \
  /srv/compose \
  /srv/homeassistant/config \
  /srv/nextcloud/{app,data,db,redis} \
  /srv/esphome \
  /srv/glance

chown -R "$TARGET_USER:$TARGET_USER" /srv

# ----- Zigbee dongle detection + .env secrets -----

LOG "Configuring .env with Zigbee dongle and DB secrets"

ENV_FILE="/srv/compose/.env"

touch "$ENV_FILE"

if grep -q '^ZIGBEE_DEVICE=' "$ENV_FILE"; then
  LOG "ZIGBEE_DEVICE already exists in $ENV_FILE, skipping"
else
  # 1) Detect first USB serial device
  ZIG_PATH="$(ls -1 /dev/serial/by-id/* 2>/dev/null | head -n1 || true)"
  if [[ -n "$ZIG_PATH" ]]; then
    echo "ZIGBEE_DEVICE=$ZIG_PATH" >> "$ENV_FILE"
    LOG "ZIGBEE_DEVICE added -> $ZIG_PATH"
  else
    WARN "No Zigbee dongle found. You can set ZIGBEE_DEVICE in $ENV_FILE later."
  fi
fi

# 2) Generate random DB passwords (only if not already present)
if ! grep -q '^MYSQL_PASSWORD=' "$ENV_FILE"; then
  MYSQL_PASSWORD=$(openssl rand -base64 18)
  echo "MYSQL_PASSWORD=$MYSQL_PASSWORD" >> "$ENV_FILE"
  LOG "Generated MYSQL_PASSWORD"
fi

if ! grep -q '^MYSQL_ROOT_PASSWORD=' "$ENV_FILE"; then
  MYSQL_ROOT_PASSWORD=$(openssl rand -base64 24)
  echo "MYSQL_ROOT_PASSWORD=$MYSQL_ROOT_PASSWORD" >> "$ENV_FILE"
  LOG "Generated MYSQL_ROOT_PASSWORD"
fi

# 3) Default ports (only add if not already present)
grep -q '^NEXTCLOUD_PORT=' "$ENV_FILE" || echo "NEXTCLOUD_PORT=8080" >> "$ENV_FILE"
grep -q '^GLANCE_PORT=' "$ENV_FILE"    || echo "GLANCE_PORT=8090" >> "$ENV_FILE"

chown "$TARGET_USER:$TARGET_USER" "$ENV_FILE"
chmod 600 "$ENV_FILE"

LOG ".env written at $ENV_FILE"


# ----- Download (always overwrite) config files from GitHub -----

RAW_COMPOSE="https://raw.githubusercontent.com/danielmititelu/home-server/refs/heads/main/compose/docker-compose.yaml"
RAW_GLANCE="https://raw.githubusercontent.com/danielmititelu/home-server/refs/heads/main/glance/glance.yaml"

DEST_COMPOSE="/srv/compose/docker-compose.yaml"
DEST_GLANCE="/srv/glance/glance.yaml"

download_overwrite() {
  local url="$1" dest="$2"
  local tmp
  tmp="$(mktemp)" || ERR "mktemp failed"

  LOG "Fetching $(basename "$dest") from $url"
  # -f: fail on HTTP errors, -S: show errors, -L: follow redirects, -s: silent
  curl -fSLS "$url" -o "$tmp" || ERR "Failed to download $url"

  # Atomic replace to avoid partial files on failure
  mv "$tmp" "$dest" || ERR "Failed to move temp file to $dest"
  chown "$TARGET_USER:$TARGET_USER" "$dest"
  chmod 644 "$dest"
}

download_overwrite "$RAW_COMPOSE" "$DEST_COMPOSE"
download_overwrite "$RAW_GLANCE"  "$DEST_GLANCE"

LOG "Configs updated:
  - $DEST_COMPOSE
  - $DEST_GLANCE"

# Helpful notice
LOG "All set."
echo
echo "Next steps (as $TARGET_USER):"
echo "  cd /srv/compose"
echo "  docker compose pull"
echo "  docker compose up -d"
echo
WARN "You must log out/in (or reboot) for '$TARGET_USER' to use Docker without sudo."