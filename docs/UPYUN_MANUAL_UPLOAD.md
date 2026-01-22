# 又拍云手动上传指南

由于又拍云配置为公开读取，用户只需要从 CDN 下载更新文件，**无需在代码中配置密钥**。

## 📋 配置信息

- **自定义域名**: `storyboard-release.bkgf.net`
- **桶名称**: `storyboard-release`
- **前缀路径**: `storyboard/`
- **访问权限**: 公开读取（匿名访问）
- **操作员**: `storyboard`（仅用于控制台上传，不在代码中使用）

## 🔒 安全说明

✅ **无需在代码中配置密钥**：
- 用户只需要**下载**更新文件
- 又拍云已配置为**公开读取**
- 应用通过 HTTPS 匿名访问文件
- 操作员密钥仅用于你手动上传文件时使用

## 📁 目录结构

在又拍云中创建以下目录结构：

```
storyboard-release (桶)
└── storyboard/
    ├── RELEASES                              # 版本清单文件
    ├── Storyboard-1.1.2-full.nupkg         # 完整安装包
    └── Storyboard-1.1.2-delta.nupkg        # 增量更新包（可选）
```

**重要**：文件必须放在 `storyboard/` 目录下。

## 🚀 发布流程

### 步骤 1：构建并发布到 GitHub

```powershell
# 运行发布脚本（会自动推送到 GitHub 并触发构建）
.\scripts\quick-release.ps1
```

这会：
1. 更新版本号
2. 提交代码
3. 创建 Git 标签
4. 推送到 GitHub
5. GitHub Actions 自动构建并创建 Release

### 步骤 2：下载 GitHub Release 文件

等待 GitHub Actions 构建完成后，下载以下文件：

```powershell
# 使用 gh CLI 下载
gh release download v1.1.2 --repo BroderQi/Storyboard --dir release-temp

# 或者手动从浏览器下载
# https://github.com/BroderQi/Storyboard/releases/tag/v1.1.2
```

需要下载的文件：
- `RELEASES`
- `Storyboard-1.1.2-full.nupkg`
- `Storyboard-1.1.2-delta.nupkg`（如果有）

### 步骤 3：手动上传到又拍云

#### 方式 1：使用又拍云控制台（推荐）

1. 登录 [又拍云控制台](https://console.upyun.com/)
2. 进入 `storyboard-release` 存储空间
3. 进入 `storyboard/` 目录
4. 点击"上传文件"
5. 选择下载的所有文件（RELEASES 和 .nupkg）
6. 等待上传完成

#### 方式 2：使用 FTP 工具

1. 使用 FileZilla 或其他 FTP 工具
2. 连接信息：
   - 主机：`v0.ftp.upyun.com`
   - 用户名：`storyboard-release/storyboard`（桶名/操作员）
   - 密码：`你的操作员密码`（仅你自己知道，不要泄露）
3. 上传文件到 `/storyboard/` 目录

### 步骤 4：验证上传

```powershell
# 验证 RELEASES 文件（公开访问，无需密钥）
curl https://storyboard-release.bkgf.net/storyboard/RELEASES

# 验证 .nupkg 文件（公开访问，无需密钥）
curl -I https://storyboard-release.bkgf.net/storyboard/Storyboard-1.1.2-full.nupkg
```

应该返回 200 状态码，表示文件可以正常访问。

## 🔧 应用配置

### appsettings.json

应用配置中**不包含任何密钥**，只有公开的 CDN 地址：

```json
{
  "Update": {
    "Sources": [
      {
        "Name": "又拍云 CDN",
        "Type": "Http",
        "Url": "https://storyboard-release.bkgf.net/storyboard",
        "Priority": 1,
        "Enabled": true
      }
    ]
  }
}
```

**安全特性**：
- ✅ 无密钥、无操作员信息
- ✅ 只有公开的 CDN 域名
- ✅ 用户通过 HTTPS 匿名下载
- ✅ 无法通过应用上传或修改文件

## 📊 更新流程

```
用户启动应用
    ↓
延迟 3 秒
    ↓
连接又拍云 CDN（公开访问，无需认证）
    ↓
GET https://storyboard-release.bkgf.net/storyboard/RELEASES
    ↓
解析版本信息
    ↓
发现新版本？
    ↓ 是
显示更新通知
    ↓
用户点击下载
    ↓
GET https://storyboard-release.bkgf.net/storyboard/Storyboard-1.1.2-full.nupkg
    ↓
下载完成（匿名访问，无需认证）
    ↓
安装并重启
```

## ✅ 检查清单

每次发布新版本时：

- [ ] 运行 `quick-release.ps1` 发布到 GitHub
- [ ] 等待 GitHub Actions 构建完成
- [ ] 从 GitHub Release 下载文件
- [ ] 登录又拍云控制台（使用你的账号）
- [ ] 上传文件到 `/storyboard/` 目录
- [ ] 验证文件可以公开访问
- [ ] 测试应用更新功能

## 🐛 故障排查

### 问题 1：检测不到更新

```powershell
# 检查 RELEASES 文件（公开访问）
curl https://storyboard-release.bkgf.net/storyboard/RELEASES

# 应该返回类似这样的内容：
# SHA1HASH Storyboard-1.1.2-full.nupkg 12345678
```

### 问题 2：下载失败

```powershell
# 检查 .nupkg 文件（公开访问）
curl -I https://storyboard-release.bkgf.net/storyboard/Storyboard-1.1.2-full.nupkg

# 应该返回 200 状态码
```

### 问题 3：文件路径错误

确保文件在正确的路径：
- ✅ 正确：`/storyboard/RELEASES`
- ❌ 错误：`/storyboard/releases/RELEASES`
- ❌ 错误：`/RELEASES`

## 💡 提示

1. **保留旧版本**：建议保留最近 3-5 个版本的文件，方便用户回滚
2. **文件命名**：确保文件名与 RELEASES 文件中的名称完全一致
3. **权限设置**：确保文件可以公开访问（匿名读取）
4. **CDN 缓存**：如果更新了文件但用户看不到，可能是 CDN 缓存，等待几分钟或手动刷新缓存
5. **密钥安全**：操作员密钥仅用于你自己上传文件，不要泄露给他人

## 🔐 安全最佳实践

1. **操作员密钥**：
   - ✅ 仅用于你手动上传文件
   - ✅ 不要写在代码中
   - ✅ 不要提交到 Git 仓库
   - ✅ 定期更换密码

2. **公开访问**：
   - ✅ 用户通过 HTTPS 匿名下载
   - ✅ 无需在应用中配置任何密钥
   - ✅ 只读权限，无法上传或修改

3. **文件完整性**：
   - ✅ Velopack 会验证 SHA1 哈希
   - ✅ 防止文件被篡改
   - ✅ 确保更新安全

---

**最后更新**：2026-01-22
