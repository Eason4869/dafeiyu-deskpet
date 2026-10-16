param([string]$Compiler = '', [string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$outputRoot = Join-Path (Split-Path $projectRoot -Parent) 'outputs'
if ($OutputDirectory) { $outputRoot = [IO.Path]::GetFullPath($OutputDirectory) }
$runtimeRoot = Join-Path $outputRoot 'DafeiyuDeskPet'
$releaseSource = Get-Content -LiteralPath (Join-Path $projectRoot 'src\ReleaseInfo.cs') -Raw -Encoding UTF8
$productVersion = [regex]::Match($releaseSource, 'DisplayVersion="([^"]+)"').Groups[1].Value
$assemblyVersion = [regex]::Match($releaseSource, 'AssemblyVersion="([^"]+)"').Groups[1].Value
$repositoryUrl = [regex]::Match($releaseSource, 'RepositoryUrl="([^"]+)"').Groups[1].Value
if (!$productVersion -or !$assemblyVersion -or !$repositoryUrl) { throw 'ReleaseInfo metadata is incomplete.' }
$installerBaseName = $(if ([version]$productVersion -ge [version]'0.6.0') { 'DeskPet-Windows-Setup-' } else { 'DeskPet-Setup-' }) + $productVersion
$runtimeVersion = [System.Reflection.AssemblyName]::GetAssemblyName((Join-Path $runtimeRoot '桌面伙伴.exe')).Version.ToString()
if ($runtimeVersion -ne $assemblyVersion) { throw 'Rebuild the application before creating the installer.' }
if (!$Compiler) { $Compiler = $env:INNO_SETUP_COMPILER }
if (!$Compiler) {
    $compilerCandidates = @((Join-Path $projectRoot 'work\tools\inno\ISCC.exe'),(Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),(Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'))
    $Compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$Compiler -or !(Test-Path -LiteralPath $Compiler)) { throw 'Install Inno Setup 6.7+ and pass -Compiler <ISCC.exe>.' }
$definitions = @('/Qp',('/DRuntimeDir='+$runtimeRoot),('/DOutputDir='+$outputRoot),('/DProductVersion='+$productVersion),('/DAssemblyVersion='+$assemblyVersion),('/DRepositoryUrl='+$repositoryUrl),('/DInstallerBaseName='+$installerBaseName))
& $Compiler @definitions (Join-Path $projectRoot 'installer\DeskPet.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$installerPath = Join-Path $outputRoot ($installerBaseName+'.exe')
$manifestPath = Join-Path $outputRoot 'SHA256.txt'
$installerName = [System.IO.Path]::GetFileName($installerPath)
$manifestLines = @()
if (Test-Path -LiteralPath $manifestPath) {
    $manifestLines = @([System.IO.File]::ReadAllLines($manifestPath) | Where-Object { !$_.EndsWith('  '+$installerName,[System.StringComparison]::Ordinal) })
}
$installerStream = [System.IO.File]::OpenRead($installerPath)
$installerHasher = [System.Security.Cryptography.SHA256]::Create()
try { $installerDigest = [System.BitConverter]::ToString($installerHasher.ComputeHash($installerStream)).Replace('-','').ToLowerInvariant() }
finally { $installerStream.Dispose(); $installerHasher.Dispose() }
$manifestLines += $installerDigest+'  '+$installerName
[System.IO.File]::WriteAllLines($manifestPath,$manifestLines,[System.Text.UTF8Encoding]::new($false))
Get-Item -LiteralPath $installerPath | Select-Object FullName,Length
