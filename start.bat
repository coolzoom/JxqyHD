@echo off
rem 剑侠情缘 跨平台启动菜单 (Windows)
setlocal enabledelayedexpansion
cd /d "%~dp0"

set CFG=Debug
set GAME_DLL=Jxqy\bin\%CFG%\net10.0\Jxqy.dll
set SETTINGS_DLL=Settings\bin\%CFG%\net10.0\Settings.dll
set EDITOR_DLL=GameEditor\bin\%CFG%\net10.0\GameEditor.dll
set FORMATTER_DLL=xnbbuilder\XNAFormatter\bin\%CFG%\net10.0\XNAFormatter.dll

:menu
echo.
echo ===== 剑侠情缘 菜单 =====
echo   1) 启动游戏
echo   2) 游戏设置
echo   3) 游戏调试 (GameEditor)
echo   4) 内容构建器 (XNAFormatter)
echo   5) 启动游戏 (编辑模式, 供 GameEditor 连接^)
echo   6) 环境一键设置
echo   7) 一键编译
echo   0) 退出
set /p choice=选择:
if "%choice%"=="1" goto :game
if "%choice%"=="2" goto :settings
if "%choice%"=="3" goto :editor
if "%choice%"=="4" goto :formatter
if "%choice%"=="5" goto :gameedit
if "%choice%"=="6" goto :setup
if "%choice%"=="7" goto :build
if "%choice%"=="0" exit /b 0
echo 无效选择
goto :menu

:game
if not exist "%GAME_DLL%" ( echo 未编译，请先执行 7^) 一键编译 & goto :menu )
pushd Data
dotnet "..\%GAME_DLL%"
popd
goto :menu

:gameedit
if not exist "%GAME_DLL%" ( echo 未编译，请先执行 7^) 一键编译 & goto :menu )
pushd Data
dotnet "..\%GAME_DLL%" --edit
popd
goto :menu

:settings
if not exist "%SETTINGS_DLL%" ( echo 未编译，请先执行 7^) 一键编译 & goto :menu )
dotnet "%SETTINGS_DLL%"
goto :menu

:editor
if not exist "%EDITOR_DLL%" ( echo 未编译，请先执行 7^) 一键编译 & goto :menu )
dotnet "%EDITOR_DLL%"
goto :menu

:formatter
if not exist "%FORMATTER_DLL%" ( echo 未编译，请先执行 7^) 一键编译 & goto :menu )
dotnet "%FORMATTER_DLL%"
goto :menu

:setup
echo ==^> 检查 .NET SDK...
where dotnet >nul 2>&1
if errorlevel 1 ( echo 错误: 未找到 dotnet，请先安装 .NET 10 SDK & goto :menu )
dotnet --version
echo ==^> 还原 dotnet 工具 (mgcb)...
dotnet tool restore
if errorlevel 1 echo 警告: 工具还原失败
echo ==^> 检查 ffmpeg (视频播放依赖)...
where ffmpeg >nul 2>&1
if errorlevel 1 (
    echo 警告: 未找到 ffmpeg，游戏内电影将无法播放 (可跳过)
    echo       下载: https://ffmpeg.org/download.html 或 winget install ffmpeg
) else (
    ffmpeg -version 2>nul | findstr /n "^" | findstr "^1:"
)
echo ==^> 环境设置完成
goto :menu

:build
where dotnet >nul 2>&1
if errorlevel 1 ( echo 错误: 未找到 dotnet，请先执行 6^) 环境一键设置 & goto :menu )
echo ==^> 编译整个解决方案...
dotnet build Jxqy.slnx
if errorlevel 1 echo 构建失败
goto :menu
