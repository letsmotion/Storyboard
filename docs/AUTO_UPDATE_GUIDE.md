# 自动更新使用指南

## 📦 功能说明

本项目已集成 **Velopack** 自动更新功能，用户安装后会自动检查并提示更新。

## 🎯 核心特性

- ✅ **自动检查更新**：应用启动 3 秒后自动检查
- ✅ **增量更新**：只下载变化的部分，节省流量
- ✅ **后台下载**：不影响用户使用
- ✅ **一键安装**：下载完成后一键重启更新
- ✅ **版本显示**：状态栏显示当前版本号
- ✅ **跨平台支持**：Windows/Mac/Linux

## 🚀 发布新版本流程

### 方式一：使用 Git 标签（推荐）

1. **更新版本号并提交代码**
```bash
git add .
git commit -m "准备发布 v1.0.1"
```

2. **创建并推送标签**
```bash
# 创建标签（版本号格式：v主版本.次版本.修订号）
git tag v1.0.1

# 推送标签到 GitHub
git push origin v1.0.1
```

3. **自动构建发布**
   - GitHub Actions 会自动触发构建
   - 自动打包 Velopack 安装包
   - 自动创建 GitHub Release
   - 用户端会自动检测到新版本

### 方式二：手动发布

1. **安装 Velopack 工具**
```bash
dotnet tool install -g vpk
```

2. **发布应用**
```bash
dotnet publish -c Release -o ./publish
```

3. **打包更新**
```bash
vpk pack `
  --packId Storyboard `
  --packVersion 1.0.1 `
  --packDir ./publish `
  --mainExe Storyboard.exe `
  --outputDir ./releases `
  --icon ./App/Assets/icon.ico
```

4. **上传到 GitHub Releases**
   - 在 GitHub 仓库创建新 Release
   - 上传 `./releases` 目录下的所有文件
   - 标签名必须是 `v1.0.1` 格式

## ⚙️ 配置说明

### 修改 GitHub 仓库地址

打开 [Application/Services/UpdateService.cs](Application/Services/UpdateService.cs:11)，修改第 11 行：

```csharp
private const string GitHubRepoUrl = "https://github.com/YOUR_USERNAME/YOUR_REPO";
```

将 `YOUR_USERNAME` 和 `YOUR_REPO` 替换为你的实际仓库地址。

### 版本号规范

使用语义化版本号：`v主版本.次版本.修订号`

- **主版本**：重大功能变更或不兼容的 API 修改
- **次版本**：新增功能，向下兼容
- **修订号**：Bug 修复，向下兼容

示例：`v1.0.0` → `v1.0.1` → `v1.1.0` → `v2.0.0`

## 📋 用户体验流程

1. **用户启动应用**
   - 应用正常启动，3 秒后后台检查更新

2. **发现新版本**
   - 顶部显示蓝色通知栏
   - 显示新版本号和当前版本号
   - 提供"立即更新"和"关闭"按钮

3. **用户点击"立即更新"**
   - 显示下载进度条
   - 下载完成后自动重启应用
   - 重启后即为新版本

4. **用户点击"关闭"**
   - 隐藏通知栏
   - 下次启动时再次检查

## 🔧 开发调试

### 本地测试更新功能

Velopack 只在通过安装程序安装后才会启用更新功能。开发环境下：

```csharp
// UpdateService.cs 会自动检测
if (VelopackApp.IsInstalled)
{
    // 启用更新功能
}
else
{
    // 开发环境，跳过更新
}
```

### 创建测试安装包

```bash
# 1. 发布应用
dotnet publish -c Release -o ./publish

# 2. 创建安装包
vpk pack --packId Storyboard --packVersion 1.0.0 --packDir ./publish --mainExe Storyboard.exe --outputDir ./releases

# 3. 安装测试
./releases/StoryboardSetup.exe
```

## 🛠️ 故障排查

### 问题 1：更新检查失败

**原因**：GitHub API 访问受限或仓库地址错误

**解决**：
1. 检查 `UpdateService.cs` 中的仓库地址是否正确
2. 确认 GitHub Release 已正确创建
3. 检查网络连接

### 问题 2：下载更新失败

**原因**：网络问题或 Release 文件不完整

**解决**：
1. 检查 GitHub Release 中是否包含所有必需文件
2. 重试下载
3. 考虑使用国内 CDN 加速（可选）

### 问题 3：安装更新失败

**原因**：权限不足或文件被占用

**解决**：
1. 以管理员身份运行应用
2. 关闭杀毒软件临时防护
3. 确保应用目录有写入权限

## 📊 更新统计

Velopack 会自动记录更新日志，位于：
```
%LOCALAPPDATA%\Storyboard\logs\
```

## 🔐 安全说明

- ✅ 所有更新包通过 HTTPS 下载
- ✅ 更新包来自官方 GitHub Release
- ✅ 支持数字签名验证（可选配置）

## 📚 更多资源

- [Velopack 官方文档](https://velopack.io/)
- [GitHub Actions 文档](https://docs.github.com/actions)
- [语义化版本规范](https://semver.org/lang/zh-CN/)

## 💡 最佳实践

1. **定期发布更新**：建议每 2-4 周发布一次更新
2. **编写更新日志**：在 GitHub Release 中详细说明更新内容
3. **测试后发布**：在测试环境充分测试后再发布
4. **保持向下兼容**：避免破坏性更新，或提前通知用户
5. **监控更新率**：关注用户更新情况，及时处理问题

---

**提示**：首次发布时，建议先创建 `v1.0.0` 版本作为基准版本。
