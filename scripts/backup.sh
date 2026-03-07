#!/bin/sh
set -e

[ "$(id -u)" -ne 0 ] && exec sudo "$0" "$@"

. /home-server/.env
export RESTIC_PASSWORD

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
    restic -r "$REPO" backup "$SOURCE" --exclude "$EXCLUDE"
  else
    restic -r "$REPO" backup "$SOURCE"
  fi

  echo "==> Pruning $REPO"
  restic -r "$REPO" forget --prune \
    --keep-daily 7 \
    --keep-weekly 4 \
    --keep-monthly 12
}

run_backup \
  /srv/samba/backup/samba \
  /srv/samba \
  /srv/samba/backup

run_backup \
  /srv/samba/backup/homeassistant \
  /srv/homeassistant/config \
  ""

run_backup \
  /srv/samba/backup/vaultwarden \
  /srv/vaultwarden \
  ""

echo "==> Backup complete"
chown -R pi:pi /srv/samba/backup
