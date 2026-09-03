# UTF-8 出力設定
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$SourceDir = $PSScriptRoot
if (-not $SourceDir) { $SourceDir = "c:\Git\CharacterEditor-FacialAnimation-EyePatch" }
$ModName = Split-Path -Leaf $SourceDir
$TargetDir = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\$ModName"

# 公開に必要なフォルダのみを同期（ホワイトリスト方式）
$IncludeFolders = @("About", "1.6", "Defs", "Languages", "Textures")

# 除外するファイルパターン
$ExcludeFiles = @("*.pdb", "*.user", "*.suo", "*.tmp", ".gitignore", "*.cs", "*.csproj", "*.sln")

# 除外するディレクトリパターン
$ExcludeDirs = @(".agents", ".git", "Source", "Docs", "obj", "bin", ".vs", ".idea")

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " $ModName -> Local Mods 安全同期" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "送信元: $SourceDir"
Write-Host "同期先: $TargetDir"
Write-Host ""

# 既存のJunctionリンクがあれば安全に解除して実体フォルダに移行
if (Test-Path $TargetDir) {
    $item = Get-Item -LiteralPath $TargetDir -Force
    if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
        Write-Host "[移行] 既存のJunctionリンクを解除して安全な実体フォルダに移行します..." -ForegroundColor Yellow
        cmd /c rmdir "$TargetDir"
    }
}

# ターゲットディレクトリの作成
if (-not (Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
    Write-Host "[作成] Modsフォルダ内に実体フォルダを作成しました: $TargetDir" -ForegroundColor Green
}

# 0. PublishedFileId.txt の自動逆回収（Workshop公開後にローカルModsに生成されたIDをGit側へ保護）
$modsPublishedFile = Join-Path $TargetDir "About\PublishedFileId.txt"
$gitPublishedFile = Join-Path $SourceDir "About\PublishedFileId.txt"
if (Test-Path $modsPublishedFile) {
    if (-not (Test-Path $gitPublishedFile) -or ((Get-FileHash $modsPublishedFile).Hash -ne (Get-FileHash $gitPublishedFile).Hash)) {
        Copy-Item -Path $modsPublishedFile -Destination $gitPublishedFile -Force
        Write-Host "[自動回収] Modsフォルダから PublishedFileId.txt をGitリポジトリへ同期しました: $(Get-Content $gitPublishedFile)" -ForegroundColor Green
    }
}

# 1. ホワイトリスト指定フォルダの同期
foreach ($folder in $IncludeFolders) {
    $srcSub = Join-Path $SourceDir $folder
    $dstSub = Join-Path $TargetDir $folder

    if (Test-Path $srcSub) {
        Write-Host "同期中: $folder..." -ForegroundColor Yellow
        & robocopy $srcSub $dstSub /MIR /XD $ExcludeDirs /XF $ExcludeFiles /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
    }
}

# 2. 厳格な安全スキャン（二重チェック：.agents, Source, Docs, .git, *.pdb 等の完全排除）
Write-Host ""
Write-Host "同期先フォルダの安全スキャンを実行中..." -ForegroundColor Cyan

$DisallowedItems = @(".agents", "Source", "Docs", ".git", ".vs", ".idea", ".gitignore")
$hasRemoved = $false

# ディレクトリ・ファイルの直接スキャン
foreach ($item in $DisallowedItems) {
    $found = Get-ChildItem -Path $TargetDir -Recurse -Force -ErrorAction SilentlyContinue | Where-Object { $_.Name -ieq $item }
    foreach ($f in $found) {
        Remove-Item -Path $f.FullName -Recurse -Force
        Write-Host "[警告・強制削除] 不正な項目を検知し削除しました: $($f.FullName)" -ForegroundColor Red
        $hasRemoved = $true
    }
}

# PDBファイル・ソースファイル等の残存スキャン
$DisallowedExtensions = @("*.pdb", "*.cs", "*.csproj", "*.sln")
foreach ($ext in $DisallowedExtensions) {
    $files = Get-ChildItem -Path $TargetDir -Recurse -Filter $ext -ErrorAction SilentlyContinue
    foreach ($file in $files) {
        Remove-Item -Path $file.FullName -Force
        Write-Host "[警告・強制削除] 不要ファイルを検知し削除しました: $($file.FullName)" -ForegroundColor Red
        $hasRemoved = $true
    }
}

# 3. 最終結果レポート
Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
if ($hasRemoved) {
    Write-Host " [注意] 不要な項目が検出されたため強制削除しました。" -ForegroundColor Yellow
}
Write-Host " [完了] 同期が完了しました！" -ForegroundColor Green
Write-Host " .agents や Source 等の開発ファイルは一切含まれていません。" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
