#!/bin/sh
[ "$(id -u)" -ne 0 ] && exec sudo "$0" "$@"

ln -sf /home-server/config/etc/samba/smb.conf /etc/samba/smb.conf
ln -sf /home-server/config/etc/systemd/system/homebot.service /etc/systemd/system/homebot.service
ln -sf /home-server/config/etc/systemd/system/kodi.service /etc/systemd/system/kodi.service
ln -sf /home-server/scripts/metis /usr/local/bin/metis
ln -sf /home-server/config/etc/cron.d/home-server /etc/cron.d/home-server
