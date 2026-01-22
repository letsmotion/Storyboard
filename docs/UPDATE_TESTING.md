# 自动更新测试指南

本文档说明如何测试分镜大师的自动更新功能。

## 📋 测试前准备

### 1. 环境要求

- Windows 10/11
- PowerShell 5.1+
- 网络连接（访问又拍云和 GitHub）

### 2. 配置又拍云测试环境

```powershell
# 设置环境变量
$env:UPYUN_BUCKET = "your-test-bucket"
$env:UPYUN_OPERATOR = "your-operator"
$env:UPYUN_PASSWORD = "your-password"
```

---

## 🧪 测试场景

### 场景 1：检测更新（又拍云源）

**目的**：验证应用能从又拍云检测到新版本

**步骤**：

1. 确保又拍云已上传新版本文件
2. 修改 `appsettings.json`：
   ```json
   {
     "Update": {
       "Sources": [
         {
           "Name": "又拍云 CDN",
           "Type": "Http",
           "Url": "https://your-bucket.b0.upaiyun.com/storyboard/releases",
           "Priority": 1,
           "Enabled": true
         }
       ]
     }
   }
   ```
3. 启动应用
4. 等待 3 秒

**预期结果**：

- 日志显示：`尝试使用 又拍云 CDN 更新源 (类型: Http)`
- 日志显示：`成功初始化 又拍云 CDN 更新源`
- 日志显示：`发现新版本: x.x.x`
- UI 显示更新通知

**验证日志**：

```
[Information] 尝试使用 又拍云 CDN 更新源 (类型: Http): https://your-bucket.b0.upaiyun.com/storyboard/releases
[Information] 成功初始化 又拍云 CDN 更新源
[Information] 开始检查更新...
[Information] 发现新版本: 1.1.2
```

---

### 场景 2：下载更新（又拍云源）

**目的**：验证应用能从又拍云下载更新包

**步骤**：

1. 完成场景 1
2. 点击"下载并安装"按钮
3. 观察下载进度

**预期结果**：

- 显示下载进度：0% → 100%
- 日志显示：`开始下载更新...`
- 日志显示：`更新下载完成`
- 应用自动重启

**验证日志**：

```
[Information] 开始下载更新...
[Information] 更新下载完成
[Information] 准备应用更新并重启...
```

---

### 场景 3：源切换（又拍云失败 → GitHub）

**目的**：验证又拍云失败时自动切换到 GitHub

**步骤**：

1. 修改 `appsettings.json`，设置错误的又拍云 URL：
   ```json
   {
     "Update": {
       "Sources": [
         {
           "Name": "又拍云 CDN",
           "Type": "Http",
           "Url": "https://invalid-url.com/releases",
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
2. 启动应用
3. 等待 3 秒

**预期结果**：

- 日志显示又拍云失败
- 日志显示切换到 GitHub
- 成功检测到更新

**验证日志**：

```
[Information] 尝试使用 又拍云 CDN 更新源 (类型: Http): https://invalid-url.com/releases
[Warning] 又拍云 CDN 更新源初始化失败，尝试下一个源
[Information] 尝试使用 GitHub 更新源 (类型: GitHub): https://github.com/BroderQi/Storyboard
[Information] 成功初始化 GitHub 更新源
[Information] 发现新版本: 1.1.2
```

---

### 场景 4：手动检查更新

**目的**：验证手动检查更新功能

**步骤**：

1. 启动应用
2. 点击"检查更新"按钮（如果有）
3. 或调用 `UpdateService.CheckForUpdatesAsync()`

**预期结果**：

- 立即开始检查更新
- 显示检查结果

---

### 场景 5：增量更新

**目的**：验证增量更新功能（delta 包）

**步骤**：

1. 确保又拍云有 delta 包：`Storyboard-x.x.x-delta.nupkg`
2. 从旧版本更新到新版本
3. 观察下载的文件大小

**预期结果**：

- 下载 delta 包（文件较小）
- 而不是 full 包（文件较大）
- 更新速度更快

---

### 场景 6：完整更新

**目的**：验证完整更新功能（full 包）

**步骤**：

1. 跨多个版本更新（如 1.0.0 → 1.2.0）
2. 或删除 delta 包，只保留 full 包
3. 执行更新

**预期结果**：

- 下载 full 包
- 完整安装新版本

---

## 🔧 手动测试工具

### 测试 RELEASES 文件

```powershell
# 下载 RELEASES 文件
curl https://your-bucket.b0.upaiyun.com/storyboard/releases/RELEASES -o RELEASES

# 查看内容
cat RELEASES
```

### 测试 .nupkg 文件

```powershell
# 检查文件是否存在
curl -I https://your-bucket.b0.upaiyun.com/storyboard/releases/Storyboard-1.1.2-full.nupkg

# 下载文件
curl https://your-bucket.b0.upaiyun.com/storyboard/releases/Storyboard-1.1.2-full.nupkg -o test.nupkg

# 验证文件大小
ls -lh test.nupkg
```

### 测试 API 连接

```powershell
# 测试又拍云 API
curl -X GET https://v0.api.upyun.com/$env:UPYUN_BUCKET/

# 测试 GitHub API
curl https://api.github.com/repos/BroderQi/Storyboard/releases/latest
```

---

## 📊 测试检查清单

### 功能测试

- [ ] 应用启动时自动检查更新
- [ ] 延迟 3 秒后开始检查
- [ ] 从又拍云检测到新版本
- [ ] 显示更新通知 UI
- [ ] 点击"下载并安装"开始下载
- [ ] 显示下载进度（0-100%）
- [ ] 下载完成后自动重启
- [ ] 重启后应用为新版本
- [ ] 手动检查更新功能正常

### 源切换测试

- [ ] 又拍云失败时切换到 GitHub
- [ ] GitHub 失败时切换到 Gitee（如果启用）
- [ ] 日志正确记录源切换过程
- [ ] 最终能成功下载更新

### 边界测试

- [ ] 当前已是最新版本时不显示更新
- [ ] 网络断开时优雅处理错误
- [ ] 下载中断后能重试
- [ ] 更新失败后不影响应用运行
- [ ] 配置错误时不崩溃

### 性能测试

- [ ] 检查更新不阻塞应用启动
- [ ] 下载速度符合预期（又拍云 > GitHub）
- [ ] 内存占用正常
- [ ] CPU 占用正常

### 日志测试

- [ ] 日志记录完整
- [ ] 日志级别正确（Info/Warning/Error）
- [ ] 敏感信息已脱敏（密码等）
- [ ] 日志文件正常滚动

---

## 🐛 常见问题

### 问题 1：检测不到更新

**可能原因**：

1. 又拍云 URL 配置错误
2. RELEASES 文件不存在或格式错误
3. 版本号比较逻辑错误
4. 网络连接问题

**调试方法**：

```powershell
# 1. 检查配置
cat appsettings.json | Select-String "Update" -Context 10

# 2. 测试 URL
curl https://your-bucket.b0.upaiyun.com/storyboard/releases/RELEASES

# 3. 查看日志
cat logs/app-*.log | Select-String "更新"
```

### 问题 2：下载失败

**可能原因**：

1. .nupkg 文件不存在
2. 文件权限问题
3. 网络超时
4. 磁盘空间不足

**调试方法**：

```powershell
# 1. 测试文件下载
curl -I https://your-bucket.b0.upaiyun.com/storyboard/releases/Storyboard-1.1.2-full.nupkg

# 2. 检查磁盘空间
Get-PSDrive C | Select-Object Used,Free

# 3. 查看详细错误
cat logs/app-*.log | Select-String "Error"
```

### 问题 3：更新后无法启动

**可能原因**：

1. 更新包损坏
2. 依赖文件缺失
3. 权限问题

**调试方法**：

```powershell
# 1. 查看 Velopack 日志
cat $env:LOCALAPPDATA\Storyboard\SquirrelSetup.log

# 2. 验证文件完整性
# 重新下载并比对 SHA1

# 3. 回滚版本
# 删除 current 目录，重命名 previous 为 current
```

---

## 📝 测试报告模板

```markdown
# 自动更新测试报告

**测试日期**：2026-01-22
**测试版本**：v1.1.2
**测试人员**：[姓名]

## 测试环境

- 操作系统：Windows 11
- 旧版本：v1.1.1
- 新版本：v1.1.2
- 更新源：又拍云 CDN

## 测试结果

### 场景 1：检测更新
- 状态：✅ 通过
- 耗时：2.5 秒
- 备注：成功从又拍云检测到新版本

### 场景 2：下载更新
- 状态：✅ 通过
- 下载速度：5.2 MB/s
- 文件大小：45 MB
- 备注：下载速度良好

### 场景 3：源切换
- 状态：✅ 通过
- 切换时间：1.2 秒
- 备注：又拍云失败后成功切换到 GitHub

## 问题记录

1. [问题描述]
   - 严重程度：高/中/低
   - 复现步骤：...
   - 预期结果：...
   - 实际结果：...

## 总结

- 通过率：100%
- 主要问题：无
- 建议：...
```

---

## 🎯 自动化测试（未来）

### 单元测试

```csharp
[Fact]
public async Task CheckForUpdatesAsync_WithUpyunSource_ShouldReturnUpdateInfo()
{
    // Arrange
    var updateService = CreateUpdateService();

    // Act
    var updateInfo = await updateService.CheckForUpdatesAsync();

    // Assert
    Assert.NotNull(updateInfo);
    Assert.True(updateInfo.TargetFullRelease.Version > currentVersion);
}
```

### 集成测试

```csharp
[Fact]
public async Task DownloadUpdatesAsync_WithUpyunSource_ShouldDownloadSuccessfully()
{
    // Arrange
    var updateService = CreateUpdateService();
    var updateInfo = await updateService.CheckForUpdatesAsync();

    // Act
    var result = await updateService.DownloadUpdatesAsync(updateInfo);

    // Assert
    Assert.True(result);
}
```

---

**最后更新**：2026-01-22
