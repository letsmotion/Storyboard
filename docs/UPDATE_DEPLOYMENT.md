# 自动更新部署指南

本文档说明如何配置和部署分镜大师的自动更新功能，使用又拍云 CDN 加速国内用户的更新体验。

## 📋 目录

1. [工作原理](#工作原理)
2. [又拍云配置](#又拍云配置)
3. [发布流程](#发布流程)
4. [配置说明](#配置说明)
5. [故障排查](#故障排查)

---

## 🔧 工作原理

### 更新检测流程

```
应用启动（3秒后）
    ↓
UpdateService.CheckForUpdatesAsync()
    ↓
连接又拍云 CDN（优先级 1）
    ↓
下载 RELEASES 文件
    ↓
解析版本信息
    ↓
比较本地版本 vs 远程版本
    ↓
发现新版本？
    ↓ 是
返回 UpdateInfo
    ↓
显示更新通知
```

### 更新下载流程

```
用户点击"下载并安装"
    ↓
从又拍云下载 .nupkg 文件
    ↓
显示下载进度（0-100%）
    ↓
下载完成
    ↓
应用更新并重启
    ↓
VelopackApp.Build().Run() 完成安装
    ↓
应用启动（新版本）✅
```

### 智能源切换

如果又拍云访问失败，自动切换到 GitHub：

```
又拍云 CDN（优先级 1）
    ↓ 失败
GitHub Release（优先级 2）
    ↓ 失败
Gitee（优先级 3，默认禁用）
```

---

## ☁️ 又拍云配置

### 1. 创建存储空间

1. 登录 [又拍云控制台](https://console.upyun.com/)
2. 创建云存储服务
3. 选择"标准存储"
4. 记录存储空间名称（如：`storyboard-cdn`）

### 2. 创建操作员

1. 进入存储空间 → 操作员管理
2. 创建新操作员
3. 设置权限：可读可写
4. 记录操作员账号和密码

### 3. 配置 CDN 加速（可选）

1. 绑定自定义域名
2. 配置 HTTPS 证书
3. 开启 CDN 加速

### 4. 设置访问权限

1. 进入存储空间 → 访问控制
2. 设置为"公开读取"
3. 允许匿名访问（用于下载更新文件）

---

## 🚀 发布流程

### 方式一：完整发布流程

```powershell
# 1. 设置又拍云环境变量（首次配置）
$env:UPYUN_BUCKET = "storyboard-cdn"
$env:UPYUN_OPERATOR = "your-operator"
$env:UPYUN_PASSWORD = "your-password"

# 2. 运行发布脚本（自动构建、推送 GitHub、下载文件）
.\scripts\quick-release.ps1

# 3. 上传到又拍云
.\scripts\upload-to-upyun.ps1 -Version "1.1.2"
```

### 方式二：仅上传到又拍云

如果 GitHub Release 已经存在：

```powershell
# 1. 下载 GitHub Release 文件
gh release download v1.1.2 --repo BroderQi/Storyboard --dir release-temp

# 2. 上传到又拍云
.\scripts\upload-to-upyun.ps1 -Version "1.1.2" -DownloadDir "release-temp"
```

### 方式三：手动上传

1. 从 GitHub Release 下载文件
2. 使用又拍云控制台或 FTP 工具上传到 `/storyboard/releases/` 目录
3. 确保以下文件都已上传：
   - `RELEASES`
   - `Storyboard-x.x.x-full.nupkg`
   - `Storyboard-x.x.x-delta.nupkg`（如果有）

---

## ⚙️ 配置说明

### appsettings.json

```json
{
  "Update": {
    "Enabled": true,
    "CheckOnStartup": true,
    "CheckDelaySeconds": 3,
    "Sources": [
      {
        "Name": "又拍云 CDN",
        "Type": "Http",
        "Url": "https://storyboard-cdn.b0.upaiyun.com/storyboard/releases",
        "Priority": 1,
        "Enabled": true
      },
      {
        "Name": "GitHub",
        "Type": "GitHub",
        "Url": "https://github.com/BroderQi/Storyboard",
        "Priority": 2,
        "Enabled": true
      }
    ]
  }
}
```

### 配置项说明

| 字段 | 说明 | 示例 |
|------|------|------|
| `Enabled` | 是否启用自动更新 | `true` |
| `CheckOnStartup` | 启动时检查更新 | `true` |
| `CheckDelaySeconds` | 启动后延迟检查秒数 | `3` |
| `Name` | 更新源名称 | `"又拍云 CDN"` |
| `Type` | 源类型 | `"Http"`, `"GitHub"`, `"Gitee"` |
| `Url` | 更新源地址 | 见下方说明 |
| `Priority` | 优先级（越小越高） | `1`, `2`, `3` |
| `Enabled` | 是否启用此源 | `true` |

### URL 格式说明

**HTTP 源（又拍云/OSS）**：
```
https://your-bucket.b0.upaiyun.com/storyboard/releases
```
- 必须是 HTTPS
- 路径指向存放 RELEASES 文件的目录
- 不要在末尾加 `/`

**GitHub 源**：
```
https://github.com/BroderQi/Storyboard
```
- 标准 GitHub 仓库地址
- Velopack 会自动访问 Release API

**Gitee 源**：
```
https://gitee.com/nan1314/Storyboard
```
- 标准 Gitee 仓库地址
- 使用与 GitHub 相同的 API 格式

---

## 📁 又拍云目录结构

```
/storyboard/releases/
├── RELEASES                              # 版本清单文件（必需）
├── Storyboard-1.1.0-full.nupkg         # v1.1.0 完整包
├── Storyboard-1.1.0-delta.nupkg        # v1.1.0 增量包
├── Storyboard-1.1.1-full.nupkg         # v1.1.1 完整包
├── Storyboard-1.1.1-delta.nupkg        # v1.1.1 增量包
├── Storyboard-1.1.2-full.nupkg         # v1.1.2 完整包（最新）
└── Storyboard-1.1.2-delta.nupkg        # v1.1.2 增量包（最新）
```

### RELEASES 文件格式

```
SHA1HASH Storyboard-1.1.2-full.nupkg 12345678
SHA1HASH Storyboard-1.1.2-delta.nupkg 1234567
SHA1HASH Storyboard-1.1.1-full.nupkg 12345678
...
```

- 由 Velopack 自动生成
- 包含所有版本的哈希值和文件大小
- 客户端通过此文件判断是否有新版本

---

## 🔍 故障排查

### 问题 1：检测不到更新

**症状**：应用启动后没有更新提示

**排查步骤**：

1. 检查日志文件（`logs/app-*.log`）：
   ```
   [Information] 尝试使用 又拍云 CDN 更新源 (类型: Http): https://...
   [Information] 成功初始化 又拍云 CDN 更新源
   [Information] 开始检查更新...
   ```

2. 验证又拍云文件可访问：
   ```powershell
   curl https://your-bucket.b0.upaiyun.com/storyboard/releases/RELEASES
   ```

3. 检查 `appsettings.json` 配置：
   - URL 是否正确
   - `Enabled` 是否为 `true`
   - 路径末尾不要有 `/`

4. 确认版本号：
   - 远程版本必须大于本地版本
   - 检查 `Storyboard.csproj` 中的 `<Version>`

### 问题 2：下载失败

**症状**：检测到更新但下载失败

**排查步骤**：

1. 检查网络连接：
   ```powershell
   curl -I https://your-bucket.b0.upaiyun.com/storyboard/releases/Storyboard-1.1.2-full.nupkg
   ```

2. 验证文件完整性：
   - 文件大小是否正确
   - SHA1 哈希是否匹配

3. 查看日志中的错误信息：
   ```
   [Error] 下载更新失败: ...
   [Information] 已切换到备用更新源: GitHub
   ```

4. 测试备用源：
   - 如果又拍云失败，应自动切换到 GitHub
   - 检查 GitHub 源是否可用

### 问题 3：上传到又拍云失败

**症状**：运行 `upload-to-upyun.ps1` 报错

**排查步骤**：

1. 检查环境变量：
   ```powershell
   echo $env:UPYUN_BUCKET
   echo $env:UPYUN_OPERATOR
   echo $env:UPYUN_PASSWORD
   ```

2. 验证操作员权限：
   - 登录又拍云控制台
   - 确认操作员有读写权限

3. 测试 API 连接：
   ```powershell
   curl -X GET https://v0.api.upyun.com/your-bucket/
   ```

4. 检查文件路径：
   - 确保 `release-temp` 目录存在
   - 确认目录中有 `.nupkg` 文件

### 问题 4：更新后应用无法启动

**症状**：安装更新后应用崩溃或无法启动

**排查步骤**：

1. 检查更新包完整性：
   - 重新下载并上传更新包
   - 验证 SHA1 哈希

2. 查看 Velopack 日志：
   - 位置：`%LocalAppData%\Storyboard\`
   - 文件：`SquirrelSetup.log`

3. 回滚到旧版本：
   - Velopack 会自动保留旧版本
   - 删除 `current` 目录，重命名 `previous` 为 `current`

4. 重新安装：
   - 卸载应用
   - 从 GitHub Release 下载完整安装包
   - 重新安装

---

## 📊 监控和维护

### 建议监控指标

1. **下载成功率**：
   - 监控又拍云 CDN 流量
   - 查看下载失败日志

2. **更新采用率**：
   - 统计各版本用户数量
   - 分析更新速度

3. **成本控制**：
   - 监控又拍云流量费用
   - 设置流量告警

### 定期维护

1. **清理旧版本**（每月）：
   - 保留最近 3-5 个版本
   - 删除过旧的 `.nupkg` 文件
   - 更新 `RELEASES` 文件

2. **测试更新流程**（每次发布前）：
   - 在测试环境验证更新
   - 确认下载速度正常
   - 检查安装过程无误

3. **备份配置**（每季度）：
   - 备份又拍云配置
   - 导出操作员信息
   - 记录 CDN 设置

---

## 🎯 最佳实践

1. **多源配置**：
   - 始终配置至少 2 个更新源
   - 又拍云作为主源，GitHub 作为备用

2. **版本管理**：
   - 使用语义化版本号（SemVer）
   - 每次发布都更新 CHANGELOG

3. **测试流程**：
   - 在测试环境先验证更新
   - 确认增量更新和完整更新都正常

4. **用户体验**：
   - 延迟 3 秒检查更新，避免影响启动
   - 显示下载进度，提升用户体验
   - 提供"稍后提醒"选项

5. **安全性**：
   - 使用 HTTPS 传输
   - 验证文件 SHA1 哈希
   - 定期更新操作员密码

---

## 📞 技术支持

如有问题，请：

1. 查看日志文件：`logs/app-*.log`
2. 提交 Issue：https://github.com/BroderQi/Storyboard/issues
3. 参考 Velopack 文档：https://docs.velopack.io/

---

**最后更新**：2026-01-22
