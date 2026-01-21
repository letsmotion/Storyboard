# 分镜大师 - PowerShell 发布脚本
# 支持自动修改版本号、提交代码、创建标签并发布

param(
    [Parameter(Mandatory=$false)]
    [string]$Version,

    [Parameter(Mandatory=$false)]
    [ValidateSet("patch", "minor", "major")]
    [string]$IncrementType = "patch",

    [Parameter(Mandatory=$false)]
    [switch]$SkipGitee,

    [Parameter(Mandatory=$false)]
    [switch]$AutoSync,

    [Parameter(Mandatory=$false)]
    [switch]$Resume,

    [Parameter(Mandatory=$false)]
    [ValidateSet("commit", "push", "tag", "build", "download", "gitee")]
    [string]$ResumeFrom
)

# 配置
$GitHubRepo = "BroderQi/Storyboard"
$GiteeRepo = "nan1314/Storyboard"

# 确定项目根目录（脚本可能从 scripts 目录或项目根目录运行）
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (Test-Path (Join-Path $ScriptDir "Storyboard.csproj")) {
    $ProjectRoot = $ScriptDir
} elseif (Test-Path (Join-Path $ScriptDir "..\Storyboard.csproj")) {
    $ProjectRoot = Resolve-Path (Join-Path $ScriptDir "..")
} else {
    Write-ColorOutput "[Error] Cannot find Storyboard.csproj" "Red"
    exit 1
}

$DownloadDir = Join-Path $ProjectRoot "release-temp"
$CsprojFile = Join-Path $ProjectRoot "Storyboard.csproj"

# 切换到项目根目录
Set-Location $ProjectRoot
Write-Host "Project root: $ProjectRoot" -ForegroundColor Gray
Write-Host ""

# 颜色输出函数
function Write-ColorOutput {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

function Write-Header {
    param([string]$Title)
    Write-Host ""
    Write-Host "================================================================================" -ForegroundColor Cyan
    Write-Host "  $Title" -ForegroundColor Cyan
    Write-Host "================================================================================" -ForegroundColor Cyan
    Write-Host ""
}

# 检查 gh 命令行工具
function Test-GitHubCLI {
    try {
        gh --version | Out-Null
        return $true
    } catch {
        return $false
    }
}

# 获取当前版本号
function Get-CurrentVersion {
    if (-not (Test-Path $CsprojFile)) {
        return $null
    }

    $content = Get-Content $CsprojFile -Raw
    if ($content -match '<Version>([\d\.]+)</Version>') {
        return $matches[1]
    }
    return $null
}

# 递增版本号
function Get-IncrementedVersion {
    param([string]$CurrentVersion, [string]$Type)

    if (-not $CurrentVersion) {
        return "1.0.0"
    }

    $parts = $CurrentVersion.Split('.')
    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = [int]$parts[2]

    switch ($Type) {
        "major" {
            $major++
            $minor = 0
            $patch = 0
        }
        "minor" {
            $minor++
            $patch = 0
        }
        "patch" {
            $patch++
        }
    }

    return "$major.$minor.$patch"
}

# 更新 .csproj 文件中的版本号
function Update-CsprojVersion {
    param([string]$NewVersion)

    if (-not (Test-Path $CsprojFile)) {
        Write-ColorOutput "[Error] Project file not found: $CsprojFile" "Red"
        return $false
    }

    $content = Get-Content $CsprojFile -Raw

    # 替换现有版本号
    if ($content -match '<Version>[\d\.]+</Version>') {
        $content = $content -replace '<Version>[\d\.]+</Version>', "<Version>$NewVersion</Version>"
        $content = $content -replace '<AssemblyVersion>[\d\.]+</AssemblyVersion>', "<AssemblyVersion>$NewVersion</AssemblyVersion>"
        $content = $content -replace '<FileVersion>[\d\.]+</FileVersion>', "<FileVersion>$NewVersion</FileVersion>"
    } else {
        # 在 PropertyGroup 中添加 Version 标签
        $versionBlock = @"
    <!-- Version: Auto-updated by release script -->
    <Version>$NewVersion</Version>
    <AssemblyVersion>$NewVersion</AssemblyVersion>
    <FileVersion>$NewVersion</FileVersion>
"@
        $content = $content -replace '(<PropertyGroup>)', "`$1`n$versionBlock"
    }

    Set-Content -Path $CsprojFile -Value $content -NoNewline -Encoding UTF8
    Write-ColorOutput "Updated version in $CsprojFile to: $NewVersion" "Green"
    return $true
}

# 主流程
Write-Header "Storyboard Master - Quick Release Script"

# 确定起始步骤
$startStep = 0
if ($Resume -or $ResumeFrom) {
    Write-ColorOutput "Resume mode enabled" "Yellow"

    if (-not $Version) {
        $Version = Get-CurrentVersion
        if (-not $Version) {
            Write-ColorOutput "[Error] Cannot determine version for resume. Please specify -Version" "Red"
            exit 1
        }
    }

    $TagName = "v$Version"
    Write-ColorOutput "Resuming release for version: $TagName" "Cyan"
    Write-Host ""

    # 设置起始步骤
    switch ($ResumeFrom) {
        "push" { $startStep = 3 }
        "tag" { $startStep = 4 }
        "build" { $startStep = 5 }
        "download" { $startStep = 6 }
        "gitee" { $startStep = 7 }
        default { $startStep = 0 }
    }

    Write-ColorOutput "Starting from step $startStep" "Yellow"
    Write-Host ""
}

# 步骤 0: 确定版本号
if ($startStep -le 0) {
    Write-Header "[0/8] Determine Version"

    $currentVersion = Get-CurrentVersion

    if ($currentVersion) {
        Write-ColorOutput "Current version: $currentVersion" "Cyan"
    } else {
        Write-ColorOutput "No version found, starting from 1.0.0" "Yellow"
    }

    # 获取版本号
    if (-not $Version) {
        if ($currentVersion) {
            $suggestedVersion = Get-IncrementedVersion -CurrentVersion $currentVersion -Type $IncrementType
            Write-Host ""
            Write-Host "Suggested new version: $suggestedVersion (increment type: $IncrementType)"
            Write-Host ""
            Write-Host "Options:"
            Write-Host "  1. Use suggested version $suggestedVersion"
            Write-Host "  2. Manually enter version"
            Write-Host ""

            $choice = Read-Host "Please choose (1/2)"

            if ($choice -eq "1") {
                $Version = $suggestedVersion
            } else {
                $Version = Read-Host "Enter version (e.g., 1.0.0)"
            }
        } else {
            $Version = Read-Host "Enter version (e.g., 1.0.0)"
        }

        if ([string]::IsNullOrWhiteSpace($Version)) {
            Write-ColorOutput "[Error] Version cannot be empty" "Red"
            exit 1
        }
    }

    # 验证版本号格式
    if ($Version -notmatch '^\d+\.\d+\.\d+$') {
        Write-ColorOutput "[Error] Invalid version format. Should be: major.minor.patch (e.g., 1.0.0)" "Red"
        exit 1
    }

    $TagName = "v$Version"
    Write-ColorOutput "Preparing to release version: $TagName" "Green"
    Write-Host ""
} else {
    # 恢复模式下，确保 TagName 已设置
    if (-not $TagName) {
        $TagName = "v$Version"
    }
}

# 步骤 1: 更新项目文件中的版本号
if ($startStep -le 1) {
    Write-Header "[1/8] Update Project Version"

    $updateSuccess = Update-CsprojVersion -NewVersion $Version

    if (-not $updateSuccess) {
        Write-ColorOutput "[Error] Failed to update version" "Red"
        exit 1
    }

    Write-Host ""
    Write-Host "Will perform the following actions:"
    Write-Host "  1. Updated version in $CsprojFile"
    Write-Host "  2. Commit version changes"
    Write-Host "  3. Create tag $TagName"
    Write-Host "  4. Push to GitHub (trigger auto-build)"
    if (-not $SkipGitee) {
        Write-Host "  5. Wait for build completion"
        Write-Host "  6. Download GitHub Release files"
        Write-Host "  7. Sync to Gitee Release"
    }
    Write-Host ""

    if (-not $AutoSync) {
        $confirm = Read-Host "Continue? (Y/N)"
        if ($confirm -ne "Y" -and $confirm -ne "y") {
            Write-ColorOutput "Operation cancelled." "Yellow"
            git checkout $CsprojFile
            exit 0
        }
    }
}

# 步骤 2: 检查 Git 状态
if ($startStep -le 2) {
    Write-Header "[2/8] Check Git Status"
    git status

    $hasChanges = git status --porcelain
    if ($hasChanges) {
        Write-ColorOutput "Detected uncommitted changes" "Yellow"

        # 步骤 3: 提交更改
        Write-Header "[3/8] Commit Changes"
        $commitMsg = "Release version $TagName"
        Write-ColorOutput "Commit message: $commitMsg" "Cyan"

        git add .
        git commit -m $commitMsg

        if ($LASTEXITCODE -ne 0) {
            Write-ColorOutput "[Error] Git commit failed" "Red"
            exit 1
        }

        Write-ColorOutput "Commit successful" "Green"
    } else {
        Write-ColorOutput "[2/8] No uncommitted changes" "Gray"
    }
}

# 步骤 3: 推送到 GitHub
if ($startStep -le 3) {
    Write-Header "[3/8] Push to GitHub"
    Write-ColorOutput "Pushing to GitHub..." "Cyan"
    git push

    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "[Error] Push failed" "Red"
        Write-Host ""
        Write-Host "Possible solutions:" -ForegroundColor Yellow
        Write-Host "  1. Check network connection to GitHub"
        Write-Host "  2. Use proxy: git config --global http.proxy http://127.0.0.1:7890"
        Write-Host "  3. Use SSH instead: git remote set-url origin git@github.com:BroderQi/Storyboard.git"
        Write-Host ""

        $retry = Read-Host "Retry push? (Y/N/Skip)"

        if ($retry -eq "Y" -or $retry -eq "y") {
            Write-ColorOutput "Retrying push..." "Cyan"
            git push
            if ($LASTEXITCODE -ne 0) {
                Write-ColorOutput "[Error] Push failed again" "Red"
                Write-Host ""
                Write-Host "You can manually push later with: git push" -ForegroundColor Yellow
                $continue = Read-Host "Continue with tag creation anyway? (Y/N)"
                if ($continue -ne "Y" -and $continue -ne "y") {
                    exit 1
                }
            } else {
                Write-ColorOutput "Push successful" "Green"
            }
        } elseif ($retry -eq "Skip" -or $retry -eq "skip" -or $retry -eq "S" -or $retry -eq "s") {
            Write-ColorOutput "Skipped push, continuing with tag creation..." "Yellow"
            Write-Host "Remember to push manually later: git push" -ForegroundColor Yellow
        } else {
            exit 1
        }
    } else {
        Write-ColorOutput "Push successful" "Green"
    }
}

# 步骤 4: 创建并推送版本标签
if ($startStep -le 4) {
    Write-Header "[4/8] Create and Push Tag"

    # 检查标签是否已存在
    $existingTag = git tag -l $TagName
    if ($existingTag) {
        Write-ColorOutput "[Warning] Tag $TagName already exists" "Yellow"
        $deleteTag = Read-Host "Delete old tag and recreate? (Y/N)"
        if ($deleteTag -eq "Y" -or $deleteTag -eq "y") {
            git tag -d $TagName
            git push origin :refs/tags/$TagName
            Write-ColorOutput "Old tag deleted" "Green"
        } else {
            Write-ColorOutput "Operation cancelled" "Yellow"
            exit 0
        }
    }

    # 创建新标签
    git tag $TagName

    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "[Error] Failed to create tag" "Red"
        exit 1
    }

    Write-ColorOutput "Tag $TagName created successfully" "Green"

    # 推送标签
    Write-Host ""
    Write-ColorOutput "Pushing tag to GitHub..." "Cyan"
    git push origin $TagName
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "[Error] Failed to push tag" "Red"
        Write-Host ""
        Write-Host "Possible solutions:" -ForegroundColor Yellow
        Write-Host "  1. Check network connection to GitHub"
        Write-Host "  2. Use proxy if needed"
        Write-Host "  3. Manually push later: git push origin $TagName"
        Write-Host ""

        $retryTag = Read-Host "Retry pushing tag? (Y/N/Skip)"

        if ($retryTag -eq "Y" -or $retryTag -eq "y") {
            Write-ColorOutput "Retrying tag push..." "Cyan"
            git push origin $TagName
            if ($LASTEXITCODE -ne 0) {
                Write-ColorOutput "[Error] Tag push failed again" "Red"
                Write-Host ""
                Write-Host "You can manually push the tag later with: git push origin $TagName" -ForegroundColor Yellow
                $continueWithoutTag = Read-Host "Continue anyway? (Y/N)"
                if ($continueWithoutTag -ne "Y" -and $continueWithoutTag -ne "y") {
                    exit 1
                }
            } else {
                Write-ColorOutput "Tag pushed successfully" "Green"
            }
        } elseif ($retryTag -eq "Skip" -or $retryTag -eq "skip" -or $retryTag -eq "S" -or $retryTag -eq "s") {
            Write-ColorOutput "Skipped tag push" "Yellow"
            Write-Host "Remember to push tag manually: git push origin $TagName" -ForegroundColor Yellow
        } else {
            exit 1
        }
    } else {
        Write-ColorOutput "Tag pushed to GitHub, GitHub Actions will start building" "Green"
    }
}

# 步骤 5: 等待 GitHub Actions 构建
if ($startStep -le 5) {
    Write-Header "[5/8] Wait for GitHub Actions Build"

    Write-ColorOutput "GitHub Actions is building..." "Cyan"
    Write-Host ""
    Write-Host "Build progress: https://github.com/$GitHubRepo/actions"
    Write-Host "Release page: https://github.com/$GitHubRepo/releases/tag/$TagName"
    Write-Host ""

    if ($SkipGitee) {
        Write-ColorOutput "Skipped Gitee sync step" "Yellow"
        Write-Host ""
        Write-ColorOutput "GitHub Release: https://github.com/$GitHubRepo/releases/tag/$TagName" "Green"
        Write-Host ""
        Write-ColorOutput "Release completed!" "Green"
        exit 0
    }

    # 检查是否安装了 gh CLI
    $hasGH = Test-GitHubCLI

    if (-not $hasGH) {
        Write-ColorOutput "[Warning] GitHub CLI (gh) not installed" "Yellow"
        Write-Host ""
        Write-Host "Please install GitHub CLI first:"
        Write-Host "  Option 1: Using winget"
        Write-Host "    winget install --id GitHub.cli"
        Write-Host ""
        Write-Host "  Option 2: Using scoop"
        Write-Host "    scoop install gh"
        Write-Host ""
        Write-Host "  Option 3: Manual download"
        Write-Host "    https://cli.github.com/"
        Write-Host ""
        Write-ColorOutput "Skipping auto-download step, please download manually and sync to Gitee" "Yellow"
        Write-Host ""
        Write-Host "GitHub Release: https://github.com/$GitHubRepo/releases/tag/$TagName"
        Write-Host "Gitee Release: https://gitee.com/$GiteeRepo/releases/new"
        exit 0
    }

    Write-ColorOutput "Waiting for build to complete..." "Cyan"
    Write-Host ""

    if (-not $AutoSync) {
        Write-Host "Options:" -ForegroundColor Cyan
        Write-Host "  1. Auto-check build status (requires gh CLI)"
        Write-Host "  2. Manual wait (press Enter when done)"
        Write-Host ""
        $waitChoice = Read-Host "Choose option (1/2)"

        if ($waitChoice -eq "1") {
            Write-ColorOutput "Checking build status..." "Cyan"
            Write-Host ""

            $maxAttempts = 60  # 最多等待 30 分钟（每次 30 秒）
            $attempt = 0
            $buildComplete = $false

            while ($attempt -lt $maxAttempts -and -not $buildComplete) {
                $attempt++

                # 检查 workflow 运行状态
                $runs = gh run list --repo $GitHubRepo --limit 1 --json status,conclusion,name | ConvertFrom-Json

                if ($runs.Count -gt 0) {
                    $latestRun = $runs[0]
                    $status = $latestRun.status
                    $conclusion = $latestRun.conclusion

                    Write-Host "[$attempt/$maxAttempts] Build status: $status" -NoNewline

                    if ($status -eq "completed") {
                        Write-Host ""
                        if ($conclusion -eq "success") {
                            Write-ColorOutput "Build completed successfully!" "Green"
                            $buildComplete = $true
                        } elseif ($conclusion -eq "failure") {
                            Write-ColorOutput "Build failed!" "Red"
                            Write-Host "Check: https://github.com/$GitHubRepo/actions"
                            $continueAnyway = Read-Host "Continue anyway? (Y/N)"
                            if ($continueAnyway -ne "Y" -and $continueAnyway -ne "y") {
                                exit 1
                            }
                            $buildComplete = $true
                        } else {
                            Write-ColorOutput "Build completed with status: $conclusion" "Yellow"
                            $buildComplete = $true
                        }
                    } else {
                        Write-Host " - waiting..."
                        Start-Sleep -Seconds 30
                    }
                } else {
                    Write-Host "[$attempt/$maxAttempts] No workflow runs found, waiting..."
                    Start-Sleep -Seconds 30
                }
            }

            if (-not $buildComplete) {
                Write-ColorOutput "Timeout waiting for build. Please check manually." "Yellow"
                Write-Host "Build progress: https://github.com/$GitHubRepo/actions"
                Read-Host "Press Enter when build completes..."
            }
        } else {
            Read-Host "Press Enter after build completes..."
        }
    } else {
        Write-ColorOutput "Waiting 5 minutes..." "Yellow"
        Start-Sleep -Seconds 300
    }
}

# 步骤 6: 下载 GitHub Release 文件
if ($startStep -le 6) {
    Write-Header "[6/8] Download GitHub Release Files"

    # 创建下载目录
    if (Test-Path $DownloadDir) {
        Remove-Item -Path $DownloadDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $DownloadDir | Out-Null

    Write-ColorOutput "Download directory: $DownloadDir" "Cyan"
    Write-ColorOutput "Starting download..." "Cyan"

    try {
        Set-Location $DownloadDir
        gh release download $TagName --repo $GitHubRepo
        Set-Location $ProjectRoot

        if ($LASTEXITCODE -ne 0) {
            Write-ColorOutput "[Error] Download failed" "Red"
            exit 1
        }

        Write-ColorOutput "Files downloaded successfully" "Green"
    } catch {
        Write-ColorOutput "[Error] Download failed: $_" "Red"
        Set-Location $ProjectRoot
        exit 1
    }

    # 列出下载的文件
    Write-Host ""
    Write-ColorOutput "Downloaded files:" "Cyan"
    Get-ChildItem -Path $DownloadDir | ForEach-Object {
        $size = [math]::Round($_.Length / 1MB, 2)
        Write-Host "  - $($_.Name) ($size MB)"
    }
}

# 步骤 7: 同步到 Gitee
if ($startStep -le 7) {
    Write-Header "[7/8] Sync to Gitee Release"

    Write-ColorOutput "Preparing to sync to Gitee..." "Cyan"
    Write-Host ""
    Write-Host "Gitee repo: https://gitee.com/$GiteeRepo"
    Write-Host "Create Release: https://gitee.com/$GiteeRepo/releases/new"
    Write-Host ""

    Write-ColorOutput "[Note] Gitee doesn't support API auto-release, manual operation required" "Yellow"
    Write-Host ""
    Write-Host "Please follow these steps:"
    Write-Host ""
    Write-Host "1. Browser will open Gitee Release creation page"
    Write-Host ""
    Write-Host "2. Fill in:"
    Write-Host "   - Tag name: $TagName"
    Write-Host "   - Release title: $TagName"
    Write-Host "   - Release description: (optional, copy from GitHub Release)"
    Write-Host ""
    Write-Host "3. Upload files:"
    Write-Host "   - Click upload attachment"
    Write-Host "   - Select all files in $DownloadDir"
    Write-Host "   - Wait for upload"
    Write-Host ""
    Write-Host "4. Click publish"
    Write-Host ""

    # 打开浏览器
    $openBrowser = Read-Host "Open Gitee Release creation page in browser? (Y/N)"
    if ($openBrowser -eq "Y" -or $openBrowser -eq "y") {
        Start-Process "https://gitee.com/$GiteeRepo/releases/new"
        Start-Sleep -Seconds 2
        # 打开文件资源管理器
        $fullPath = Resolve-Path $DownloadDir
        Start-Process "explorer.exe" -ArgumentList $fullPath
    }

    Write-Host ""
    Read-Host "Press Enter after creating Gitee Release..."
}

# 步骤 8: 验证
if ($startStep -le 8) {
    Write-Header "[8/8] Verify Release"

    Write-ColorOutput "GitHub Release: https://github.com/$GitHubRepo/releases/tag/$TagName" "Green"
    Write-ColorOutput "Gitee Release: https://gitee.com/$GiteeRepo/releases/tag/$TagName" "Green"

    Write-Host ""
    Write-ColorOutput "Release completed!" "Green"
    Write-Host ""

    # 清理
    $cleanup = Read-Host "Delete temporary download directory? (Y/N)"
    if ($cleanup -eq "Y" -or $cleanup -eq "y") {
        Remove-Item -Path $DownloadDir -Recurse -Force
        Write-ColorOutput "Temporary files cleaned up" "Green"
    }

    Write-Host ""
    Write-ColorOutput "Thank you for using Storyboard release script!" "Cyan"
}