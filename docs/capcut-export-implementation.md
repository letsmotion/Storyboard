# CapCut 导出功能实现总结

## 概述

本次实现为分镜大师项目添加了 **CapCut 草稿导出功能**，允许用户将时间轴上的视频片段导出为 CapCut 可识别的草稿格式，方便在 CapCut 中进行进一步编辑。

## 文件结构

### 1. 模板文件位置

模板文件存放在 `resources/templates/capcut/` 目录下：

```
resources/
└── templates/
    └── capcut/
        ├── draft_content_template.json    # 草稿内容模板
        └── draft_meta_info.json           # 草稿元信息模板
```

**为什么选择这个位置？**
- `resources/` 目录是应用资源的标准存放位置
- `templates/` 子目录清晰地表明这些是模板文件
- `capcut/` 子目录便于未来扩展其他编辑器的导出格式（如 Premiere、DaVinci Resolve 等）

### 2. 模型文件

创建了两个 C# 模型类来映射 CapCut 的 JSON 格式：

#### `Shared/Models/CapCut/DraftContent.cs`
- 包含完整的 CapCut 草稿内容结构
- 主要类：
  - `DraftContent` - 草稿主体
  - `CanvasConfig` - 画布配置（分辨率、比例）
  - `Materials` - 素材集合（视频、音频、图片等）
  - `Track` - 轨道
  - `Segment` - 片段
  - `TimeRange` - 时间范围

#### `Shared/Models/CapCut/DraftMetaInfo.cs`
- 包含草稿元信息结构
- 主要类：
  - `DraftMetaInfo` - 草稿元数据
  - `DraftEnterpriseInfo` - 企业信息
  - `DraftMaterial` - 草稿素材引用

### 3. 服务实现

#### `Infrastructure/Services/CapCutExportService.cs`

**核心功能：**
1. **加载模板** - 从 JSON 模板文件加载基础结构
2. **构建草稿内容** - 将项目的镜头数据转换为 CapCut 格式
3. **复制视频素材** - 将视频文件复制到草稿目录
4. **生成草稿文件** - 保存 `draft_content.json` 和 `draft_meta_info.json`

**关键实现细节：**
- 时间单位转换：CapCut 使用微秒（1秒 = 1,000,000 微秒）
- 自动跳过未生成的镜头
- 创建独立的草稿目录，包含所有必需的文件
- 使用相对路径引用素材文件

### 4. 集成到现有系统

#### 修改的文件：

**`App/App.axaml.cs`**
- 注册 `ICapCutExportService` 服务

```csharp
services.AddSingleton<ICapCutExportService, CapCutExportService>();
```

**`App/ViewModels/Generation/ExportViewModel.cs`**
- 添加 `ICapCutExportService` 依赖注入
- 新增 `ExportToCapCut` 命令方法
- 支持导出为 CapCut 草稿格式

**`App/Messages/ShotMessages.cs`**
- 添加 `GetProjectInfoQuery` 消息类，用于查询当前项目信息

**`App/ViewModels/MainViewModel.cs`**
- 注册 `GetProjectInfoQuery` 消息处理器

## 使用方法

### 从代码调用

```csharp
// 在 ExportViewModel 中
await ExportToCapCut(outputDirectory);
```

### 导出流程

1. 用户选择导出目录
2. 系统查询所有已生成的镜头
3. 创建草稿目录（格式：`CapCut_Draft_{项目名}_{时间戳}`）
4. 复制视频文件到 `materials/` 子目录
5. 生成 `draft_content.json` 和 `draft_meta_info.json`
6. 自动打开导出目录

### 导出的目录结构

```
CapCut_Draft_MyProject_20260128_143022/
├── draft_content.json          # 草稿内容
├── draft_meta_info.json        # 草稿元信息
└── materials/                  # 素材目录
    ├── shot_001.mp4
    ├── shot_002.mp4
    └── shot_003.mp4
```

## 技术架构

### 设计模式

1. **模板方法模式** - 使用 JSON 模板作为基础结构
2. **依赖注入** - 通过接口解耦服务实现
3. **消息传递** - 使用 Messenger 进行跨 ViewModel 通信

### 数据流

```
ShotItem (应用内部格式)
    ↓
CapCutExportService
    ↓
DraftContent + DraftMetaInfo (CapCut 格式)
    ↓
JSON 文件 + 视频素材
    ↓
CapCut 草稿目录
```

### 时间转换

```csharp
// 应用内部：秒（double）
double durationSeconds = 3.5;

// CapCut：微秒（long）
long durationMicroseconds = (long)(durationSeconds * 1_000_000);
// 结果：3,500,000 微秒
```

## 扩展性

### 支持其他编辑器

当前架构便于扩展到其他视频编辑器：

```
resources/templates/
├── capcut/
│   ├── draft_content_template.json
│   └── draft_meta_info.json
├── premiere/                    # 未来扩展
│   └── project_template.prproj
└── davinci/                     # 未来扩展
    └── project_template.drp
```

### 添加新的导出格式

1. 在 `resources/templates/` 下创建新目录
2. 在 `Shared/Models/` 下创建对应的模型类
3. 在 `Infrastructure/Services/` 下实现导出服务
4. 在 `App.axaml.cs` 中注册服务
5. 在 `ExportViewModel` 中添加导出命令

## 优势

### 1. 模板驱动
- 易于维护：修改模板文件即可调整导出格式
- 版本兼容：可以为不同版本的 CapCut 提供不同模板

### 2. 解耦设计
- 服务接口化：便于单元测试和替换实现
- 消息传递：ViewModel 之间松耦合

### 3. 用户友好
- 自动跳过未生成的镜头
- 自动打开导出目录
- 清晰的目录命名（包含项目名和时间戳）

### 4. 可扩展性
- 目录结构支持多种导出格式
- 服务架构便于添加新功能

## 注意事项

### 1. 视频分辨率
当前实现使用默认分辨率（1920x1080），未来可以从视频文件中读取实际分辨率：

```csharp
// 建议改进
var videoInfo = await FFmpeg.GetVideoInfoAsync(videoPath);
material.Width = videoInfo.Width;
material.Height = videoInfo.Height;
```

### 2. 时间精度
CapCut 使用微秒级精度，确保转换时不丢失精度：

```csharp
long microseconds = (long)(seconds * 1_000_000);
```

### 3. 文件路径
- 模板文件路径：使用 `AppDomain.CurrentDomain.BaseDirectory`
- 素材文件路径：使用相对路径（相对于草稿目录）

### 4. 错误处理
- 模板文件不存在时使用默认模板
- 视频文件不存在时跳过该镜头
- 导出失败时发送失败消息

## 测试建议

### 单元测试
1. 测试时间转换的准确性
2. 测试模板加载逻辑
3. 测试草稿内容构建

### 集成测试
1. 测试完整的导出流程
2. 测试导出的草稿能否在 CapCut 中打开
3. 测试不同数量的镜头（0个、1个、多个）

### 边界测试
1. 空项目导出
2. 超长视频导出
3. 特殊字符的项目名

## 未来改进方向

1. **支持音频轨道** - 当前只支持视频轨道
2. **支持字幕轨道** - 导出字幕信息
3. **支持转场效果** - 在片段之间添加转场
4. **支持特效** - 导出应用的特效
5. **批量导出** - 支持导出多个项目
6. **自定义模板** - 允许用户自定义导出模板
7. **预览功能** - 导出前预览草稿结构

## 总结

本次实现成功地将分镜大师的时间轴数据导出为 CapCut 草稿格式，为用户提供了更灵活的后期编辑选项。通过模板驱动和服务化的设计，系统具有良好的可维护性和可扩展性，为未来支持更多编辑器格式奠定了基础。

## 相关文件清单

### 新增文件
- `resources/templates/capcut/draft_content_template.json`
- `resources/templates/capcut/draft_meta_info.json`
- `Shared/Models/CapCut/DraftContent.cs`
- `Shared/Models/CapCut/DraftMetaInfo.cs`
- `Infrastructure/Services/CapCutExportService.cs`

### 修改文件
- `App/App.axaml.cs` - 服务注册
- `App/ViewModels/Generation/ExportViewModel.cs` - 添加导出命令
- `App/Messages/ShotMessages.cs` - 添加查询消息
- `App/ViewModels/MainViewModel.cs` - 注册消息处理器

### 接口定义
- `ICapCutExportService` - 定义在 `CapCutExportService.cs` 中
