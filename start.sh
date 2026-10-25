#!/usr/bin/env bash
# 剑侠情缘 跨平台启动菜单 (macOS/Linux)
set -e
cd "$(dirname "$0")"

CFG=Debug
GAME_DLL="Jxqy/bin/$CFG/net10.0/Jxqy.dll"
SETTINGS_DLL="Settings/bin/$CFG/net10.0/Settings.dll"
EDITOR_DLL="GameEditor/bin/$CFG/net10.0/GameEditor.dll"
FORMATTER_DLL="xnbbuilder/XNAFormatter/bin/$CFG/net10.0/XNAFormatter.dll"

setup_env() {
    echo "==> 检查 .NET SDK..."
    if ! command -v dotnet >/dev/null 2>&1; then
        echo "错误: 未找到 dotnet，请先安装 .NET 10 SDK"
        return 1
    fi
    dotnet --version
    echo "==> 还原 dotnet 工具 (mgcb)..."
    dotnet tool restore
    echo "==> 检查 ffmpeg (视频播放依赖)..."
    if command -v ffmpeg >/dev/null 2>&1 && command -v ffprobe >/dev/null 2>&1; then
        echo "ffmpeg: $(ffmpeg -version 2>/dev/null | head -1)"
    else
        echo "警告: 未找到 ffmpeg/ffprobe，游戏内电影将无法播放 (可跳过)"
        echo "      macOS: brew install ffmpeg   Ubuntu: sudo apt install ffmpeg"
    fi
    echo "==> 环境设置完成"
}

build_all() {
    if ! command -v dotnet >/dev/null 2>&1; then
        echo "错误: 未找到 dotnet，请先安装 .NET 10 SDK (菜单 6)"
        return 1
    fi
    echo "==> 编译整个解决方案..."
    dotnet build Jxqy.slnx
}

run_game() {
    if [ ! -f "$GAME_DLL" ]; then echo "未编译，请先执行 7) 一键编译"; return; fi
    (cd Data && dotnet "../$GAME_DLL" ${1:+$1})
}

run_dll() {
    if [ ! -f "$1" ]; then echo "未编译，请先执行 7) 一键编译"; return; fi
    dotnet "$1"
}

while true; do
    echo
    echo "===== 剑侠情缘 菜单 ====="
    echo "  1) 启动游戏"
    echo "  2) 游戏设置"
    echo "  3) 游戏调试 (GameEditor)"
    echo "  4) 内容构建器 (XNAFormatter)"
    echo "  5) 启动游戏 (编辑模式, 供 GameEditor 连接)"
    echo "  6) 环境一键设置"
    echo "  7) 一键编译"
    echo "  0) 退出"
    read -r -p "选择: " choice
    case "$choice" in
        1) run_game ;;
        2) run_dll "$SETTINGS_DLL" ;;
        3) run_dll "$EDITOR_DLL" ;;
        4) run_dll "$FORMATTER_DLL" ;;
        5) run_game --edit ;;
        6) setup_env || true ;;
        7) build_all || true ;;
        0) exit 0 ;;
        *) echo "无效选择" ;;
    esac
done
