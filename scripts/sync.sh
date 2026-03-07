[ "$EUID" -ne 0 ] && exec sudo "$0" "$@"

cp /home-server/config/srv/caddy/Caddyfile /srv/caddy/Caddyfile
ln -sf /home-server/config/etc/samba/smb.conf /etc/samba/smb.conf
ln -sf /home-server/config/etc/systemd/system/home-bot.service /etc/systemd/system/home-bot.service
ln -sf /home-server/config/etc/systemd/system/kodi.service /etc/systemd/system/kodi.service
