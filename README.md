# home-server
My home server

Init command:
```
curl -fsSL https://raw.githubusercontent.com/danielmititelu/home-server/refs/heads/main/init.sh | sudo bash
```

Commands:
```
cd /srv/compose
docker compose pull
docker compose up -d

docker compose logs homeassistant
docker exec -it homeassistant bash
```

Good to know:

### What is the username/password for qbittorrent web ui?

A temporary password is printed in the logs:
```
docker compose logs qbittorrent
```

### How to enable Kodi?

1. (optional instead of rsync) copy kodi.service to /etc/systemd/system/kodi.service
2. run these commands
```
sudo rsync -av /home-server/config/etc/ /etc/
sudo systemctl enable kodi
sudo systemctl start kodi
reboot
```
### How to run sql on postgresql?

```
docker exec -it postgresql psql -U postgres

# usefull commands:
\l -> list databases
\du -> list roles/users 
\dt -> list tables
\c DBNAME -> change current db
\q -> quit

create user immich with password 'immich_pw';
create database immich owner immich;
create extension if not exists vector;
```

### How to sync config files 

```
rsync -a /home-server/config/srv/ /srv/

```

### How to enable audio for steamlink
```
sudo apt install pulseaudio -y
pulseaudio --start
todo: try with pipewire
```

### Commands for a service running with systemd
```
sudo systemctl daemon-reload
sudo systemctl start home-bot.service
sudo systemctl stop home-bot.service
sudo systemctl enable homebot.service # Enable on boot
sudo systemctl disable homebot.service # Disable on boot
sudo systemctl restart home-bot.service
journalctl -u home-bot.service -f # view logs
```
