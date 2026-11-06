param(
    [ValidateSet('x64','arm64','both')][string]$Architecture = 'both',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\dist')
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $output | Out-Null
Push-Location $projectRoot
try {
    & python -m unittest discover -s tests -p test_sdk_extract.py -v
    if ($LASTEXITCODE -ne 0) { throw 'SDK extraction regression tests failed.' }
    & dotnet run --project 'tests/NXInstaller.Tests' -c Release -- --sdk-fixture $projectRoot --python python --report (Join-Path $output 'core-tests.json')
    if ($LASTEXITCODE -ne 0) { throw 'Core checks failed.' }
    $targets = if ($Architecture -eq 'both') { @('x64','arm64') } else { @($Architecture) }
    $sums = @()
    foreach ($arch in $targets) {
        $publish = Join-Path $output ('publish-win-' + $arch)
        & dotnet publish 'src/NXInstaller.App/NXInstaller.App.csproj' -c Release -r ('win-' + $arch) -o $publish --nologo
        if ($LASTEXITCODE -ne 0) { throw "Publish failed for $arch." }
        $name = 'NXNativeWindowsEasyInstaller-v2.0.2-win-' + $arch + '.exe'
        $file = Join-Path $output $name
        Copy-Item (Join-Path $publish 'NXNativeWindowsEasyInstaller.exe') $file -Force
        $sums += ((Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $name)
    }
    [IO.File]::WriteAllLines((Join-Path $output 'SHA256SUMS.txt'), [string[]]$sums)
    Write-Host "Build complete: $output"
} finally { Pop-Location }
