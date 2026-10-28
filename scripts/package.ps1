[CmdletBinding()]
param([string]$InnoCompiler)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release
& (Join-Path $PSScriptRoot 'test.ps1')
$version=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $projectRoot 'build/Release/BigPictureControle.exe')).ProductVersion
if([string]::IsNullOrWhiteSpace($version)){throw 'Versão do aplicativo ausente.'}
if(-not $InnoCompiler){
    $found=Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if($found){$InnoCompiler=$found.Source}else{
        $candidates=@((Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'),(Join-Path $env:ProgramFiles 'Inno Setup 6/ISCC.exe'))
        $InnoCompiler=$candidates|Where-Object{Test-Path -LiteralPath $_}|Select-Object -First 1
    }
}
if(-not $InnoCompiler -or -not(Test-Path -LiteralPath $InnoCompiler)){throw 'Informe o compilador Inno Setup 6: -InnoCompiler caminho/ISCC.exe'}
$dist=Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
& $InnoCompiler ('/DAppVersion='+$version) ('/DAppSourceDir='+(Join-Path $projectRoot 'build/Release')) ('/O'+$dist) (Join-Path $projectRoot 'installer/BigPictureControle.iss')
if($LASTEXITCODE -ne 0){throw 'Falha ao gerar o instalador.'}
$installer=Join-Path $dist ('BigPictureControle-Setup-'+$version+'.exe')
if(-not(Test-Path -LiteralPath $installer)){throw 'Instalador esperado não foi encontrado.'}
$portable=Join-Path $dist ('BigPictureControle-Portable-'+$version+'.zip')
Compress-Archive -LiteralPath @((Join-Path $projectRoot 'build/Release/BigPictureControle.exe'),(Join-Path $projectRoot 'README.md')) -DestinationPath $portable -Force
$source=Join-Path $dist ('BigPictureControle-Source-'+$version+'.zip')
$sourcePaths=@('.github','assets','docs','installer','scripts','src','tests','README.md','CHANGELOG.md','.gitignore','.gitattributes')|ForEach-Object{Join-Path $projectRoot $_}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if(Test-Path -LiteralPath $source){Remove-Item -LiteralPath $source}
$archive=[IO.Compression.ZipFile]::Open($source,[IO.Compression.ZipArchiveMode]::Create)
try{
    foreach($item in $sourcePaths){
        $files=if(Test-Path -LiteralPath $item -PathType Container){Get-ChildItem -LiteralPath $item -Recurse -File -Force}else{Get-Item -LiteralPath $item}
        foreach($file in $files){
            $entryName=$file.FullName.Substring($projectRoot.Length+1).Replace('\','/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$entryName,[IO.Compression.CompressionLevel]::Optimal)|Out-Null
        }
    }
}finally{$archive.Dispose()}
$hashes=@($installer,$portable,$source)|ForEach-Object{(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($_)}
[IO.File]::WriteAllLines((Join-Path $dist 'SHA256SUMS.txt'),$hashes,[Text.UTF8Encoding]::new($false))
Write-Output ('Distribuição pronta em '+$dist)
