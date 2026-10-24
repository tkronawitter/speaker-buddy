# Publishes speaker-buddy.exe as one self-contained file for Windows x64, so the presenting laptop
# needs no .NET runtime. No trimming: PowerPoint is read through late-bound COM, which trimming breaks.
# Compression roughly halves the file at the cost of unpacking into memory at every start.
# Usage: pwsh tools/publish-laptop.ps1 [-Output <folder>]
param(
    [string] $Output = (Join-Path $PSScriptRoot '..\laptop\publish')
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot '..\laptop\src\SpeakerBuddy.App\SpeakerBuddy.App.csproj'
dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $Output `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$exe = Get-Item (Join-Path $Output 'speaker-buddy.exe')
'{0} ({1:N1} MB)' -f $exe.FullName, ($exe.Length / 1MB)
