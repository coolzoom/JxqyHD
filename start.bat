@echo off
rem 一键配置环境、编译并启动游戏/工具 (Windows)
setlocal enabledelayedexpansion
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo 错误: 未找到 dotnet，请先安装 .NET 10 SDK
    pause
    exit /b 1
)

echo ==^> 还原 dotnet 工具 (mgcb)...
dotnet tool restore
if errorlevel 1 goto :fail

echo ==^> 编译整个解决方案...
dotnet build Jxqy.slnx
if errorlevel 1 goto :fail

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
echo   0) 退出
set /p choice=选择:
if "%choice%"=="1" (
    pushd Data
    dotnet "..\%GAME_DLL%"
    popd
) else if "%choice%"=="2" (
    dotnet "%SETTINGS_DLL%"
) else if "%choice%"=="3" (
    dotnet "%EDITOR_DLL%"
) else if "%choice%"=="4" (
    dotnet "%FORMATTER_DLL%"
) else if "%choice%"=="5" (
    pushd Data
    dotnet "..\%GAME_DLL%" --edit
    popd
) else if "%choice%"=="0" (
    exit /b 0
) else (
    echo 无效选择
)
goto :menu

:fail
echo 构建失败
pause
exit /b 1
