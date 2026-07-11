[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$UnityPath,

    [string]$ProjectPath = (Get-Location).Path,

    [switch]$SkipPlayMode
)

$ErrorActionPreference = "Stop"
$ProjectPath = (Resolve-Path $ProjectPath).Path
$UnityPath = (Resolve-Path $UnityPath).Path
$LogDir = Join-Path $ProjectPath "Logs/ArchitectureValidation"
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $Command $($Arguments -join ' ')"
    }
}

function Invoke-UnityTests {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("EditMode", "PlayMode")]
        [string]$Platform,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    Invoke-Checked -Command $UnityPath -Arguments @(
        "-batchmode",
        "-nographics",
        "-projectPath", $ProjectPath,
        "-runTests",
        "-testPlatform", $Platform,
        "-testResults", (Join-Path $LogDir "$Label.xml"),
        "-logFile", (Join-Path $LogDir "$Label.log")
    )
}

Push-Location $ProjectPath
try {
    Invoke-Checked -Command "python" -Arguments @("Tools/architecture/guard.py", "--project-root", ".")
    Invoke-Checked -Command "python" -Arguments @("Tools/architecture/audit.py", "--project-root", ".", "--output", "Docs/Architecture/Generated")
    Invoke-Checked -Command "python" -Arguments @("Tools/architecture/serialized_refs.py", "--project-root", ".", "--output", "Docs/Architecture/Generated/serialized_script_refs.csv")

    Invoke-UnityTests -Platform "EditMode" -Label "editmode-before-root"

    Invoke-Checked -Command $UnityPath -Arguments @(
        "-batchmode",
        "-nographics",
        "-quit",
        "-projectPath", $ProjectPath,
        "-executeMethod", "Chateau.Editor.Architecture.GameRootInstaller.InstallGameplaySceneBatch",
        "-logFile", (Join-Path $LogDir "game-root-install.log")
    )

    Invoke-UnityTests -Platform "EditMode" -Label "editmode-after-root"

    if (-not $SkipPlayMode) {
        Invoke-UnityTests -Platform "PlayMode" -Label "playmode-after-root"
    }

    Invoke-Checked -Command "python" -Arguments @("Tools/architecture/guard.py", "--project-root", ".")

    Write-Host ""
    Write-Host "Foundation gate completed." -ForegroundColor Green
    Write-Host "Review before committing:"
    Write-Host "  git status --short"
    Write-Host "  git diff -- Assets/Scenes/Gameplay.unity Assets/_Chateau/Data"
    Write-Host "Evidence: $LogDir"
}
finally {
    Pop-Location
}
