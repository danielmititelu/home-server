# home-server
My home server

## Setup (fresh Raspberry Pi OS)

1. Clone the repo:
```
git clone https://github.com/danielmititelu/home-server /home-server
```

2. Run init (installs Docker, samba, restic, creates /srv layout, symlinks config files):
```
metis init
```

3. Fill in `/home-server/.env`:
```
ZIGBEE_DEVICE=       # ls /dev/serial/by-id/ to find your dongle
RESTIC_PASSWORD=
```

4. Start containers:
```
metis up <service>
```

5. Log out and back in (or reboot) for `pi` to use Docker without sudo.

---

## metis

`metis` is the CLI tool for managing this server. Run `metis` with no arguments for usage.

```
metis up <service>        # Start a container (detached)
metis down <service>      # Stop a container
metis restart <service>   # Restart a container
metis logs <service>      # Follow container logs (Ctrl+C to stop)
metis status              # Show CPU usage, memory and temperature
metis sync                # Symlink config files into the system
metis backup              # Run restic backups (samba, homeassistant, vaultwarden)
metis vaultling           # Run Vaultling (Obsidian vault processor)
metis start kodi          # Start Kodi
metis stop kodi           # Stop Kodi
metis init                # Bootstrap a fresh Raspberry Pi
```

Service names match container names: `caddy`, `glance`, `homeassistant`, `vaultwarden`, `qbittorrent`, `syncthing`, `pihole`.

---

## Backups

Backups are managed with restic and stored in `/srv/samba/backup/` (accessible via the samba share):

```
backup/
  samba/           # restic repo for /srv/samba
  homeassistant/   # restic repo for /srv/homeassistant/config
  vaultwarden/     # restic repo for /srv/vaultwarden
```

Run manually with `metis backup`. Runs automatically every night at 2 AM via cron.
Retention: daily for 7 days, weekly for 4 weeks, monthly for 12 months.

---

## Good to know

### What is the username/password for qbittorrent web ui?

A temporary password is printed in the logs:
```
metis logs qbittorrent
```

### How to enable Kodi?

```
metis sync
sudo systemctl enable kodi
sudo systemctl start kodi
reboot
```

### Commands for a service running with systemd
```
sudo systemctl daemon-reload
sudo systemctl start home-bot.service
sudo systemctl stop home-bot.service
sudo systemctl enable home-bot.service
sudo systemctl disable home-bot.service
sudo systemctl restart home-bot.service
journalctl -u home-bot.service -f
```

### How to run sql on postgresql?

```
docker exec -it postgresql psql -U postgres

# useful commands:
\l   -> list databases
\du  -> list roles/users
\dt  -> list tables
\c DBNAME -> change current db
\q   -> quit

create user immich with password 'immich_pw';
create database immich owner immich;
create extension if not exists vector;
```

### How to give permission to a group
```
sudo chown -R :groupname /path/to/folder
sudo chmod -R g+rwx /path/to/folder
sudo chmod g+s /path/to/folder
sudo setfacl -R -m g:groupname:rwx /path/to/folder
sudo setfacl -d -m g:groupname:rwx /path/to/folder
sudo usermod -a -G groupname username
```

### How to enable audio for steamlink
```
sudo apt install pulseaudio -y
pulseaudio --start
```

