@echo off
echo ========================================
echo Timeline 功能快速验证脚本
echo ========================================
echo.

REM 设置项目目录（请根据实际情况修改）
set PROJECT_DIR=C:\Users\Administrator\AppData\Local\Storyboard\projects

echo 1. 检查最近的项目目录...
dir /b /ad /o-d "%PROJECT_DIR%" | findstr /v "^$" > temp_projects.txt
set /p LATEST_PROJECT=<temp_projects.txt
del temp_projects.txt

if "%LATEST_PROJECT%"=="" (
    echo [错误] 未找到项目目录
    pause
    exit /b 1
)

echo    最新项目: %LATEST_PROJECT%
echo.

set DRAFT_DIR=%PROJECT_DIR%\%LATEST_PROJECT%\draft

echo 2. 检查草稿目录...
if exist "%DRAFT_DIR%" (
    echo    [✓] 草稿目录存在: %DRAFT_DIR%
) else (
    echo    [✗] 草稿目录不存在
    echo    提示: 请先打开项目并切换到时间轴视图
    pause
    exit /b 1
)
echo.

echo 3. 检查草稿文件...
if exist "%DRAFT_DIR%\draft_content.json" (
    echo    [✓] draft_content.json 存在
    for %%A in ("%DRAFT_DIR%\draft_content.json") do echo       大小: %%~zA 字节
) else (
    echo    [✗] draft_content.json 不存在
)

if exist "%DRAFT_DIR%\draft_meta_info.json" (
    echo    [✓] draft_meta_info.json 存在
    for %%A in ("%DRAFT_DIR%\draft_meta_info.json") do echo       大小: %%~zA 字节
) else (
    echo    [✗] draft_meta_info.json 不存在
)
echo.

echo 4. 显示 draft_content.json 内容摘要...
if exist "%DRAFT_DIR%\draft_content.json" (
    powershell -Command "$json = Get-Content '%DRAFT_DIR%\draft_content.json' | ConvertFrom-Json; Write-Host '   草稿 ID:' $json.id; Write-Host '   项目名称:' $json.name; Write-Host '   总时长:' ($json.duration / 1000000) '秒'; Write-Host '   轨道数:' $json.tracks.Count; Write-Host '   片段数:' ($json.tracks | ForEach-Object { $_.segments.Count } | Measure-Object -Sum).Sum"
)
echo.

echo 5. 检查日志文件...
set LOG_DIR=C:\Users\Administrator\AppData\Local\Storyboard\logs
if exist "%LOG_DIR%" (
    echo    [✓] 日志目录存在
    dir /b /o-d "%LOG_DIR%\app-*.log" | findstr /v "^$" > temp_logs.txt
    set /p LATEST_LOG=<temp_logs.txt
    del temp_logs.txt

    if not "%LATEST_LOG%"=="" (
        echo    最新日志: %LATEST_LOG%
        echo.
        echo    最近的草稿相关日志:
        findstr /i "草稿 draft 同步" "%LOG_DIR%\%LATEST_LOG%" | findstr /v "^$" > temp_draft_logs.txt
        type temp_draft_logs.txt | more
        del temp_draft_logs.txt
    )
)
echo.

echo ========================================
echo 验证完成！
echo ========================================
echo.
echo 下一步:
echo 1. 如果草稿文件存在，说明功能正常
echo 2. 可以在 CapCut 中打开草稿目录测试
echo 3. 查看完整文档: docs\timeline-testing-guide.md
echo.
pause
