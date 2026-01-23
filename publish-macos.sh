#!/bin/bash
# macOS 发布脚本
# 用法: ./publish-macos.sh [版本号]
# 示例: ./publish-macos.sh 1.1.4

set -e  # 遇到错误立即退出

# 颜色输出
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}分镜大师 - macOS 发布脚本${NC}"
echo -e "${GREEN}========================================${NC}"

# 检查 VLC 是否已安装（运行时需要）
echo ""
echo -e "${YELLOW}检查系统依赖...${NC}"
VLC_FOUND=false
if [ -d "/Applications/VLC.app/Contents/MacOS/lib" ]; then
    echo -e "${GREEN}✓ 找到 VLC.app${NC}"
    VLC_FOUND=true
elif [ -d "/opt/homebrew/lib" ] && [ -f "/opt/homebrew/lib/libvlc.dylib" ]; then
    echo -e "${GREEN}✓ 找到 Homebrew VLC (ARM64)${NC}"
    VLC_FOUND=true
elif [ -d "/usr/local/lib" ] && [ -f "/usr/local/lib/libvlc.dylib" ]; then
    echo -e "${GREEN}✓ 找到 Homebrew VLC (Intel)${NC}"
    VLC_FOUND=true
fi

if [ "$VLC_FOUND" = false ]; then
    echo -e "${RED}⚠️  警告: 未找到 VLC 安装${NC}"
    echo -e "${YELLOW}应用运行需要 VLC。请安装:${NC}"
    echo -e "  ${YELLOW}brew install --cask vlc${NC}"
    echo ""
    read -p "是否继续发布? (y/N) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

# 获取版本号
VERSION=${1:-"1.1.3"}
echo -e "${YELLOW}版本号: ${VERSION}${NC}"

# 检测 Mac 芯片类型
ARCH=$(uname -m)
if [ "$ARCH" = "arm64" ]; then
    RUNTIME="osx-arm64"
    ARCH_NAME="Apple Silicon"
else
    RUNTIME="osx-x64"
    ARCH_NAME="Intel"
fi

echo -e "${YELLOW}目标平台: macOS ${ARCH_NAME} (${RUNTIME})${NC}"

# 输出目录
OUTPUT_DIR="./publish/macos-${RUNTIME}"
ZIP_NAME="Storyboard-macOS-${RUNTIME}-v${VERSION}.zip"

echo ""
echo -e "${GREEN}步骤 1/5: 清理旧的构建${NC}"
rm -rf "$OUTPUT_DIR"
rm -f "./publish/${ZIP_NAME}"

echo ""
echo -e "${GREEN}步骤 2/5: 还原依赖${NC}"
dotnet restore Storyboard.csproj

echo ""
echo -e "${GREEN}步骤 3/5: 构建项目${NC}"
dotnet build Storyboard.csproj --configuration Release

echo ""
echo -e "${GREEN}步骤 4/5: 发布应用${NC}"
dotnet publish Storyboard.csproj \
    --configuration Release \
    --runtime "$RUNTIME" \
    --self-contained true \
    --output "$OUTPUT_DIR" \
    /p:PublishSingleFile=false \
    /p:PublishTrimmed=false

# 确保配置文件随包发布
cp -f "./appsettings.json" "$OUTPUT_DIR/appsettings.json"

echo ""
echo -e "${GREEN}步骤 5/5: 打包 ZIP${NC}"

# 创建 publish 目录（如果不存在）
mkdir -p ./publish

# 进入输出目录打包
cd "$OUTPUT_DIR"
zip -r "../../publish/${ZIP_NAME}" . -x "*.pdb" -x "*.xml"
cd ../..

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}✅ 发布完成！${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo -e "📦 输出文件: ${YELLOW}./publish/${ZIP_NAME}${NC}"
echo -e "📂 输出目录: ${YELLOW}${OUTPUT_DIR}${NC}"
echo ""
echo -e "${YELLOW}⚠️  注意事项:${NC}"
echo -e "1. ${RED}必须先安装 VLC:${NC} ${YELLOW}brew install --cask vlc${NC}"
echo -e "2. 首次运行需要右键 → 打开（macOS 安全限制）"
echo -e "3. 需要安装 FFmpeg: ${YELLOW}brew install ffmpeg${NC}"
echo ""
echo -e "${GREEN}运行应用:${NC}"
echo -e "  cd ${OUTPUT_DIR}"
echo -e "  ./Storyboard"
echo ""
