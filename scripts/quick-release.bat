@echo off
chcp 65001 >nul
echo ================================================================================
echo                    分镜大师 - 快速发布脚本
echo ================================================================================
echo.

REM 项目文件路径
set CSPROJ_FILE=Storyboard.csproj

REM 获取当前版本号
echo [0/6] 确定版本号
echo.

REM 尝试从 .csproj 文件读取当前版本号
set CURRENT_VERSION=
for /f "tokens=2 delims=<>" %%a in ('findstr "<Version>" %CSPROJ_FILE% 2^>nul') do set CURRENT_VERSION=%%a

if not "%CURRENT_VERSION%"=="" (
    echo 当前版本: %CURRENT_VERSION%
) else (
    echo 未找到版本号，将从 1.0.0 开始
)

echo.
REM 获取新版本号
set /p VERSION="请输入新版本号 (例如: 1.0.0): "
if "%VERSION%"=="" (
    echo [错误] 版本号不能为空
    pause
    exit /b 1
)

echo.
echo 准备发布版本: v%VERSION%
echo.

REM 步骤 1: 更新项目文件中的版本号
echo ================================================================================
echo [1/6] 更新项目文件版本号
echo ================================================================================

REM 检查文件是否存在
if not exist "%CSPROJ_FILE%" (
    echo [错误] 找不到项目文件: %CSPROJ_FILE%
    pause
    exit /b 1
)

REM 创建临时文件
set TEMP_FILE=%CSPROJ_FILE%.tmp

REM 检查是否已有 Version 标签
findstr "<Version>" %CSPROJ_FILE% >nul
if errorlevel 1 (
    REM 没有 Version 标签，需要添加
    (
        for /f "delims=" %%i in (%CSPROJ_FILE%) do (
            echo %%i
            echo %%i | findstr "<PropertyGroup>" >nul
            if not errorlevel 1 (
                echo     ^<^!-- 版本号：发布脚本会自动更新此值 --^>
                echo     ^<Version^>%VERSION%^</Version^>
                echo     ^<AssemblyVersion^>%VERSION%^</AssemblyVersion^>
                echo     ^<FileVersion^>%VERSION%^</FileVersion^>
            )
        )
    ) > %TEMP_FILE%
) else (
    REM 已有 Version 标签，替换它们
    (
        for /f "delims=" %%i in (%CSPROJ_FILE%) do (
            set "line=%%i"
            setlocal enabledelayedexpansion
            echo !line! | findstr "<Version>" >nul
            if not errorlevel 1 (
                echo     ^<Version^>%VERSION%^</Version^>
            ) else (
                echo !line! | findstr "<AssemblyVersion>" >nul
                if not errorlevel 1 (
                    echo     ^<AssemblyVersion^>%VERSION%^</AssemblyVersion^>
                ) else (
                    echo !line! | findstr "<FileVersion>" >nul
                    if not errorlevel 1 (
                        echo     ^<FileVersion^>%VERSION%^</FileVersion^>
                    ) else (
                        echo !line!
                    )
                )
            )
            endlocal
        )
    ) > %TEMP_FILE%
)

REM 替换原文件
move /y %TEMP_FILE% %CSPROJ_FILE% >nul

echo 已更新 %CSPROJ_FILE% 中的版本号为: %VERSION%
echo.

echo 将执行以下操作：
echo   1. 已更新 %CSPROJ_FILE% 中的版本号
echo   2. 提交版本号更改
echo   3. 创建版本标签 v%VERSION%
echo   4. 推送到 GitHub（触发自动构建）
echo   5. 等待 GitHub Actions 构建完成
echo   6. 提示手动同步到 Gitee
echo.

set /p CONFIRM="是否继续? (Y/N): "
if /i not "%CONFIRM%"=="Y" (
    echo 操作已取消，恢复版本号更改...
    git checkout %CSPROJ_FILE%
    pause
    exit /b 0
)

echo.
echo ================================================================================
echo [2/6] 检查 Git 状态
echo ================================================================================
git status

echo.
set /p HAS_CHANGES="是否有未提交的更改? (Y/N): "

if /i "%HAS_CHANGES%"=="Y" (
    echo.
    echo ================================================================================
    echo [3/6] 提交更改
    echo ================================================================================

    set COMMIT_MSG=发布版本 v%VERSION%
    echo 提交信息: %COMMIT_MSG%

    git add .
    git commit -m "%COMMIT_MSG%"

    if errorlevel 1 (
        echo [错误] Git 提交失败
        pause
        exit /b 1
    )

    echo.
    echo 推送到 GitHub...
    git push

    if errorlevel 1 (
        echo [错误] 推送失败
        pause
        exit /b 1
    )

    echo 推送成功
) else (
    echo [3/6] 跳过提交（无其他更改）
)

echo.
echo ================================================================================
echo [4/6] 创建并推送版本标签
echo ================================================================================

REM 检查标签是否已存在
git tag -l "v%VERSION%" | findstr "v%VERSION%" >nul
if not errorlevel 1 (
    echo [警告] 标签 v%VERSION% 已存在
    set /p DELETE_TAG="是否删除旧标签并重新创建? (Y/N): "
    if /i "%DELETE_TAG%"=="Y" (
        git tag -d v%VERSION%
        git push origin :refs/tags/v%VERSION%
    ) else (
        echo 操作已取消。
        pause
        exit /b 0
    )
)

REM 创建标签
git tag v%VERSION%
if errorlevel 1 (
    echo [错误] 创建标签失败
    pause
    exit /b 1
)

echo 标签 v%VERSION% 创建成功

REM 推送标签
git push origin v%VERSION%
if errorlevel 1 (
    echo [错误] 推送标签失败
    pause
    exit /b 1
)

echo 标签已推送到 GitHub，GitHub Actions 将自动开始构建

echo.
echo ================================================================================
echo [5/6] 等待 GitHub Actions 构建
echo ================================================================================
echo.
echo GitHub Actions 正在构建中...
echo.
echo 请访问以下链接查看构建进度：
echo https://github.com/BroderQi/Storyboard/actions
echo.
echo 构建完成后，Release 将自动创建：
echo https://github.com/BroderQi/Storyboard/releases/tag/v%VERSION%
echo.

set /p WAIT_BUILD="是否等待构建完成后继续? (Y/N): "
if /i "%WAIT_BUILD%"=="Y" (
    echo.
    echo 请在浏览器中查看构建进度，构建完成后按任意键继续...
    pause >nul
)

echo.
echo ================================================================================
echo [6/6] 发布完成
echo ================================================================================
echo.
echo 版本: v%VERSION%
echo GitHub Release: https://github.com/BroderQi/Storyboard/releases/tag/v%VERSION%
echo.
echo ================================================================================
echo                      下一步：同步到 Gitee
echo ================================================================================
echo.
echo 请按照以下步骤手动同步到 Gitee：
echo.
echo 1. 下载 GitHub Release 文件
echo    访问: https://github.com/BroderQi/Storyboard/releases/tag/v%VERSION%
echo    下载以下文件：
echo      - StoryboardSetup.exe
echo      - RELEASES
echo      - Storyboard-%VERSION%-full.nupkg
echo.
echo 2. 在 Gitee 创建 Release
echo    访问: https://gitee.com/nan1314/Storyboard/releases/new
echo    - 标签名称: v%VERSION%
echo    - 发行版标题: v%VERSION%
echo    - 上传下载的所有文件
echo    - 点击"发布"
echo.
echo 3. 验证 Gitee Release
echo    访问: https://gitee.com/nan1314/Storyboard/releases
echo    确认文件可以正常下载
echo.
echo ================================================================================
echo.
echo 提示：你也可以使用 PowerShell 脚本自动同步到 Gitee
echo 运行: .\scripts\sync-to-gitee.ps1 -Version %VERSION%
echo.
echo ================================================================================
echo.

set /p OPEN_GITHUB="是否在浏览器中打开 GitHub Release 页面? (Y/N): "
if /i "%OPEN_GITHUB%"=="Y" (
    start https://github.com/BroderQi/Storyboard/releases/tag/v%VERSION%
)

set /p OPEN_GITEE="是否在浏览器中打开 Gitee Release 创建页面? (Y/N): "
if /i "%OPEN_GITEE%"=="Y" (
    start https://gitee.com/nan1314/Storyboard/releases/new
)

echo.
echo 发布流程完成！
echo.
pause
