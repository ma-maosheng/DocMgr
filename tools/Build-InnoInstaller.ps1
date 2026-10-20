# 一键打 Inno 安装包：框架依赖发布 + 附带 .NET 8 桌面运行时 + 编译 DocMgr.iss
# 用法（仓库 DocMgr 目录）：
#   powershell -NoProfile -File tools\Build-InnoInstaller.ps1
# 已有 publish\overlay-win-x64 且不需重发时可加 -SkipPublish
# 已有 tools\redist\windowsdesktop-runtime-win-x64.exe 时可加 -SkipRuntimeDownload

[CmdletBinding()]
param(
    [switch] $SkipPublish,
    [switch] $SkipRuntimeDownload
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "DocMgr.csproj"
$publishDir = Join-Path $projectRoot "publish\overlay-win-x64"
$redistDir = Join-Path $PSScriptRoot "redist"
$runtimeFileName = "windowsdesktop-runtime-win-x64.exe"
$runtimePath = Join-Path $redistDir $runtimeFileName
$issPath = Join-Path $PSScriptRoot "DocMgr.iss"
$outDir = Join-Path $projectRoot "installer-out"

function Find-ISCC {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    foreach ($p in $candidates) {
        if ($p -and (Test-Path -LiteralPath $p)) { return $p }
    }
    $cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Invoke-DownloadFile {
    param(
        [Parameter(Mandatory = $true)][string] $Uri,
        [Parameter(Mandatory = $true)][string] $OutFile
    )
    $proxy = "http://127.0.0.1:7890"
    $attempts = @(
        @{ Args = @("-fsSL", "--connect-timeout", "30", "-L", "-o", $OutFile, $Uri); Label = "direct" },
        @{ Args = @("-fsSL", "--connect-timeout", "30", "-L", "--proxy", $proxy, "-o", $OutFile, $Uri); Label = "proxy" }
    )
    foreach ($a in $attempts) {
        Write-Host "下载 [$($a.Label)]: $Uri"
        Remove-Item -LiteralPath $OutFile -Force -ErrorAction SilentlyContinue
        & curl.exe @($a.Args)
        if ($LASTEXITCODE -eq 0 -and (Test-Path -LiteralPath $OutFile) -and ((Get-Item -LiteralPath $OutFile).Length -gt 1MB)) {
            Write-Host ("下载成功，大小 {0:N1} MB" -f ((Get-Item -LiteralPath $OutFile).Length / 1MB))
            return
        }
        Write-Host "尝试失败 (exit=$LASTEXITCODE)"
    }
    throw "无法下载 .NET 8 桌面运行时。请手动下载后保存为：$OutFile`n官方入口：https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"
}

if (-not (Test-Path -LiteralPath $projectFile)) {
    throw "未找到项目文件：$projectFile"
}
if (-not (Test-Path -LiteralPath $issPath)) {
    throw "未找到 Inno 脚本：$issPath"
}

$iscc = Find-ISCC
if (-not $iscc) {
    throw "未找到 Inno Setup 6 的 ISCC.exe。请先安装 Inno Setup 6。"
}
Write-Host "ISCC: $iscc"

# 1) 框架依赖发布（运行时由安装包 redist 提供）
if (-not $SkipPublish) {
    if (Test-Path -LiteralPath $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }
    Write-Host "dotnet publish Release win-x64 self-contained=false ..."
    & dotnet publish $projectFile -c Release -r win-x64 --self-contained false -o $publishDir -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish 失败，退出码 $LASTEXITCODE"
    }
}

$exePath = Join-Path $publishDir "DocMgr.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "未找到发布结果：$exePath"
}
foreach ($required in @("appsettings.json", "appsettings.lan.example.json")) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishDir $required))) {
        throw "发布目录缺少 $required"
    }
}

# 2) 下载 .NET 8 桌面运行时（运行程序所需；不是 SDK）
New-Item -ItemType Directory -Force -Path $redistDir | Out-Null
if ($SkipRuntimeDownload -and -not (Test-Path -LiteralPath $runtimePath)) {
    throw "指定了 -SkipRuntimeDownload，但缺少：$runtimePath"
}
if (-not $SkipRuntimeDownload) {
    if ((Test-Path -LiteralPath $runtimePath) -and ((Get-Item -LiteralPath $runtimePath).Length -gt 1MB)) {
        Write-Host "已存在运行时安装包，跳过下载：$runtimePath"
    }
    else {
        Invoke-DownloadFile -Uri "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe" -OutFile $runtimePath
    }
}
else {
    Write-Host "跳过运行时下载，使用已有：$runtimePath"
}

# 3) 编译 Inno 安装包
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
Push-Location $PSScriptRoot
try {
    & $iscc $issPath
    if ($LASTEXITCODE -ne 0) {
        throw "ISCC 编译失败，退出码 $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

$setup = Get-ChildItem -LiteralPath $outDir -Filter "DocMgr-Setup-*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $setup) {
    throw "未在 $outDir 找到 DocMgr-Setup-*.exe"
}

Write-Host ""
Write-Host "安装包已生成：$($setup.FullName)"
Write-Host ("大小 {0:N1} MB" -f ($setup.Length / 1MB))
Write-Host "说明：框架依赖发布；若目标机无 .NET 8 桌面运行时，向导会提示安装包内 redist。"
