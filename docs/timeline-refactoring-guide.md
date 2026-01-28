# Timeline 核心架构重构：基于 CapCut 草稿格式

## 概述

本次重构将分镜大师的核心数据格式从内部自定义格式迁移到 **CapCut 草稿格式**（两个 JSON 文件）。这意味着：

1. **项目数据即草稿** - 每个项目都有一个 `draft/` 目录，包含 `draft_content.json` 和 `draft_meta_info.json`
2. **实时同步** - 时间轴的所有操作都直接反映在草稿文件中
3. **自动保存** - 草稿文件每 5 秒自动保存
4. **无缝导出** - 导出到 CapCut 只需复制草稿目录

## 架构变化

### 旧架构（Before）

```
ShotItem (内存)
    ↓
TimelineEditorViewModel (临时构建)
    ↓
导出时转换为 CapCut 格式
```

**问题：**
- 数据格式不统一
- 导出时需要复杂转换
- 无法直接在 CapCut 中编辑项目

### 新架构（After）

```
ShotItem (内存)
    ↓ (同步)
DraftContent + DraftMetaInfo (磁盘 JSON)
    ↓ (加载)
TimelineEditorViewModel (基于草稿)
    ↓ (直接复制)
CapCut 草稿
```

**优势：**
- 数据格式统一（CapCut 标准）
- 实时持久化到磁盘
- 导出即复制，无需转换
- 可以直接在 CapCut 中打开项目

## 核心组件

### 1. DraftManager (`Infrastructure/Services/DraftManager.cs`)

**职责：** 管理草稿文件的生命周期

```csharp
public interface IDraftManager
{
    // 创建新草稿
    Task<(DraftContent, DraftMetaInfo)> CreateNewDraftAsync(string projectName, string projectPath);

    // 加载草稿
    Task<(DraftContent, DraftMetaInfo)> LoadDraftAsync(string draftDirectory);

    // 保存草稿
    Task SaveDraftAsync(string draftDirectory, DraftContent content, DraftMetaInfo meta);

    // 获取草稿目录
    string GetDraftDirectory(string projectPath);
}
```

**草稿目录结构：**
```
ProjectDirectory/
└── draft/
    ├── draft_content.json      # 时间轴内容
    ├── draft_meta_info.json    # 元信息
    └── materials/              # 视频素材（可选）
        ├── shot_001.mp4
        └── shot_002.mp4
```

### 2. DraftAdapter (`Infrastructure/Services/DraftAdapter.cs`)

**职责：** 在 ShotItem 和 CapCut 格式之间转换

```csharp
public static class DraftAdapter
{
    // 将 ShotItem 列表同步到草稿
    void SyncShotsToDraft(List<ShotItem> shots, DraftContent draft);

    // 从草稿提取时间轴信息
    TimelineInfo ExtractTimelineInfo(DraftContent draft);

    // 添加/删除/更新片段
    void AddSegmentToDraft(DraftContent draft, ShotItem shot, double startTime);
    void RemoveSegmentFromDraft(DraftContent draft, string segmentId);
    void UpdateSegmentPosition(DraftContent draft, string segmentId, double newStartTime);

    // 时间转换
    long SecondsToMicroseconds(double seconds);
    double MicrosecondsToSeconds(long microseconds);
}
```

**关键转换：**
- 时间单位：秒 ↔ 微秒（1秒 = 1,000,000 微秒）
- 数据结构：ShotItem ↔ Segment + VideoMaterial

### 3. TimelineEditorViewModel (重构版)

**核心变化：**

```csharp
public partial class TimelineEditorViewModel : ObservableObject
{
    // 核心数据：CapCut 草稿
    private DraftContent? _draftContent;
    private DraftMetaInfo? _draftMetaInfo;

    // 自动保存定时器
    private System.Timers.Timer? _autoSaveTimer;
    private bool _isDirty;

    // 加载或创建草稿
    Task LoadOrCreateDraftAsync(string projectPath, string projectName);

    // 从 Shots 同步到草稿
    Task SyncShotsToTimelineAsync();

    // 从草稿构建 UI
    void BuildTimelineFromDraft();
}
```

**工作流程：**

1. **项目加载时：**
   ```csharp
   // 接收 ProjectDataLoadedMessage
   await LoadOrCreateDraftAsync(projectPath, projectName);
   // 如果草稿存在 → 加载
   // 如果草稿不存在 → 创建新草稿
   ```

2. **Shot 变化时：**
   ```csharp
   // 接收 ShotAddedMessage/ShotUpdatedMessage/ShotDeletedMessage
   await SyncShotsToTimelineAsync();
   // 使用 DraftAdapter 同步数据
   // 标记为脏数据（_isDirty = true）
   ```

3. **自动保存：**
   ```csharp
   // 每 5 秒检查
   if (_isDirty && _draftContent != null)
   {
       await SaveDraftAsync();
       _isDirty = false;
   }
   ```

### 4. ExportViewModel (重构版)

**新增导出方法：**

```csharp
// 方法 1：直接复制草稿（推荐）
Task ExportToCapCutDirect(string outputDirectory);
// 直接复制项目的 draft/ 目录

// 方法 2：从 ShotItem 构建（兼容）
Task ExportToCapCut(string outputDirectory);
// 使用 CapCutExportService 从 ShotItem 构建

// 方法 3：合成最终视频（原有）
Task ExportVideo(string outputPath);
// 使用 FFmpeg 合成所有片段
```

## 数据流

### 完整数据流

```
用户操作
    ↓
ShotItem 变化（内存）
    ↓
发送 ShotAddedMessage/ShotUpdatedMessage
    ↓
TimelineEditorViewModel 接收消息
    ↓
调用 DraftAdapter.SyncShotsToDraft()
    ↓
更新 DraftContent（内存）
    ↓
标记为脏数据（_isDirty = true）
    ↓
5 秒后自动保存
    ↓
DraftManager.SaveDraftAsync()
    ↓
写入 draft_content.json 和 draft_meta_info.json（磁盘）
```

### 项目加载流程

```
打开项目
    ↓
发送 ProjectDataLoadedMessage
    ↓
TimelineEditorViewModel.LoadOrCreateDraftAsync()
    ↓
检查 draft/ 目录是否存在
    ↓
存在：DraftManager.LoadDraftAsync()
不存在：DraftManager.CreateNewDraftAsync()
    ↓
BuildTimelineFromDraft()
    ↓
显示时间轴 UI
```

### 导出流程

```
用户点击"导出到 CapCut"
    ↓
ExportViewModel.ExportToCapCutDirect()
    ↓
获取项目路径
    ↓
获取草稿目录（projectPath/draft/）
    ↓
复制整个 draft/ 目录到导出位置
    ↓
重命名为 CapCut_Draft_{项目名}_{时间戳}
    ↓
打开导出目录
```

## 迁移指南

### 对于开发者

#### 1. 注册服务

在 `App.axaml.cs` 中已注册：

```csharp
services.AddSingleton<IDraftManager, DraftManager>();
```

#### 2. 使用新的 TimelineEditorViewModel

**旧代码：**
```csharp
// 构建时间轴
timelineEditor.BuildTimelineFromShots();
```

**新代码：**
```csharp
// 加载草稿
await timelineEditor.LoadOrCreateDraftAsync(projectPath, projectName);

// 同步 Shots 到草稿
await timelineEditor.SyncShotsToTimelineAsync();
```

#### 3. 发送项目路径信息

确保 `ProjectDataLoadedMessage` 包含项目路径：

```csharp
// 需要在 MainViewModel 中注册处理器
_messenger.Register<GetCurrentProjectPathQuery>(this, (r, query) =>
{
    query.ProjectPath = GetCurrentProjectPath(); // 实现此方法
});
```

#### 4. 导出到 CapCut

**旧代码：**
```csharp
await exportViewModel.ExportToCapCut(outputDirectory);
// 需要从 ShotItem 构建草稿
```

**新代码：**
```csharp
await exportViewModel.ExportToCapCutDirect(outputDirectory);
// 直接复制现有草稿
```

### 对于用户

#### 项目文件结构变化

**旧结构：**
```
MyProject/
├── project.db          # SQLite 数据库
└── outputs/            # 生成的视频
```

**新结构：**
```
MyProject/
├── project.db          # SQLite 数据库
├── draft/              # CapCut 草稿（新增）
│   ├── draft_content.json
│   ├── draft_meta_info.json
│   └── materials/
└── outputs/            # 生成的视频
```

#### 导出选项

1. **导出为最终视频** - 合成所有片段为一个 MP4 文件
2. **导出到 CapCut（直接）** - 复制草稿目录，可在 CapCut 中打开
3. **导出到 CapCut（构建）** - 从当前镜头重新构建草稿

## 优势与权衡

### 优势

1. **数据标准化**
   - 使用行业标准格式（CapCut）
   - 便于与其他工具集成

2. **实时持久化**
   - 自动保存，防止数据丢失
   - 可以随时在 CapCut 中查看项目

3. **简化导出**
   - 导出即复制，无需转换
   - 导出速度快

4. **可扩展性**
   - 未来可支持更多编辑器格式
   - 可以直接编辑 JSON 文件

5. **调试友好**
   - JSON 格式易于查看和调试
   - 可以手动修改草稿文件

### 权衡

1. **磁盘空间**
   - 草稿文件会占用额外空间
   - 如果复制素材到 materials/ 目录，空间占用更大

2. **性能考虑**
   - 每 5 秒写入 JSON 文件
   - 大型项目的 JSON 文件可能较大

3. **兼容性**
   - 旧项目需要迁移
   - 需要确保 CapCut 版本兼容

## 测试建议

### 单元测试

1. **DraftManager 测试**
   ```csharp
   [Test]
   public async Task CreateNewDraft_ShouldCreateFiles()
   {
       var draft = await draftManager.CreateNewDraftAsync("Test", "/path");
       Assert.IsTrue(File.Exists("/path/draft/draft_content.json"));
   }
   ```

2. **DraftAdapter 测试**
   ```csharp
   [Test]
   public void SyncShotsToDraft_ShouldConvertCorrectly()
   {
       var shots = CreateTestShots();
       var draft = new DraftContent();
       DraftAdapter.SyncShotsToDraft(shots, draft);
       Assert.AreEqual(shots.Count, draft.Tracks[0].Segments.Count);
   }
   ```

### 集成测试

1. **完整工作流测试**
   - 创建项目 → 添加镜头 → 生成视频 → 同步到草稿 → 导出到 CapCut
   - 验证草稿文件内容正确
   - 在 CapCut 中打开验证

2. **自动保存测试**
   - 修改镜头 → 等待 5 秒 → 检查文件是否更新

3. **项目加载测试**
   - 创建项目 → 关闭 → 重新打开 → 验证草稿加载正确

## 故障排查

### 问题 1：草稿文件未创建

**症状：** 项目目录下没有 `draft/` 目录

**解决：**
1. 检查 `IDraftManager` 是否已注册
2. 检查 `LoadOrCreateDraftAsync` 是否被调用
3. 检查项目路径是否正确

### 问题 2：自动保存不工作

**症状：** 修改镜头后草稿文件未更新

**解决：**
1. 检查 `_isDirty` 标志是否被设置
2. 检查自动保存定时器是否启动
3. 查看日志中的保存记录

### 问题 3：导出的草稿在 CapCut 中无法打开

**症状：** CapCut 提示"无法打开项目"

**解决：**
1. 检查 JSON 格式是否正确
2. 检查视频文件路径是否有效
3. 检查 CapCut 版本兼容性
4. 验证时间单位转换（微秒）

### 问题 4：时间轴显示不正确

**症状：** 片段位置或时长错误

**解决：**
1. 检查时间转换（秒 ↔ 微秒）
2. 检查 `PixelsPerSecond` 设置
3. 验证 `BuildTimelineFromDraft` 逻辑

## 未来改进

1. **增量保存**
   - 只保存变化的部分，而非整个文件
   - 使用 JSON Patch 格式

2. **版本控制**
   - 保存草稿历史版本
   - 支持撤销/重做

3. **素材管理**
   - 可选择是否复制素材到 materials/
   - 支持相对路径和绝对路径

4. **多格式支持**
   - 支持导出到 Premiere Pro
   - 支持导出到 DaVinci Resolve

5. **协作功能**
   - 草稿文件可以通过 Git 共享
   - 支持多人协作编辑

## 总结

本次重构将分镜大师的核心数据格式统一为 CapCut 草稿格式，实现了：

✅ **数据标准化** - 使用行业标准格式
✅ **实时持久化** - 自动保存到磁盘
✅ **简化导出** - 导出即复制
✅ **可扩展性** - 易于支持更多格式
✅ **调试友好** - JSON 格式易于查看

这为分镜大师提供了更强大、更灵活的架构基础。

## 相关文件

### 新增文件
- `Infrastructure/Services/DraftManager.cs` - 草稿管理服务
- `Infrastructure/Services/DraftAdapter.cs` - 数据适配器
- `App/ViewModels/Timeline/TimelineEditorViewModelRefactored.cs` - 重构的时间轴 ViewModel
- `App/ViewModels/Generation/ExportViewModelRefactored.cs` - 重构的导出 ViewModel

### 修改文件
- `App/App.axaml.cs` - 注册 IDraftManager 服务
- `App/Messages/ShotMessages.cs` - 添加 GetCurrentProjectPathQuery

### 模板文件
- `resources/templates/capcut/draft_content_template.json`
- `resources/templates/capcut/draft_meta_info.json`

### 模型文件
- `Shared/Models/CapCut/DraftContent.cs`
- `Shared/Models/CapCut/DraftMetaInfo.cs`
