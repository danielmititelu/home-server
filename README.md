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

docker compose logs nextcloud
docker exec -it nextcloud bash
```

Good to know:

What is the username/password for qbittorrent web ui?

A temporary password is printed in the logs:
```
docker compose logs qbittorrent
```
How to enable Kodi?
1. copy /kodi/kodi.service to /etc/systemd/system/kodi.service
2. run these commands
```
sudo systemctl enable kodi
sudo systemctl start kodi
reboot
```

docker exec -u www-data nextcloud php occ maintenance:mode --off