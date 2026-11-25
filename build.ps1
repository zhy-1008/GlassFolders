$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'src'
$output = Join-Path $PSScriptRoot 'build'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The 64-bit Windows .NET Framework C# compiler is required.'
}

[IO.Directory]::CreateDirectory($output) | Out-Null
$icon = Join-Path $source 'app.ico'
$manifest = Join-Path $source 'app.manifest'
$app = Join-Path $output 'GlassFolders.exe'
$installer = Join-Path $output 'GlassFolders-Setup.exe'
$appReferences = @(
    'System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll',
    'System.Runtime.Serialization.dll', 'System.Xaml.dll', 'WPF\WindowsBase.dll',
    'WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll'
) | ForEach-Object { "/reference:$(Join-Path $framework $_)" }
$installerReferences = @(
    'System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll',
    'Microsoft.CSharp.dll'
) | ForEach-Object { "/reference:$(Join-Path $framework $_)" }
$appSources = @('Native.cs', 'Model.cs', 'App.cs', 'SelfTest.cs', 'VersionInfo.cs') |
    ForEach-Object { Join-Path $source $_ }
$installerSources = @('Installer.cs', 'VersionInfo.cs') |
    ForEach-Object { Join-Path $source $_ }
$common = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/utf8output', '/codepage:65001')
$common += @("/win32icon:$icon", "/win32manifest:$manifest")

& $compiler @common "/out:$app" @appReferences "/resource:$icon,AppIcon" @appSources
if ($LASTEXITCODE -ne 0) { throw 'GlassFolders application build failed.' }

& $compiler @common "/out:$installer" @installerReferences "/resource:$app,GlassFolders.exe" @installerSources
if ($LASTEXITCODE -ne 0) { throw 'GlassFolders installer build failed.' }
Write-Output "Built app and installer in $output"
