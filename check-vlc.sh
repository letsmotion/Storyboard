#!/bin/bash
# VLC 安装检查脚本

echo "=== VLC 安装检查 ==="
echo ""

echo "1. 检查 VLC.app:"
if [ -d "/Applications/VLC.app" ]; then
    echo "  ✓ /Applications/VLC.app 存在"
    echo "  查找 libvlc 文件:"
    find /Applications/VLC.app -name "libvlc*.dylib" 2>/dev/null
else
    echo "  ✗ /Applications/VLC.app 不存在"
fi

echo ""
echo "2. 检查 Homebrew (ARM64):"
if [ -d "/opt/homebrew" ]; then
    echo "  ✓ /opt/homebrew 存在"
    echo "  查找 libvlc 文件:"
    find /opt/homebrew -name "libvlc*.dylib" 2>/dev/null | head -10
else
    echo "  ✗ /opt/homebrew 不存在"
fi

echo ""
echo "3. 检查 Homebrew (Intel):"
if [ -d "/usr/local" ]; then
    echo "  ✓ /usr/local 存在"
    echo "  查找 libvlc 文件:"
    find /usr/local -name "libvlc*.dylib" 2>/dev/null | head -10
else
    echo "  ✗ /usr/local 不存在"
fi

echo ""
echo "4. 系统架构:"
uname -m

echo ""
echo "5. 检查 VLC 是否通过 Homebrew 安装:"
if command -v brew &> /dev/null; then
    echo "  Homebrew 已安装"
    if brew list --cask vlc &> /dev/null; then
        echo "  ✓ VLC 已通过 Homebrew 安装"
        brew info --cask vlc
    else
        echo "  ✗ VLC 未通过 Homebrew 安装"
    fi
else
    echo "  ✗ Homebrew 未安装"
fi
