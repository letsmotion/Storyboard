# 发布指南

本文档说明如何为不同平台构建和发布分镜大师应用。

## 前置要求

- .NET 8.0 SDK
- Git

### macOS 额外要求
- Homebrew（用于安装依赖）
- FFmpeg: `brew install ffmpeg`
- VLC: `brew install --cask vlc`

### Windows 额外要求
- PowerShell 5.1 或更高版本

---

## macOS 发布

### 1. 使用发布脚本（推荐）

```bash
# 赋予执行权限
chmod +x publish-macos.sh

# 发布（自动检测芯片类型）
./publish-macos.sh 1.1.4

# 输出文件
# - Apple Silicon: ./publish/Storyboard-macOS-osx-arm64-v1.1.4.zip
# - Intel Mac: ./publish/Storyboard-macOS-osx-x64-v1.1.4.zip
```

### 2. 手动发布

```bash
# Apple Silicon (M1/M2/M3)
dotnet publish Storyboard.csproj \
    --configuration Release \
    --runtime osx-arm64 \
    --self-contained true \
    --output ./publish/macos-arm64

# Intel Mac
dotnet publish Storyboard.csproj \
    --configuration Release \
    --runtime osx-x64 \
    --self-contained true \
    --output ./publish/macos-x64

# 打包
cd ./publish/macos-arm64
zip -r ../Storyboard-macOS-arm64.zip . -x "*.pdb" -x "*.xml"
```

### 3. 运行应用

```bash
cd ./publish/macos-arm64
./Storyboard
```

**首次运行注意事项：**
- macOS 会提示"无法验证开发者"
- 右键点击应用 → 选择"打开"
- 点击"打开"确认

---

## Windows 发布

### 1. 使用发布脚本（推荐）

```powershell
# 发布
.\publish-windows.ps1 -Version "1.1.4"

# 输出文件
# ./publish/Storyboard-Windows-win-x64-v1.1.4.zip
```

### 2. 手动发布

```powershell
# Windows x64
dotnet publish Storyboard.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output ./publish/windows-x64

# 打包
Compress-Archive -Path ./publish/windows-x64/* -DestinationPath ./publish/Storyboard-Windows-x64.zip
```

### 3. 运行应用

```powershell
cd ./publish/windows-x64
.\Storyboard.exe
```

---

## 已知问题和限制

### macOS 平台

#### ⚠️ LibVLC 依赖问题
当前项目引用了 `VideoLAN.LibVLC.Windows` 包，在 macOS 上会有警告。

**解决方案：**
修改 `Storyboard.csproj`，添加条件引用：

```xml
<ItemGroup>
  <!-- 移除 -->
  <!-- <PackageReference Include="VideoLAN.LibVLC.Windows" Version="3.0.23" /> -->

  <!-- 添加条件引用 -->
  <PackageReference Include="VideoLAN.LibVLC.Mac" Version="3.0.23"
                    Condition="$(RuntimeIdentifier.StartsWith('osx'))" />
  <PackageReference Include="VideoLAN.LibVLC.Windows" Version="3.0.23"
                    Condition="$(RuntimeIdentifier.StartsWith('win'))" />
</ItemGroup>
```

#### ⚠️ FFmpeg 路径问题
项目中的 `Tools/ffmpeg` 包含 Windows 版本的 ffmpeg.exe。

**解决方案：**
macOS 用户需要通过 Homebrew 安装：
```bash
brew install ffmpeg
```

应用会自动使用系统的 FFmpeg。

#### ⚠️ 自动更新功能
macOS 版本**不支持**自动更新，需要手动下载新版本。

---

## 发布检查清单

### 发布前
- [ ] 更新版本号（`Storyboard.csproj` 中的 `<Version>`）
- [ ] 测试应用在目标平台上能否正常运行
- [ ] 检查依赖项是否完整

### 发布后
- [ ] 测试 zip 包解压后能否运行
- [ ] 验证所有核心功能正常
- [ ] 更新 Release Notes

---

## 分发建议

### macOS
- 提供两个版本：Apple Silicon (arm64) 和 Intel (x64)
- 在 README 中说明首次运行的安全提示处理方法
- 说明需要安装 FFmpeg 和 VLC

### Windows
- 提供 x64 版本
- 说明需要 .NET 8 Desktop Runtime（或使用自包含发布）
- FFmpeg 和 LibVLC 已内置

---

## 常见问题

### Q: 为什么 macOS 版本没有自动更新？
A: 当前自动更新功能仅支持 Windows 平台（使用 Velopack）。macOS 用户需要手动下载更新。

### Q: 如何减小发布包体积？
A: 可以使用以下选项：
```bash
dotnet publish \
    --configuration Release \
    --runtime osx-arm64 \
    --self-contained true \
    /p:PublishTrimmed=true \
    /p:PublishSingleFile=false
```

**注意：** 启用 `PublishTrimmed` 可能导致反射相关功能失效，需要充分测试。

### Q: 如何创建 .app bundle（macOS）？
A: 需要额外的打包工具，如 `dotnet-bundle` 或手动创建 .app 结构。这超出了本文档范围。

---

## 技术支持

如有问题，请提交 GitHub Issue 或联系开发者。
