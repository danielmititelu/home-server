#!/bin/sh
set -e

[ "$(id -u)" -ne 0 ] && exec sudo "$0" "$@"

. /home-server/.env
export RESTIC_PASSWORD

BACKUP_ROOT=/mnt/backup

if ! mountpoint -q "$BACKUP_ROOT"; then
  echo "ERROR: $BACKUP_ROOT is not mounted, aborting" >&2
  exit 1
fi

trap 'chown -R pi:pi "$BACKUP_ROOT"' EXIT

run_backup() {
  REPO="$1"
  SOURCE="$2"
  EXCLUDE="$3"

  if ! restic -r "$REPO" snapshots > /dev/null 2>&1; then
    echo "==> Initializing new repo at $REPO"
    restic -r "$REPO" init
  fi

  echo "==> Backing up $SOURCE to $REPO"
  if [ -n "$EXCLUDE" ]; then
    restic -r "$REPO" backup $SOURCE --exclude "$EXCLUDE"
  else
    restic -r "$REPO" backup $SOURCE
  fi

  echo "==> Pruning $REPO"
  restic -r "$REPO" forget --prune \
    --keep-daily 7 \
    --keep-weekly 4 \
    --keep-monthly 12
}

run_backup \
  "$BACKUP_ROOT/samba" \
  /srv/samba \
  ""

run_backup \
  "$BACKUP_ROOT/homeassistant" \
  "/srv/homeassistant/config /home-server/.env" \
  ""

run_backup \
  "$BACKUP_ROOT/vaultwarden" \
  /srv/vaultwarden \
  ""

echo "==> Backup complete"
