[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$UnityPath,
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$TestFilter,
    [Parameter(Mandatory = $true)][int]$MinimumTests,
    [int]$MaximumFailed = 0
)

$ErrorActionPreference = "Stop"
$UnityPath = (Resolve-Path $UnityPath).Path
$ProjectPath = (Resolve-Path $ProjectPath).Path
$Stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$LogDir = Join-Path $ProjectPath "Logs/ArchitectureMigration/$Stamp"
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed ($LASTEXITCODE): $Command $($Arguments -join ' ')"
    }
}

Push-Location $ProjectPath
try {
    $dirty = git status --porcelain
    if ($dirty) { throw "Working tree must be clean before beginning a slice gate.`n$dirty" }

    Invoke-Checked "python" @("Tools/architecture/guard.py", "--project-root", ".")
    Invoke-Checked "python" @("Tools/architecture/audit.py", "--project-root", ".", "--output", "Docs/Architecture/Generated")
    Invoke-Checked "python" @("Tools/architecture/serialized_refs.py", "--project-root", ".", "--output", "Docs/Architecture/Generated/serialized_script_refs.csv")
    Invoke-Checked "python" @("Tools/architecture/scan_unity_script_integrity.py", "--project-root", ".")
    Invoke-Checked "python" @("Tools/architecture/validate_runtime_ledger.py", "--project-root", ".")
    Invoke-Checked "git" @("diff", "--check")

    Invoke-Checked $UnityPath @(
        "-batchmode", "-nographics", "-quit", "-projectPath", $ProjectPath,
        "-logFile", (Join-Path $LogDir "compile.log")
    )

    $Result = Join-Path $LogDir "focused.xml"
    Invoke-Checked $UnityPath @(
        "-batchmode", "-nographics", "-projectPath", $ProjectPath,
        "-runTests", "-testPlatform", "EditMode", "-testFilter", $TestFilter,
        "-testResults", $Result, "-logFile", (Join-Path $LogDir "focused.log")
    )
    Invoke-Checked "python" @(
        "Tools/architecture/verify_nunit_xml.py", $Result,
        "--minimum-total", "$MinimumTests", "--maximum-failed", "$MaximumFailed"
    )

    Invoke-Checked "python" @("Tools/architecture/guard.py", "--project-root", ".")
    Invoke-Checked "python" @("Tools/architecture/scan_unity_script_integrity.py", "--project-root", ".")
    Invoke-Checked "git" @("diff", "--check")

    Write-Host "Slice gate passed. Evidence: $LogDir" -ForegroundColor Green
}
finally {
    Pop-Location
}
