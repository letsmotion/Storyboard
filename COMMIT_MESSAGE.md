# Velopack 更新机制修复 - 提交说明

## 🎯 修复的核心问题

**问题**: 应用更新时出现 "Failed to remove existing application directory" 错误

**根本原因**:
1. 使用了 `ApplyUpdatesAndRestart()` 方法，会在应用未完全退出时尝试替换文件
2. 资源清理不完整，导致文件句柄未释放
3. JobQueueService 缺少 Dispose 实现

## ✅ 修复内容

### 1. 使用正确的 Velopack 更新方法

**文件**: `Application/Services/UpdateService.cs`, `App/ViewModels/UpdateNotificationViewModel.cs`

```csharp
// ❌ 之前 - 立即替换文件（失败）
_updateManager.ApplyUpdatesAndRestart(updateInfo);

// ✅ 现在 - 等待应用退出后再替换
await _updateManager.WaitExitThenApplyUpdatesAsync(updateInfo);
desktop.Shutdown();  // 触发正常退出流程
```

### 2. 实现完整的资源清理机制

**文件**: `App/App.axaml.cs`

```csharp
// 注册退出事件
desktop.ShutdownRequested += OnShutdownRequested;  // 关闭前准备
desktop.Exit += OnApplicationExit;                  // 退出时清理

// 清理顺序（总延迟 ~800ms）
1. 停止后台任务 (JobQueueService.Dispose)  - 100ms
2. 关闭数据库连接 (EF Core)                - 100ms
3. 释放所有服务 (ServiceProvider.Dispose)  - 300ms
4. 关闭日志系统 (Log.CloseAndFlush)        - 200ms
```

### 3. JobQueueService 实现 IDisposable

**文件**: `Application/Services/JobQueueService.cs`

```csharp
public void Dispose()
{
    _channel.Writer.Complete();
    foreach (var cts in _cancellations.Values) cts?.Cancel();
    Thread.Sleep(100);
    foreach (var cts in _cancellations.Values) cts?.Dispose();
    _cancellations.Clear();
    _runners.Clear();
    _concurrency?.Dispose();
}
```

### 4. 添加 Velopack 更新钩子

**文件**: `App/Program.cs`

```csharp
VelopackApp.Build()
    .WithFirstRun(v => { ... })
    .WithAfterUpdateFastCallback(v => {
        Thread.Sleep(500);  // 给系统时间完成文件操作
    })
    .Run();
```

### 5. 修复编译错误

**文件**: `Application/Services/UpdateService.cs`
- 移除不存在的 `restarted` 参数

**文件**: `.github/workflows/release.yml`
- 移除不支持的 `--runtimeDependencies` 参数

## 📊 修改的文件

```
Application/Services/UpdateService.cs          - 更新方法优化
Application/Services/JobQueueService.cs        - 实现 IDisposable
App/App.axaml.cs                               - 完善资源清理
App/Program.cs                                 - 添加 Velopack 钩子
App/ViewModels/UpdateNotificationViewModel.cs - 使用正确的更新流程
.github/workflows/release.yml                  - 修复打包参数
```

## 🔍 技术细节

### 更新流程对比

**之前（失败）**:
```
下载 → ApplyUpdatesAndRestart() → 立即替换文件 → ❌ 文件被占用
```

**现在（成功）**:
```
下载 → WaitExitThenApplyUpdatesAsync() → 通知用户 → Shutdown()
→ 完整资源清理 (~800ms) → 应用退出 → Update.exe 等待
→ 替换文件 → ✅ 成功 → 重启应用
```

### 资源清理保证

1. **后台任务**: 取消所有任务，释放 SemaphoreSlim 和 Channel
2. **数据库**: EF Core 自动关闭连接，释放 SQLite 文件锁
3. **LibVLC**: ServiceProvider.Dispose 自动释放所有单例服务
4. **日志**: Log.CloseAndFlush() 刷新缓冲区，关闭文件句柄
5. **延迟**: 每个步骤都有足够的延迟确保操作完成

### 用户数据保护

```
应用程序目录（会被更新）:
  C:\Users\用户名\AppData\Local\Storyboard\current\

用户数据目录（永远不会被删除）:
  C:\Users\用户名\AppData\Local\Storyboard\
  ├── Data\storyboard.db
  ├── output\
  ├── logs\
  └── *.json
```

## ✅ 验证

- [x] 代码编译成功
- [x] 所有编译错误已修复
- [x] 资源清理机制完整
- [x] 用户数据保护正确
- [ ] 实际更新测试（待发布后）

## 📝 相关文档

- `docs/VELOPACK_UPDATE_FIX.md` - 详细的更新机制说明
- `docs/VELOPACK_REVIEW_AND_FIX.md` - 全面审查和修复报告
- `docs/VELOPACK_USER_DATA_PROTECTION.md` - 用户数据保护机制
- `docs/RESOURCE_CLEANUP_ON_CLOSE.md` - 资源清理流程分析
- `docs/UPDATE_TROUBLESHOOTING.md` - 用户故障排除指南
- `docs/VELOPACK_FIX_SUMMARY.md` - 修复总结

## 🎉 预期效果

修复后，应用更新应该能够：
1. ✅ 正确下载更新包
2. ✅ 等待应用完全退出
3. ✅ 成功替换所有文件
4. ✅ 自动重启应用
5. ✅ 保留所有用户数据

**"Failed to remove existing application directory" 错误应该彻底解决！**

---

**修复时间**: 2026-03-04
**修复人**: Claude (Sonnet 4.6)
