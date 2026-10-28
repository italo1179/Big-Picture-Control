[CmdletBinding()]
param([ValidateSet('Release','Debug')][string]$Configuration='Release')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if(-not (Test-Path -LiteralPath $compiler)){throw 'Compilador .NET Framework x64 não encontrado. Execute no Windows 10/11 de 64 bits.'}
$output=Join-Path $projectRoot ('build/'+$Configuration)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$sources=@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -Recurse -File | Sort-Object FullName | ForEach-Object FullName)
$arguments=@('/nologo','/target:winexe','/platform:x64','/codepage:65001','/warn:4','/warnaserror+',('/out:'+(Join-Path $output 'BigPictureControle.exe')),('/win32manifest:'+(Join-Path $projectRoot 'assets/app.manifest')),('/win32icon:'+(Join-Path $projectRoot 'assets/app.ico')),'/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Core.dll')
if($Configuration -eq 'Release'){$arguments+='/optimize+'}else{$arguments+=@('/debug+','/optimize-')}
& $compiler @arguments @sources
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar o aplicativo.'}
Write-Output ('Aplicativo compilado: '+(Join-Path $output 'BigPictureControle.exe'))
