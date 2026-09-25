# 扫描工作区中被意外清空的源文件（文件仍在、内容接近 0 字节）。
# 用法（仓库 DocMgr 目录）：
#   powershell -NoProfile -File tools\Scan-EmptySourceFiles.ps1
#   powershell -NoProfile -File tools\Scan-EmptySourceFiles.ps1 -Restore
#
# -Restore：仅恢复「工作区空、但 HEAD 有内容」的已跟踪文件（git checkout HEAD --）。
# 仓库里本来就空的占位文件（如 BoolToVisConverter.cs）会跳过。

[CmdletBinding()]
param(
    [switch] $Restore
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
Set-Location -LiteralPath $projectRoot

function Get-RepoRelativePath {
    param([string] $FullPath)
    $root = $projectRoot.TrimEnd('\', '/')
    $full = (Resolve-Path -LiteralPath $FullPath).Path
    if ($full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($root.Length).TrimStart('\', '/').Replace('\', '/')
    }
    throw "路径不在仓库内：$FullPath"
}

$maxEmptyBytes = 5
$sourceExt = @('.cs', '.xaml', '.yaml', '.yml', '.json', '.mdc')
$excludeDirPattern = '\\(bin|obj|\.vs|\.git|publish|installer-out|node_modules|\.build-|agent-transcripts)\\'

Write-Host "扫描目录：$projectRoot"
Write-Host "阈值：<= $maxEmptyBytes 字节视为空"
Write-Host ""

$candidates = Get-ChildItem -LiteralPath $projectRoot -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Length -le $maxEmptyBytes -and
        $sourceExt -contains $_.Extension.ToLowerInvariant() -and
        $_.FullName -notmatch $excludeDirPattern
    }

if (-not $candidates) {
    Write-Host "未发现空源文件。"
    exit 0
}

$trackedSet = @{}
foreach ($p in @(git ls-files -- "*.cs" "*.xaml" "*.yaml" "*.yml" "*.json" "*.mdc")) {
    if (-not [string]::IsNullOrWhiteSpace($p)) {
        $trackedSet[$p.Replace('\', '/')] = $true
    }
}

$rows = foreach ($f in $candidates) {
    $rel = Get-RepoRelativePath -FullPath $f.FullName
    $isTracked = $trackedSet.ContainsKey($rel)
    $headBytes = $null
    if ($isTracked) {
        $sizeText = git cat-file -s "HEAD:$rel" 2>$null
        if ($LASTEXITCODE -eq 0 -and $sizeText -match '^\d+$') {
            $headBytes = [int]$sizeText
        }
    }

    $kind = if (-not $isTracked) {
        "未跟踪空文件"
    }
    elseif ($null -eq $headBytes) {
        "已跟踪(HEAD 无此路径)"
    }
    elseif ($headBytes -le $maxEmptyBytes) {
        "占位空文件(HEAD 亦空，跳过)"
    }
    else {
        "疑似误清空(可恢复)"
    }

    [pscustomobject]@{
        Kind      = $kind
        DiskBytes = [int]$f.Length
        HeadBytes = $headBytes
        Path      = $rel
        FullPath  = $f.FullName
    }
}

$rows | Sort-Object Kind, Path | Format-Table Kind, DiskBytes, HeadBytes, Path -AutoSize

$restorable = @($rows | Where-Object { $_.Kind -eq "疑似误清空(可恢复)" })
Write-Host ""
Write-Host ("合计 {0} 个空文件；其中疑似误清空 {1} 个。" -f $rows.Count, $restorable.Count)

if ($restorable.Count -eq 0) {
    exit 0
}

if (-not $Restore) {
    Write-Host ""
    Write-Host '仅扫描。若要恢复疑似误清空文件，请加 -Restore：'
    Write-Host '  powershell -NoProfile -File tools\Scan-EmptySourceFiles.ps1 -Restore'
    exit 1
}

Write-Host ""
Write-Host "正在从 HEAD 恢复 $($restorable.Count) 个文件..."
$paths = @($restorable | ForEach-Object { $_.Path })
git checkout HEAD -- @paths
if ($LASTEXITCODE -ne 0) {
    throw "git checkout 失败，退出码 $LASTEXITCODE"
}

foreach ($r in $restorable) {
    $len = (Get-Item -LiteralPath $r.FullPath).Length
    Write-Host ("  已恢复 {0} -> {1} 字节" -f $r.Path, $len)
}

Write-Host "完成。"
exit 0