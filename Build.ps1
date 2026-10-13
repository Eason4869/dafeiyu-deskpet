param([ValidateSet('App','CoreTests','ApiTests','ProfileTests','ConversationTests','ChatSmoke','AudioSmoke','UiSmoke','LiveTests','UpdateTests','StartupTests','ReleaseUiSmoke','ProxyTests')][string]$Target = 'App')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$frameworkRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $frameworkRoot 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework compiler is required.' }
$speechAssembly = Get-ChildItem -LiteralPath (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Speech') -Filter 'System.Speech.dll' -Recurse | Select-Object -First 1 -ExpandProperty FullName
if (!$speechAssembly) { throw 'Windows System.Speech assembly was not found.' }
$referenceFiles = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Net.Http.dll','System.Runtime.Serialization.dll','System.Security.dll','System.Web.Extensions.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
$references = @($referenceFiles | ForEach-Object { '/reference:' + (Join-Path $frameworkRoot $_) }) + @('/reference:' + $speechAssembly)
$workRoot = Join-Path $projectRoot 'work'
New-Item -ItemType Directory -Path $workRoot -Force | Out-Null
if ($Target -eq 'App') {
    $outputRoot = Join-Path (Split-Path $projectRoot -Parent) 'outputs\DafeiyuDeskPet'
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    $destination = Join-Path $outputRoot '桌面伙伴.exe'
    $sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName)
    $options = @('/target:winexe','/main:Dafeiyu.Program',('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')),('/win32icon:' + (Join-Path $projectRoot 'assets\pet.ico')))
} else {
    $destination = Join-Path $workRoot ($Target + '.exe')
    if ($Target -eq 'CoreTests') { $sourceNames = @('PetCore.cs','Settings.cs','ReleaseInfo.cs','ProxyConfiguration.cs') }
    elseif ($Target -eq 'ApiTests') { $sourceNames = @('PetCore.cs','Settings.cs','ReleaseInfo.cs','ProxyConfiguration.cs','ApiClient.cs','AudioRecorder.cs') }
    elseif ($Target -eq 'ConversationTests') { $sourceNames = @('PetCore.cs','Settings.cs','ReleaseInfo.cs','ProxyConfiguration.cs','ApiClient.cs','AudioRecorder.cs','SpeechOutput.cs','ConversationSession.cs') }
    else { $sourceNames = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | Select-Object -ExpandProperty Name) }
    $sources = @($sourceNames | ForEach-Object { Join-Path $projectRoot ('src\' + $_) } | Where-Object { Test-Path -LiteralPath $_ }) + @((Join-Path $projectRoot ('tests\' + $Target + '.cs')))
    if ($Target -eq 'UiSmoke' -or $Target -eq 'ChatSmoke' -or $Target -eq 'AudioSmoke' -or $Target -eq 'LiveTests' -or $Target -eq 'UpdateTests' -or $Target -eq 'ProxyTests') { $sources += Join-Path $projectRoot 'tests\ApiTests.cs' }
    if ($Target -eq 'ChatSmoke') { $sources += Join-Path $projectRoot 'tests\LiveTests.cs' }
    $options = @('/target:exe',('/main:Dafeiyu.Tests.' + $Target))
}
$assets = @()
foreach ($asset in @(@('spritesheet.png','Dafeiyu.Atlas'),@('pet.ico','Dafeiyu.Icon'),@('pet-preview-hd.png','Dafeiyu.Preview'),@('e-bao-spritesheet.png','Dafeiyu.EbaoAtlas'),@('e-bao-pet-preview-hd.png','Dafeiyu.EbaoPreview'))) {
    $assetPath = Join-Path $projectRoot ('assets\' + $asset[0])
    if (Test-Path -LiteralPath $assetPath) { $assets += '/resource:' + $assetPath + ',' + $asset[1] }
}
$arguments = @('/nologo','/optimize+','/codepage:65001','/utf8output','/platform:anycpu',('/out:' + $destination)) + $references + $options + $assets + $sources
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw "Compilation failed ($Target)." }
Copy-Item -LiteralPath (Join-Path $projectRoot 'app.config') -Destination ($destination + '.config') -Force
if ($Target -ne 'App') {
    & $destination
    if ($LASTEXITCODE -ne 0) { throw "Tests failed ($Target)." }
} else { Write-Output $destination }
