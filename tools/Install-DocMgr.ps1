# 将当前目录中的自包含程序安装到 %LOCALAPPDATA%\DocMgr，并创建开始菜单快捷方式。
# 不覆盖 DocMgr.db / WAL，已有 appsettings.json 时保留。
# 用法：解压后右键「使用 PowerShell 运行」，或：
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-DocMgr.ps1

[CmdletBinding()]
param(
    [string] $TargetDir = (Join-Path $env:LOCALAPPDATA "DocMgr"),
    [switch] $DesktopShortcut
)

$ErrorActionPreference = "Stop"

function Test-IsDatabaseFile {
    param([string] $FileName)
    if ($FileName -match '(?i)\.db-wal$' -or $FileName -match '(?i)\.db-shm$') { return $true }
    if ($FileName -match '(?i)\.pre-migrate-.*\.db$') { return $true }
    if ($FileName -match '(?i)\.db$') { return $true }
    return $false
}

$sourceRoot = $PSScriptRoot
$exeSource = Join-Path $sourceRoot "DocMgr.exe"
if (-not (Test-Path -LiteralPath $exeSource)) {
    throw "未找到 DocMgr.exe，请在解压后的程序目录中运行本脚本。"
}

$targetFull = [System.IO.Path]::GetFullPath($TargetDir)
Write-Host "安装目录：$targetFull"
New-Item -ItemType Directory -Force -Path $targetFull | Out-Null

$copied = 0
$skippedDb = 0
$preservedSettings = 0

Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($sourceRoot.Length).TrimStart("\", "/")
    if ($relative -match '(?i)^(Install-DocMgr\.ps1|README-install\.txt)$') {
        return
    }

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
        return
    }

    $destDir = [System.IO.Path]::GetDirectoryName($destination)
    if (-not (Test-Path -LiteralPath $destDir)) {
        New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    }

    Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
    $copied++
}

$exeTarget = Join-Path $targetFull "DocMgr.exe"
$programs = [Environment]::GetFolderPath("Programs")
$shortcutDir = Join-Path $programs "测绘资料管理系统"
New-Item -ItemType Directory -Force -Path $shortcutDir | Out-Null
$shell = New-Object -ComObject WScript.Shell
$menuLink = $shell.CreateShortcut((Join-Path $shortcutDir "测绘资料管理系统.lnk"))
$menuLink.TargetPath = $exeTarget
$menuLink.WorkingDirectory = $targetFull
$menuLink.IconLocation = $exeTarget
$menuLink.Save()

if ($DesktopShortcut) {
    $desktop = [Environment]::GetFolderPath("Desktop")
    $desktopLink = $shell.CreateShortcut((Join-Path $desktop "测绘资料管理系统.lnk"))
    $desktopLink.TargetPath = $exeTarget
    $desktopLink.WorkingDirectory = $targetFull
    $desktopLink.IconLocation = $exeTarget
    $desktopLink.Save()
}

Write-Host "安装完成：复制 $copied 个文件，跳过数据库 $skippedDb 个，保留已有配置 $preservedSettings 个。"
Write-Host "可从开始菜单启动「测绘资料管理系统」，或直接打开：$exeTarget"
Write-Host "首次启动会在安装目录创建 DocMgr.db；覆盖安装请勿用空库覆盖该文件。"
