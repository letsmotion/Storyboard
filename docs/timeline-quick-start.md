# Timeline 重构 - 快速集成指南

## 🚀 立即开始

### 第 1 步：替换 ViewModel 文件

```bash
# 在项目根目录执行

# 备份旧文件
mv "App/ViewModels/Timeline/TimelineEditorViewModel.cs" "App/ViewModels/Timeline/TimelineEditorViewModel.cs.backup"
mv "App/ViewModels/Generation/ExportViewModel.cs" "App/ViewModels/Generation/ExportViewModel.cs.backup"

# 使用新文件
mv "App/ViewModels/Timeline/TimelineEditorViewModelRefactored.cs" "App/ViewModels/Timeline/TimelineEditorViewModel.cs"
mv "App/ViewModels/Generation/ExportViewModelRefactored.cs" "App/ViewModels/Generation/ExportViewModel.cs"
```

### 第 2 步：在 MainViewModel 中添加消息处理器

在 `App/ViewModels/MainViewModel.cs` 的构造函数中，找到现有的消息注册部分，添加：

```csharp
// 在 _messenger.Register<GetAllShotsQuery> 之后添加

_messenger.Register<GetCurrentProjectPathQuery>(this, (r, query) =>
{
    if (!string.IsNullOrEmpty(CurrentProjectId))
    {
        // 获取项目路径
        var storagePathService = App.Services.GetRequiredService<StoragePathService>();
        var projectsDir = storagePathService.GetProjectsDirectory();
        query.ProjectPath = Path.Combine(projectsDir, CurrentProjectId);
    }
});
```

### 第 3 步：更新 TimelineEditorViewModel 的依赖注入

在 `App/App.axaml.cs` 中，找到 TimelineEditorViewModel 的注册，确保它接收 IDraftManager：

```csharp
// 应该已经正确注册，但请确认：
services.AddTransient<ViewModels.Timeline.TimelineEditorViewModel>();
```

构造函数会自动注入 `IDraftManager`。

### 第 4 步：触发草稿加载

在项目加载时，需要触发草稿的加载。在 `MainViewModel` 或 `ProjectManagementViewModel` 中：

**方式 A：通过消息触发（推荐）**

修改 `ProjectDataLoadedMessage` 的发送位置，确保包含项目路径信息。由于当前的 `ProjectDataLoadedMessage` 接收 `ProjectState`，我们需要确保 TimelineEditorViewModel 能获取到项目路径。

在 `MainViewModel.cs` 中，找到发送 `ProjectDataLoadedMessage` 的地方，在其后添加：

```csharp
// 在发送 ProjectDataLoadedMessage 之后
private void OnProjectDataLoaded(object recipient, ProjectDataLoadedMessage message)
{
    // 现有逻辑...

    // 触发 Timeline 加载草稿
    if (TimelineEditor != null && !string.IsNullOrEmpty(CurrentProjectId))
    {
        var projectPath = GetProjectPath(CurrentProjectId);
        var projectName = ProjectName;
        _ = TimelineEditor.LoadOrCreateDraftAsync(projectPath, projectName);
    }
}

private string GetProjectPath(string projectId)
{
    var storagePathService = App.Services.GetRequiredService<StoragePathService>();
    return Path.Combine(storagePathService.GetProjectsDirectory(), projectId);
}
```

**方式 B：直接调用（简单）**

如果 MainViewModel 有 TimelineEditor 属性，可以直接调用：

```csharp
// 在项目加载完成后
await TimelineEditor.LoadOrCreateDraftAsync(projectPath, projectName);
```

### 第 5 步：编译并测试

```bash
# 编译项目
dotnet build

# 如果有编译错误，检查：
# 1. 所有 using 语句是否正确
# 2. IDraftManager 是否已注册
# 3. 文件路径是否正确
```

### 第 6 步：运行测试

1. **创建新项目**
   ```
   启动应用 → 创建新项目 → 检查项目目录
   应该看到：ProjectDirectory/draft/draft_content.json
   ```

2. **添加镜头并生成视频**
   ```
   导入视频 → 抽帧 → 生成视频
   等待 5 秒 → 检查 draft_content.json 是否更新
   ```

3. **重新打开项目**
   ```
   关闭项目 → 重新打开
   时间轴应该正确显示所有片段
   ```

4. **导出到 CapCut**
   ```
   点击导出 → 选择"导出到 CapCut"
   在 CapCut 中打开导出的草稿
   验证视频片段正确
   ```

## 🔧 故障排查

### 问题 1：编译错误 - 找不到 IDraftManager

**解决：**
```csharp
// 确保在 App.axaml.cs 中已注册
services.AddSingleton<IDraftManager, DraftManager>();
```

### 问题 2：草稿文件未创建

**检查：**
1. `LoadOrCreateDraftAsync` 是否被调用？
2. 项目路径是否正确？
3. 查看日志输出

**调试代码：**
```csharp
// 在 TimelineEditorViewModel 中添加日志
_logger.LogInformation("尝试加载草稿: {ProjectPath}", projectPath);
```

### 问题 3：时间轴不显示

**检查：**
1. `BuildTimelineFromDraft()` 是否被调用？
2. `draft_content.json` 中是否有数据？
3. 查看 `Tracks` 集合是否为空

**调试代码：**
```csharp
// 在 BuildTimelineFromDraft 中添加
_logger.LogInformation("构建时间轴: {TrackCount} 轨道, {SegmentCount} 片段",
    _draftContent.Tracks.Count,
    _draftContent.Tracks.SelectMany(t => t.Segments).Count());
```

### 问题 4：自动保存不工作

**检查：**
1. 定时器是否启动？
2. `_isDirty` 标志是否被设置？

**调试代码：**
```csharp
// 在 SyncShotsToTimelineAsync 中添加
_logger.LogInformation("同步完成，标记为脏数据");
_isDirty = true;
```

## 📝 验证清单

完成以下检查以确保集成成功：

- [ ] 编译无错误
- [ ] 创建新项目时生成 `draft/` 目录
- [ ] `draft_content.json` 和 `draft_meta_info.json` 存在
- [ ] 添加镜头后，5 秒内 JSON 文件更新
- [ ] 重新打开项目时，时间轴正确显示
- [ ] 导出到 CapCut 成功
- [ ] 在 CapCut 中能打开导出的草稿
- [ ] 视频片段顺序和时长正确

## 🎯 下一步优化

### 立即可做
1. **添加导出按钮到 UI**
   ```xml
   <Button Content="导出到 CapCut"
           Command="{Binding ExportToCapCutDirectCommand}"/>
   ```

2. **调整自动保存频率**
   ```csharp
   // 在 TimelineEditorViewModel 中
   _autoSaveTimer = new System.Timers.Timer(10000); // 改为 10 秒
   ```

3. **添加加载指示器**
   ```csharp
   [ObservableProperty]
   private bool _isLoadingDraft;
   ```

### 未来增强
1. 增量保存（只保存变化部分）
2. 草稿版本历史
3. 撤销/重做支持
4. 素材路径配置（相对/绝对）
5. 支持更多编辑器格式

## 📚 相关文档

- [完整重构指南](timeline-refactoring-guide.md) - 详细的架构说明
- [CapCut 导出文档](capcut-export-implementation.md) - 导出功能说明
- [重构总结](timeline-refactoring-summary.md) - 完成情况总结

## 💡 提示

1. **备份数据** - 在测试前备份现有项目
2. **查看日志** - 遇到问题时先查看日志输出
3. **手动检查** - 可以直接打开 JSON 文件查看内容
4. **渐进式迁移** - 可以先在测试项目中验证

## 🆘 需要帮助？

如果遇到问题：
1. 检查日志文件：`logs/app-*.log`
2. 查看 JSON 文件内容
3. 参考文档中的故障排查部分
4. 检查是否所有步骤都已完成

---

**准备就绪！** 按照以上步骤即可完成集成。
