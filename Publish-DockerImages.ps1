$ErrorActionPreference = "Stop"

Set-Location $PSScriptRoot\WebAPI

"Building WebAPI image"
docker buildx build --platform linux/arm64 -t homeserver-webapi:arm --load .

"Tag WebAPI image"
docker tag homeserver-webapi:arm 192.168.1.21:5000/homeserver-webapi:latest

"Push WebAPI image"
docker push 192.168.1.21:5000/homeserver-webapi:latest

Set-Location $PSScriptRoot\webpage

"Building webpage image"
docker buildx build --platform linux/arm64 -t homeserver-webpage:arm --load .

"Tag webpage image"
docker tag homeserver-webpage:arm 192.168.1.21:5000/homeserver-webpage:latest

"Push webpage image"
docker push 192.168.1.21:5000/homeserver-webpage:latest

Set-Location $PSScriptRoot

"Transfer docker-compose.yml to neph-pi"
scp docker-compose.yml neph-pi:~/dev/

"Transfer WebAPI secrets to neph-pi"
scp $env:APPDATA\Microsoft\UserSecrets\33c19101-aa4e-4952-8538-07c141d452e8\secrets.json neph-pi:~/dev

"Execute docker compose"
ssh neph-pi "cd ~/dev/ && docker compose pull"
ssh neph-pi "cd ~/dev/ && docker compose up -d"