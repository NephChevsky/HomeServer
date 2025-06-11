$ErrorActionPreference = "Stop"

Set-Location $PSScriptRoot\WebAPI

"Building WebAPI image"
docker buildx build --platform linux/arm64 -t homeserver-webapi:arm --load .

"Exporting WebAPI image to tar file"
if (-not(Test-Path "bin"))
{
    New-Item -ItemType Directory -Path "bin"
}
docker save homeserver-webapi:arm -o bin/homeserver-webapi.tar

"Transfer WebAPI tar file to neph-pi"
scp bin/homeserver-webapi.tar neph-pi:~/dev

"Transfer WebAPI secrets to neph-pi"
scp $env:APPDATA\Microsoft\UserSecrets\33c19101-aa4e-4952-8538-07c141d452e8\secrets.json neph-pi:~/dev

"Load WebAPI tar file in docker"
ssh neph-pi "docker load -i ~/dev/homeserver-webapi.tar"

"Remove old WebAPI image"
ssh neph-pi "docker rm -f homeserver-webapi"

"Run WebAPI image"
ssh neph-pi "docker run -d --name homeserver-webapi -p 8081:80 -v /home/neph/dev/secrets.json:/app/secrets.json homeserver-webapi:arm -v /home/neph/dev/logs:/app/logs"

"Check WebAPI status"
$status = ssh neph-pi "docker inspect -f '{{.State.Status}}' homeserver-webapi"
if ($status -ne "running") {
    throw "Container failed to start. Status: $status"
}

Set-Location $PSScriptRoot\webpage

"Building webpage image"
docker buildx build --platform linux/arm64 -t homeserver-webpage:arm --load .

"Exporting webpage image to tar file"
if (-not(Test-Path "bin"))
{
    New-Item -ItemType Directory -Path "bin"
}
docker save homeserver-webpage:arm -o bin/homeserver-webpage.tar

"Transfer webpage tar file to neph-pi"
scp bin/homeserver-webpage.tar neph-pi:~/dev

"Load webpage tar file in docker"
ssh neph-pi "docker load -i ~/dev/homeserver-webpage.tar"

"Remove old webpage image"
ssh neph-pi "docker rm -f homeserver-webpage"

"Run webpage image"
ssh neph-pi "docker run -d --name homeserver-webpage -p 8080:80 homeserver-webpage:arm"

"Check webpage status"
$status = ssh neph-pi "docker inspect -f '{{.State.Status}}' homeserver-webpage"
if ($status -ne "running") {
    throw "Container failed to start. Status: $status"
}