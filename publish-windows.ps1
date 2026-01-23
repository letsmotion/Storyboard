# Windows 发布脚本
# 用法: .\publish-windows.ps1 [-Version "1.1.4"]
# 示例: .\publish-windows.ps1 -Version "1.1.4"

param(
    [string]$Version = "1.1.3",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Green
Write-Host "分镜大师 - Windows 发布脚本" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "版本号: $Version" -ForegroundColor Yellow
Write-Host "目标平台: Windows x64 ($Runtime)" -ForegroundColor Yellow

# 输出目录
$OutputDir = "./publish/windows-$Runtime"
$ZipName = "Storyboard-Windows-$Runtime-v$Version.zip"

Write-Host ""
Write-Host "步骤 1/5: 清理旧的构建" -ForegroundColor Green
if (Test-Path $OutputDir) {
    Remove-Item -Path $OutputDir -Recurse -Force
}
if (Test-Path "./publish/$ZipName") {
    Remove-Item -Path "./publish/$ZipName" -Force
}

Write-Host ""
Write-Host "步骤 2/5: 还原依赖" -ForegroundColor Green
dotnet restore Storyboard.csproj --runtime $Runtime

Write-Host ""
Write-Host "步骤 3/5: 构建项目" -ForegroundColor Green
dotnet build Storyboard.csproj --configuration Release --no-restore

Write-Host ""
Write-Host "步骤 4/5: 发布应用" -ForegroundColor Green
dotnet publish Storyboard.csproj `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    --output $OutputDir `
    /p:PublishSingleFile=false `
    /p:PublishTrimmed=false

Write-Host ""
Write-Host "步骤 5/5: 打包 ZIP" -ForegroundColor Green

# 创建 publish 目录（如果不存在）
New-Item -ItemType Directory -Path "./publish" -Force | Out-Null

# 打包（排除 .pdb 和 .xml 文件）
Compress-Archive -Path "$OutputDir\*" -DestinationPath "./publish/$ZipName" -Force

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "✅ 发布完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "📦 输出文件: " -NoNewline
Write-Host "./publish/$ZipName" -ForegroundColor Yellow
Write-Host "📂 输出目录: " -NoNewline
Write-Host "$OutputDir" -ForegroundColor Yellow
Write-Host ""
Write-Host "⚠️  注意事项:" -ForegroundColor Yellow
Write-Host "1. 需要安装 .NET 8 Desktop Runtime"
Write-Host "2. FFmpeg 已内置在 Tools/ffmpeg 目录"
Write-Host "3. LibVLC 已包含在发布包中"
Write-Host ""
Write-Host "运行应用:" -ForegroundColor Green
Write-Host "  cd $OutputDir"
Write-Host "  .\Storyboard.exe"
Write-Host ""
