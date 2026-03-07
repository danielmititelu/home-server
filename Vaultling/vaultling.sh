#!/bin/bash
# run command: `crontab -e` and add the following line to schedule the script to run daily at 3 AM:
# 0 3 * * * /home-server/Vaultling/vaultling.sh >> /home-server/Vaultling/vaultling.log 2>&1

cd /home-server/Vaultling/Vaultling/bin/Release/net10.0/publish
/home/pi/.dotnet/dotnet Vaultling.dll
