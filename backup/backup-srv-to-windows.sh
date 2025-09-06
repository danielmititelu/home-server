#!/usr/bin/env bash
set -euo pipefail

# Load sensitive variables from a separate file if it exists
if [[ -f "./backup-srv-to-windows.env" ]]; then
    source "./backup-srv-to-windows.env"
else
    echo "Environment file ./backup-srv-to-windows.env not found. Please create it with WIN_USER and WIN_HOST."
    exit 1
fi

DEST_DIR="/cygdrive/e/srv-backup"
SRC_DIR="/srv"

# Rsync options
# -a archive mode
# -v verbose
# -z compress
# --delete delete files in dest that are not in src
# --partial keep partially transferred files
# --numeric-ids use numeric user and group IDs

RSYNC_OPTIONS="-az --delete --partial --numeric-ids"

EXCLUDES=(--exclude="/downloads")

echo "Starting backup from $SRC_DIR to $DEST_DIR"

rsync $RSYNC_OPTIONS "${EXCLUDES[@]}" "$SRC_DIR/" "$WIN_USER@$WIN_HOST:$DEST_DIR"

echo "Backup completed successfully."
