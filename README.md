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
metis start <service>
```

5. Log out and back in (or reboot) for `pi` to use Docker without sudo.

---

## metis

`metis` is the CLI tool for managing this server. Run `metis` with no arguments for usage.

```
metis start <container|service>   # Start a container or systemd service
metis stop <container|service>    # Stop a container or systemd service
metis restart <container|service> # Restart a container or systemd service
metis logs <container|service>    # Follow logs (Ctrl+C to stop)
metis status              # Show CPU usage, memory and temperature
metis sync                # Symlink config files into the system
metis backup              # Run restic backups (samba, homeassistant, vaultwarden)
metis vaultling           # Run Vaultling (Obsidian vault processor)
metis init                # Bootstrap a fresh Raspberry Pi
```

Service names for containers: `caddy`, `glance`, `homeassistant`, `vaultwarden`, `qbittorrent`, `syncthing`, `pihole`.
Systemd services: `kodi`.

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
metis start kodi
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

