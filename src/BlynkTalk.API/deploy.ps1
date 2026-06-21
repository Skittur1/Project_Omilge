# deploy.ps1
$server = "root@138.252.100.148"
$remotePath = "/var/www/blynktalk-api"
$serviceName = "blynktalk-api"

Write-Host "Publishing..." -ForegroundColor Cyan
dotnet publish -c Release -o publish

Write-Host "Stopping remote service..." -ForegroundColor Cyan
ssh $server "sudo systemctl stop $serviceName"

Write-Host "Uploading files..." -ForegroundColor Cyan
scp -r .\publish\* "${server}:${remotePath}/"

Write-Host "Starting remote service..." -ForegroundColor Cyan
ssh $server "sudo systemctl start $serviceName"

Write-Host "Checking status..." -ForegroundColor Cyan
ssh $server "sudo systemctl status $serviceName --no-pager"

Write-Host "Deploy complete." -ForegroundColor Green