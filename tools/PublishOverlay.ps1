# 将 Release 发布结果覆盖安装到目标目录，不覆盖业务库与已有数据库路径配置。
# 用法（在仓库 DocMgr 目录下）：
#   powershell -NoProfile -File tools\PublishOverlay.ps1 -TargetDir "D:\DocMgr"
# 目标电脑若未安装 .NET 8 桌面运行时，请加 -SelfContained

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $TargetDir,

    [switch] $SelfContained
)

$ErrorActionPreference = "Stop"

function Test-IsDatabaseFile {
    param([string] $FileName)
    if ($FileName -match '(?i)\.db-wal$' -or $FileName -match '(?i)\.db-shm$') {
        return $true
    }
    if ($FileName -match '(?i)\.pre-migrate-.*\.db$') {
        return $true
    }
    if ($FileName -match '(?i)\.db$') {
        return $true
    }
    return $false
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "DocMgr.csproj"
if (-not (Test-Path -LiteralPath $projectFile)) {
    throw "未找到项目文件：$projectFile"
}

$publishDir = Join-Path $projectRoot "publish\overlay-win-x64"
$runtimeId = "win-x64"
$selfContainedArg = if ($SelfContained) { "true" } else { "false" }

Write-Host "发布配置：Release / $runtimeId / self-contained=$selfContainedArg"
Write-Host "发布目录：$publishDir"
Write-Host "安装目录：$TargetDir"

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

$publishArgs = @(
    "publish", $projectFile,
    "-c", "Release",
    "-r", $runtimeId,
    "--self-contained", $selfContainedArg,
    "-o", $publishDir,
    "-p:DebugType=None",
    "-p:DebugSymbols=false"
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失败，退出码 $LASTEXITCODE"
}

$targetFull = [System.IO.Path]::GetFullPath($TargetDir)
New-Item -ItemType Directory -Force -Path $targetFull | Out-Null

$copied = 0
$skippedDb = 0
$preservedSettings = 0

Get-ChildItem -LiteralPath $publishDir -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($publishDir.Length).TrimStart("\", "/")
    $destination = Join-Path $targetFull $relative
    $fileName = $_.Name

    if (Test-IsDatabaseFile $fileName) {
        $skippedDb++
        return
    }

    if ($fileName -eq "appsettings.json" -and (Test-Path -LiteralPath $destination)) {
        $publishedSettings = Join-Path ([System.IO.Path]::GetDirectoryName($destination)) "appsettings.json.published"
        Copy-Item -LiteralPath $_.FullName -Destination $publishedSettings -Force
        $preservedSettings++
        Write-Host "已保留目标目录现有 appsettings.json，新模板写入 appsettings.json.published"
        return
    }

    $destDir = [System.IO.Path]::GetDirectoryName($destination)
    if (-not (Test-Path -LiteralPath $destDir)) {
        New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    }

    Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
    $copied++
}

Write-Host "覆盖完成：复制 $copied 个文件，跳过数据库文件 $skippedDb 个，保留已有配置 $preservedSettings 个。"
Write-Host "请确认目标电脑已退出本程序后再启动 $targetFull\DocMgr.exe"
if (-not $SelfContained) {
    Write-Host "当前为依赖框架发布，目标电脑需已安装 .NET 8 桌面运行时（Windows Desktop）。"
}
