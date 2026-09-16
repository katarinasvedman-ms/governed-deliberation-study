param(
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"
Push-Location (Join-Path $PSScriptRoot "..")
try {
    $runId = [Guid]::NewGuid().ToString("N")
    $results = Join-Path (Get-Location) ".artifacts\t0-feasibility\$runId"
    New-Item -ItemType Directory -Path $results | Out-Null
    $revision = git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw "Cannot determine the source revision." }
    $status = git status --short
    if ($LASTEXITCODE -ne 0) { throw "Cannot determine the worktree status." }
    $sdk = dotnet --version
    if ($LASTEXITCODE -ne 0) { throw "The .NET SDK is unavailable." }
    @(
        "specification=EXPERIMENT_SPEC.md v1.0"
        "scope=T0 only; scripted; no model client"
        "sourceRevision=$revision"
        "worktreeStatus=$($status -join '; ')"
        "sdk=$sdk"
        "workflowPackage=Microsoft.Agents.AI.Workflows 1.17.0"
        "clock=explicit probe stages; not experimental ticks or framework supersteps"
    ) | Set-Content (Join-Path $results "provenance.txt")
    $evidenceSources = @(
        ".\Directory.Packages.props"
        ".\Directory.Build.props"
        ".\global.json"
        ".\docs\EXPERIMENT_SPEC.md"
        ".\docs\COPILOT_HANDOFF.md"
        ".\scripts\test-t0-feasibility.ps1"
        ".\tests\GovernedAgent.IntegrationTests\GovernedAgent.IntegrationTests.csproj"
        ".\tests\GovernedAgent.IntegrationTests\T0\FrameworkProbe.cs"
        ".\tests\GovernedAgent.IntegrationTests\T0\GovernedProbeOwner.cs"
        ".\tests\GovernedAgent.IntegrationTests\T0\FrameworkFeasibilityTests.cs"
    )
    $evidenceSources | ForEach-Object {
        "$((Get-FileHash $_ -Algorithm SHA256).Hash)  $_"
    } | Set-Content (Join-Path $results "source-sha256.txt")

    $testArguments = @(
        "test", ".\tests\GovernedAgent.IntegrationTests\GovernedAgent.IntegrationTests.csproj",
        "--configuration", "Release",
        "--filter", "FullyQualifiedName~GovernedAgent.IntegrationTests.T0.FrameworkFeasibilityTests",
        "--logger", "console;verbosity=detailed",
        "--logger", "trx;LogFileName=t0.trx",
        "--results-directory", $results,
        "--blame-hang-timeout", "2m"
    )
    if ($NoRestore) { $testArguments += "--no-restore" }
    & dotnet @testArguments
    if ($LASTEXITCODE -ne 0) { throw "T0 feasibility failed. Evidence: $results" }

    Copy-Item ".\tests\GovernedAgent.IntegrationTests\obj\project.assets.json" `
        (Join-Path $results "project.assets.json")
    $assembly = [Reflection.AssemblyName]::GetAssemblyName(
        (Join-Path (Get-Location) "tests\GovernedAgent.IntegrationTests\bin\Release\net10.0\Microsoft.Agents.AI.Workflows.dll"))
    "workflowAssembly=$($assembly.FullName)" | Add-Content (Join-Path $results "provenance.txt")
    Write-Output "T0 evidence: $results"
}
finally {
    Pop-Location
}
