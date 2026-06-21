[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$repositoryRoot = Split-Path -Parent $repoRoot
$quantLibRoot = Join-Path $repoRoot 'external\QuantLib'
$swigRoot = Join-Path $repoRoot 'external\QuantLib-SWIG'
$quantLibSolution = Join-Path $quantLibRoot 'QuantLib.sln'
$swigSolution = Join-Path $swigRoot 'CSharp\QuantLib.sln'
$nativeProject = Join-Path $swigRoot 'CSharp\cpp\QuantLibWrapper.vcxproj'
$managedProject = Join-Path $swigRoot 'CSharp\csharp\NQuantLib.csproj'
$wrapperSource = Join-Path $swigRoot 'CSharp\cpp\quantlib_wrap.cpp'
$nativeBuildDll = Join-Path $swigRoot 'CSharp\cpp\bin\x64\Release\NQuantLibc.dll'
$nativeDll = Join-Path $swigRoot 'CSharp\cpp\NQuantLibc.dll'
$quantLibLibrary = Join-Path $quantLibRoot 'lib\QuantLib-x64-mt.lib'

function Assert-Path {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw ($Description + ' is missing at ' + $Path + '. Run git submodule update --init --recursive from ' + $repositoryRoot + '.')
    }
}

function Assert-PinnedRepository {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $ExpectedCommit,

        [Parameter(Mandatory)]
        [string] $Name
    )

    $actualCommit = & git -C $Path rev-parse HEAD
    if ($LASTEXITCODE -ne 0) {
        throw ('Could not inspect the ' + $Name + ' submodule at ' + $Path + '.')
    }

    if ($actualCommit.Trim() -ne $ExpectedCommit) {
        throw ($Name + ' must be pinned to v1.42.1 (' + $ExpectedCommit + '), but HEAD is ' + $actualCommit.Trim() + '.')
    }
}

Assert-Path -Path $quantLibSolution -Description 'QuantLib submodule'
Assert-Path -Path $swigSolution -Description 'QuantLib-SWIG submodule'
Assert-Path -Path $managedProject -Description 'Official NQuantLib managed project'

if (-not $env:BOOST_ROOT) {
    throw 'BOOST_ROOT must point to a Boost source tree containing boost\config.hpp.'
}

$boostRoot = [IO.Path]::GetFullPath($env:BOOST_ROOT)
$boostConfig = Join-Path $boostRoot 'boost\config.hpp'
if (-not (Test-Path -LiteralPath $boostConfig)) {
    throw ('BOOST_ROOT must point to a Boost source tree containing boost\config.hpp. Received: ' + $boostRoot)
}

if (-not $env:SWIG_EXE) {
    throw 'SWIG_EXE must point to swig.exe.'
}

$swigExe = [IO.Path]::GetFullPath($env:SWIG_EXE)
if (-not (Test-Path -LiteralPath $swigExe)) {
    throw ('SWIG_EXE must point to swig.exe. Received: ' + $swigExe)
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git is required to verify the pinned native dependencies.'
}

Assert-PinnedRepository -Path $quantLibRoot -ExpectedCommit '099987f0ca2c11c505dc4348cdb9ce01a598e1e5' -Name 'QuantLib'
Assert-PinnedRepository -Path $swigRoot -ExpectedCommit '34e9247f3b6008725517cd5359d9fbe64e52aa21' -Name 'QuantLib-SWIG'

if (-not ${env:ProgramFiles(x86)}) {
    throw 'ProgramFiles(x86) is not available; this build requires 64-bit Windows.'
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw 'vswhere was not found. Install Visual Studio 2022 with the Desktop development with C++ workload.'
}

$visualStudioRoot = & $vswhere -latest -products '*' -version '[17.0,18.0)' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if ($LASTEXITCODE -ne 0 -or -not $visualStudioRoot) {
    throw 'Visual Studio 2022 with the MSVC x64/x86 build tools was not found. Install the Desktop development with C++ workload.'
}

$msbuild = Join-Path $visualStudioRoot.Trim() 'MSBuild\Current\Bin\MSBuild.exe'
Assert-Path -Path $msbuild -Description 'Visual Studio 2022 MSBuild'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET 8 SDK is required but dotnet was not found.'
}

$dotnetVersion = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or -not $dotnetVersion.StartsWith('8.')) {
    throw ('The repository requires a .NET 8 SDK; dotnet selected ' + $dotnetVersion + '.')
}

$previousCl = $env:CL
try {
    $env:CL = ('/I"' + $boostRoot + '" /wd4996 ' + $previousCl).Trim()

    Write-Host 'Building official QuantLib v1.42.1 (Release|x64)...'
    & $msbuild $quantLibSolution /m /nologo /p:Configuration=Release /p:Platform=x64
    if ($LASTEXITCODE -ne 0) {
        throw 'QuantLib x64 build failed.'
    }
    Assert-Path -Path $quantLibLibrary -Description 'QuantLib Release x64 library'

    Write-Host 'Generating official QuantLib-SWIG C# bindings...'
    Push-Location (Join-Path $swigRoot 'CSharp')
    try {
        & $swigExe -csharp -c++ -outdir csharp -namespace QuantLib -o cpp\quantlib_wrap.cpp ..\SWIG\quantlib.i
        if ($LASTEXITCODE -ne 0) {
            throw 'QuantLib-SWIG source generation failed.'
        }
    }
    finally {
        Pop-Location
    }
    Assert-Path -Path $wrapperSource -Description 'Generated QuantLib-SWIG wrapper source'

    $env:QL_DIR = $quantLibRoot
    Write-Host 'Building official QuantLib-SWIG v1.42.1 wrapper (Release|x64)...'
    & $msbuild $nativeProject /m /nologo /p:Configuration=Release /p:Platform=x64
    if ($LASTEXITCODE -ne 0) {
        throw 'QuantLib-SWIG x64 build failed.'
    }
    Assert-Path -Path $nativeBuildDll -Description 'Official QuantLib-SWIG Release x64 build output'
    Assert-Path -Path $nativeDll -Description 'Official QuantLib-SWIG x64 post-build copy'

    Write-Host 'Building official NQuantLib managed binding (Release|net8.0)...'
    & dotnet build $managedProject -c Release -f net8.0 --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'NQuantLib managed build failed.'
    }

    Write-Host ('Native build complete: ' + $nativeDll)
}
finally {
    $env:CL = $previousCl
}
