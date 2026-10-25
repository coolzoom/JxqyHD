#!/usr/bin/env bash
# 一键配置环境、编译并启动游戏/工具 (macOS/Linux)
set -e
cd "$(dirname "$0")"

if ! command -v dotnet >/dev/null 2>&1; then
    echo "错误: 未找到 dotnet，请先安装 .NET 10 SDK"
    exit 1
fi

echo "==> 还原 dotnet 工具 (mgcb)..."
dotnet tool restore

echo "==> 编译整个解决方案..."
dotnet build Jxqy.slnx

CFG=Debug
GAME_DLL="Jxqy/bin/$CFG/net10.0/Jxqy.dll"
SETTINGS_DLL="Settings/bin/$CFG/net10.0/Settings.dll"
EDITOR_DLL="GameEditor/bin/$CFG/net10.0/GameEditor.dll"
FORMATTER_DLL="xnbbuilder/XNAFormatter/bin/$CFG/net10.0/XNAFormatter.dll"
BUILDER_DLL="xnbbuilder/xnaconverter/bin/$CFG/net10.0/XNABuilder.dll"

run_dll() {
    # 工具自行定位 Data 目录，直接运行
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
    echo "  0) 退出"
    read -r -p "选择: " choice
    case "$choice" in
        1) (cd Data && dotnet "../$GAME_DLL") ;;
        2) run_dll "$SETTINGS_DLL" ;;
        3) run_dll "$EDITOR_DLL" ;;
        4) run_dll "$FORMATTER_DLL" ;;
        5) (cd Data && dotnet "../$GAME_DLL" --edit) ;;
        0) exit 0 ;;
        *) echo "无效选择" ;;
    esac
done
