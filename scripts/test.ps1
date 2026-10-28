[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if(-not(Test-Path -LiteralPath $compiler)){throw 'Compilador .NET Framework x64 não encontrado.'}
$testOutput=Join-Path $projectRoot 'build/tests'
New-Item -ItemType Directory -Path $testOutput -Force | Out-Null
$cases=@(
    @{Name='AudioSessionTests';Sources=@('tests/AudioSessionTests.cs','src/AudioControl.cs','src/AudioSession.cs')},
    @{Name='TaskbarSessionTests';Sources=@('tests/TaskbarSessionTests.cs','src/TaskbarNative.cs','src/TaskbarSession.cs','src/DisplayControl.cs')}
)
foreach($case in $cases){
    $path=Join-Path $testOutput ($case.Name+'.exe')
    $arguments=@('/nologo','/target:exe','/platform:x64','/codepage:65001','/r:System.Core.dll',('/out:'+$path))
    $arguments+=@($case.Sources|ForEach-Object{Join-Path $projectRoot $_})
    & $compiler @arguments
    if($LASTEXITCODE -ne 0){throw ('Falha ao compilar '+$case.Name)}
    $previousTestRoot=$env:BPC_TEST_DATA_ROOT
    try {
        $env:BPC_TEST_DATA_ROOT=Join-Path $testOutput 'data'
        & $path
        if($LASTEXITCODE -ne 0){throw ('Falha em '+$case.Name)}
    }finally{$env:BPC_TEST_DATA_ROOT=$previousTestRoot}
}
$migration=Join-Path $testOutput 'SettingsMigrationTests.exe'
$migrationSources=@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -Recurse -File|ForEach-Object FullName)
$migrationArguments=@('/nologo','/target:exe','/platform:x64','/codepage:65001','/main:SettingsMigrationTests','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll',('/out:'+$migration),(Join-Path $projectRoot 'tests/SettingsMigrationTests.cs'))
& $compiler @migrationArguments @migrationSources
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar testes de migração.'}
& $migration
if($LASTEXITCODE -ne 0){throw 'Falha nos testes de migração.'}
Write-Output 'Verificações concluídas sem alterar monitores, áudio ou barra de tarefas do Windows.'
