#!/bin/sh
[ "$(id -u)" -ne 0 ] && exec sudo "$0" "$@"

ln -sf /home-server/config/etc/samba/smb.conf /etc/samba/smb.conf
ln -sf /home-server/config/etc/systemd/system/homebot.service /etc/systemd/system/homebot.service
ln -sf /home-server/config/etc/systemd/system/kodi.service /etc/systemd/system/kodi.service
ln -sf /home-server/scripts/metis /usr/local/bin/metis
rm -f /etc/cron.d/home-server
# copy the cron file instead of symlinking it because cron needs root
# ownership to read the file but in the repo it needs user ownership 
# in order to be editable without sudo
cp /home-server/config/etc/cron.d/home-server /etc/cron.d/home-server
chown root:root /etc/cron.d/home-server
chmod 644 /etc/cron.d/home-server
