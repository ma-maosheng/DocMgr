# 打出自包含测试包到 installer-out（不依赖 Inno Setup）。
# 用法（仓库 DocMgr 目录）：
#   powershell -NoProfile -File tools\PackTestInstaller.ps1
# 已有 publish\overlay-win-x64 时可加 -SkipPublish

[CmdletBinding()]
param(
    [switch] $SkipPublish
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "DocMgr.csproj"
$publishDir = Join-Path $projectRoot "publish\overlay-win-x64"
$outDir = Join-Path $projectRoot "installer-out"
$zipPath = Join-Path $outDir "DocMgr-Setup-1.0.0-win-x64.zip"

if (-not $SkipPublish) {
    if (Test-Path -LiteralPath $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }
    & dotnet publish $projectFile -c Release -r win-x64 --self-contained true -o $publishDir -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish 失败，退出码 $LASTEXITCODE"
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $publishDir "DocMgr.exe"))) {
    throw "未找到发布结果：$publishDir\DocMgr.exe"
}

Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Install-DocMgr.ps1") -Destination (Join-Path $publishDir "Install-DocMgr.ps1") -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "README-install.txt") -Destination (Join-Path $publishDir "README-install.txt") -Force

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Push-Location $publishDir
try {
    & tar.exe -a -cf $zipPath *
    if ($LASTEXITCODE -ne 0) {
        throw "打包 zip 失败，退出码 $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

Write-Host "Package created: $zipPath"
Write-Host ("SizeMB={0:N1}" -f ((Get-Item -LiteralPath $zipPath).Length / 1MB))
