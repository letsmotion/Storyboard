<h1 align="center">🎬 分镜大师 Storyboard Studio</h1>

<p align="center"><b>专业本地分镜工作台：视频导入 → 智能抽帧 → AI 解析 → 图片/视频生成 → 批量任务 → 成片合成</b></p>

<p align="center">中文 | <a href="README.en.md">English</a></p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4" alt=".NET 8">
  <img src="https://img.shields.io/badge/Avalonia-11.x-3A7CF0" alt="Avalonia">
  <img src="https://img.shields.io/badge/SQLite-Embedded-003B57" alt="SQLite">
  <img src="https://img.shields.io/badge/FFmpeg-Bundled-4CBB17" alt="FFmpeg">
  <img src="https://img.shields.io/badge/AI-Multi%20Provider-FF6B6B" alt="AI Providers">
</p>

> 🚀 专为视频创作者打造的专业分镜工具！只需导入视频或输入文本描述，即可快速生成完整分镜脚本与素材资产。支持通义千问、火山引擎等多 AI 平台，兼顾本地渲染与云端模型调用，让创意落地更高效。

---

## 📖 项目简介

**分镜大师**是一款专业的本地分镜工作台应用，为视频创作者和制作团队提供从创意到成片的完整工作流支持。

### 🎯 核心价值

- **两种创作模式**：视频导入自动分镜 + 文本描述生成分镜
- **AI 智能解析**：自动识别镜头类型、场景设置、动作指令
- **专业参数控制**：支持构图、光线、色调、镜头类型等专业参数
- **创意意图融合**：将创作目标、目标受众、视频基调融入 AI 生成
- **批量任务处理**：解析、生成、合成任务独立执行，互不干扰
- **本地化部署**：数据本地存储，FFmpeg 内置，无需外部依赖

---

## ✨ 功能亮点

### 📁 项目管理
- ✅ 创建/打开/切换项目，SQLite 本地持久化
- ✅ 最近项目历史记录
- ✅ 项目级元数据管理（创意意图、视频信息）

### 🎥 视频导入与分析
- ✅ 支持主流视频格式导入
- ✅ 自动提取视频元数据（时长/分辨率/帧率）
- ✅ FFprobe 智能视频分析

### 🖼️ 智能抽帧（四种模式）
- ✅ **定数模式**：提取指定数量的关键帧
- ✅ **动态间隔模式**：根据场景变化动态调整间隔
- ✅ **等时模式**：按固定时间间隔提取
- ✅ **关键帧检测**：基于场景变化智能识别关键帧

### ✏️ 分镜编辑
- ✅ 镜头字段全量编辑（镜头类型、核心内容、动作指令、场景设置）
- ✅ 拖拽排序，灵活调整镜头顺序
- ✅ 时间线可视化展示
- ✅ 多视图模式：网格、列表、时间线

### 🤖 AI 智能功能

#### AI 镜头解析
- ✅ 分析首尾帧特征，生成结构化镜头描述
- ✅ 三种处理策略：覆盖/追加/放弃
- ✅ 融合创意意图（创作目标、目标受众、视频基调、关键信息）

#### 文本生成分镜
- ✅ 自然语言描述自动拆分为多个镜头
- ✅ 智能识别场景转换和镜头切换
- ✅ 支持创意意图引导生成

### 🎨 素材生成

#### 图片生成
- ✅ 首帧、尾帧独立生成
- ✅ 专业参数支持：构图、光线、色调、镜头类型
- ✅ 多次生成保留历史，用户显式绑定
- ✅ 支持通义千问、火山引擎 Seedream

#### 视频生成
- ✅ 基于镜头描述生成视频片段
- ✅ 支持场景描述、动作描述、风格描述
- ✅ 镜头运动、特效参数配置
- ✅ 支持火山引擎 Seedance

### ⚙️ 配置管理
- ✅ 多 Provider 支持（通义千问、火山引擎）
- ✅ 多模型配置，文本/图片/视频各自独立
- ✅ 应用内可视化配置界面
- ✅ 本地渲染与云端模型并存

### 📊 批量任务与任务管理
- ✅ 批量解析、生成、合成任务
- ✅ 任务队列管理，支持取消/重试/删除
- ✅ 任务历史记录
- ✅ 独立任务执行，互不干扰

### 📤 导出与输出
- ✅ 导出分镜 JSON 文件
- ✅ FFmpeg 合成最终视频
- ✅ 输出目录：`output/projects/<ProjectId>/images` 和 `videos`

---

## 🌐 Web 演示
仅包含 UI 界面，无后端实现
演示地址：http://47.100.163.84/

---

## 🖼️ 界面预览

### 首页 - 创建项目
![首页](resources/home.png)

### 主页面 - 项目工作区
![主页面](resources/main.png)

### 分镜编辑 - 镜头管理
![分镜](resources/storyboard.png)

### 批量生成 - 任务处理
![批量](resources/batch.png)

### 任务管理 - 队列监控
![任务管理](resources/taskmanage.png)

### 导出成品 - 视频合成
![导出](resources/export.png)

### AI 配置 - 模型设置
![配置AI模型](resources/AiProder.png)

---

## 🧭 完整工作流

```
创建项目 → 导入视频/输入文本 → 抽帧/AI 分镜 → 编辑镜头 → 生成图片 → 生成视频 → 合成成片 → 导出
```

### 工作流详解

1. **创建项目** → SQLite 数据库创建项目记录
2. **导入视频** → FFprobe 提取元数据（时长、分辨率、帧率）
3. **智能抽帧** → FFmpeg 根据选定模式提取关键帧
4. **AI 解析** → 通义千问/火山引擎分析首尾帧，生成镜头描述
5. **文本生成分镜** → 输入文本提示词，AI 自动生成多个镜头
6. **编辑镜头** → 手动调整镜头参数、顺序、描述
7. **生成图片** → 火山引擎 Seedream 生成首尾帧图片
8. **生成视频** → 火山引擎 Seedance 生成视频片段
9. **合成成片** → FFmpeg 合并所有镜头为最终视频
10. **导出** → 保存分镜 JSON 和最终视频文件

每个环节都可独立执行，支持手动编辑与批量任务处理。

---

## 🚀 快速开始

### 环境要求
- .NET 8.0 SDK
- Windows / Linux / macOS（跨平台支持）

### 下载安装

#### 国内用户（推荐）
从 Gitee 下载，速度更快：
- 访问 [Gitee Release](https://gitee.com/YOUR_USERNAME/YOUR_REPO/releases)
- 下载最新版本的 `StoryboardSetup.exe`
- 运行安装程序

#### 国外用户
从 GitHub 下载：
- 访问 [GitHub Release](https://github.com/YOUR_USERNAME/YOUR_REPO/releases)
- 下载最新版本的 `StoryboardSetup.exe`
- 运行安装程序

**注意**: 请将上述链接中的 `YOUR_USERNAME/YOUR_REPO` 替换为实际的仓库地址。

### 运行方式

#### 方式一：命令行运行
```bash
# 克隆项目
git clone <repository-url>
cd 分镜大师

# 还原依赖
dotnet restore

# 编译项目
dotnet build

# 运行应用
dotnet run
```

#### 方式二：Visual Studio 2022
1. 打开 `Storyboard.sln`
2. 按 F5 直接运行

#### 方式三：发布版本
1. 下载 Release 版本（推荐从 Gitee 下载，国内速度更快）
2. 运行 `StoryboardSetup.exe` 安装
3. 安装后支持自动更新功能

---

## 🔄 自动更新

本项目支持 **Gitee + GitHub 双源自动更新**：

- ✅ **国内用户**: 优先使用 Gitee 更新源，速度快
- ✅ **国外用户**: 自动切换到 GitHub 更新源
- ✅ **智能切换**: 如果主源不可用，自动切换到备用源
- ✅ **增量更新**: 只下载变化的部分，节省流量
- ✅ **后台更新**: 不影响正常使用

### 更新流程
1. 应用启动 3 秒后自动检查更新
2. 发现新版本时显示通知栏
3. 点击"立即更新"下载并安装
4. 重启后即为最新版本

详细配置请参考：[Gitee + GitHub 双源发布指南](docs/GITEE_RELEASE_GUIDE.md)

---

## ⚙️ 配置管理（多模型 / 本地模型）

### 配置入口
- **推荐**：应用内「提供商设置」界面（可视化配置）
- **高级**：直接编辑 `appsettings.json`

### 支持的 AI 提供商

#### 文本理解
- 通义千问（Qwen）
- 火山引擎（Volcengine）
- OpenAI
- Azure OpenAI

#### 图片生成
- 通义千问（Qwen）
- 火山引擎 Seedream（Volcengine）

#### 视频生成
- 火山引擎 Seedance（Volcengine）

### 配置结构示例

```json
{
  "AIServices": {
    "Providers": {
      "Qwen": {
        "ApiKey": "your-api-key",
        "Endpoint": "https://dashscope.aliyuncs.com/compatible-mode/v1",
        "DefaultModels": {
          "Text": "qwen-plus",
          "Image": "wanx-v1"
        }
      },
      "Volcengine": {
        "ApiKey": "your-api-key",
        "Endpoint": "https://ark.cn-beijing.volces.com/api/v3",
        "DefaultModels": {
          "Text": "ep-xxx",
          "Image": "seedream-v1",
          "Video": "seedance-v1"
        }
      }
    },
    "Defaults": {
      "Text": { "Provider": "Volcengine", "Model": "ep-xxx" },
      "Image": { "Provider": "Volcengine", "Model": "seedream-v1" },
      "Video": { "Provider": "Volcengine", "Model": "seedance-v1" }
    }
  }
}
```

### 配置能力
- ✅ 多 Provider、多模型并存
- ✅ 界面可选择默认 Provider
- ✅ 本地渲染/本地合成与云端模型可并行配置
- ✅ 按任务选择与切换不同模型

---

## 🗂️ 项目结构

```
分镜大师/
├─ App/                          # Avalonia UI 层
│  ├─ Views/                     # XAML 视图组件
│  ├─ ViewModels/                # MVVM 视图模型
│  ├─ Converters/                # 值转换器
│  ├─ Messages/                  # 消息传递
│  └─ App.axaml.cs               # 依赖注入配置
│
├─ Application/                  # 应用层（用例）
│  ├─ Abstractions/              # 服务接口
│  ├─ Services/                  # 应用服务
│  └─ DTOs/                      # 数据传输对象
│
├─ Domain/                       # 领域层（业务逻辑）
│  └─ Entities/                  # 核心领域模型
│     ├─ Project.cs              # 项目实体
│     ├─ Shot.cs                 # 镜头实体
│     └─ ShotAsset.cs            # 素材资产实体
│
├─ Infrastructure/               # 基础设施层
│  ├─ AI/                        # AI 服务集成
│  │  ├─ Core/                   # AI 提供商接口
│  │  ├─ Providers/              # 通义千问、火山引擎实现
│  │  ├─ Prompts/                # 提示词模板管理
│  │  └─ AIServiceManager.cs     # AI 服务编排
│  ├─ Media/                     # 媒体处理
│  │  ├─ Providers/              # 图片/视频生成提供商
│  │  └─ FfmpegLocator.cs        # FFmpeg 集成
│  ├─ Persistence/               # 数据库与 EF Core
│  ├─ Configuration/             # 配置管理
│  └─ Services/                  # 基础设施服务
│
├─ Shared/                       # 共享模型与 DTO
├─ Tools/ffmpeg/                 # 内置 FFmpeg/FFprobe
├─ appsettings.json              # 应用配置文件
└─ Storyboard.sln                # 解决方案文件
```

### 架构特点
- **分层架构**：Domain / Application / Infrastructure / App
- **MVVM 模式**：响应式 UI，MVVM Toolkit + Messenger
- **依赖注入**：完整的 DI 配置
- **异步编程**：全面使用 async/await
- **多线程**：任务队列支持并发（默认 2 个并发任务）

---

## 📦 数据与输出

### 数据存储
- **数据库位置**：`Data/storyboard.db`（应用启动目录下）
- **数据库类型**：SQLite + Entity Framework Core
- **迁移支持**：启动时自动增量迁移

### 输出目录
- **图片输出**：`output/projects/<ProjectId>/images/`
- **视频输出**：`output/projects/<ProjectId>/videos/`
- **分镜导出**：JSON 格式

---

## 🧰 FFmpeg 依赖

项目已内置 `Tools/ffmpeg`，包含：
- `ffmpeg.exe`：视频处理与合成
- `ffprobe.exe`：视频元数据分析

视频导入、抽帧与本地视频合成会自动使用内置 FFmpeg，无需额外安装。

---

## 🧪 技术栈

| 技术领域 | 技术选型 |
|---------|---------|
| **框架** | .NET 8 + Avalonia 11.x |
| **UI 框架** | Avalonia（跨平台 XAML） |
| **架构模式** | MVVM + 分层架构 |
| **状态管理** | MVVM Toolkit (CommunityToolkit.Mvvm) |
| **消息传递** | MVVM Messenger (WeakReferenceMessenger) |
| **数据库** | SQLite + Entity Framework Core 8.0 |
| **ORM** | EF Core（支持迁移） |
| **日志** | Serilog（控制台、调试、文件） |
| **媒体处理** | FFmpeg/FFprobe（内置） |
| **图像处理** | SkiaSharp 2.88.9 |
| **AI 集成** | Semantic Kernel + 多提供商适配器 |
| **HTTP 客户端** | Microsoft.Extensions.Http |
| **依赖注入** | Microsoft.Extensions.DependencyInjection |
| **配置管理** | Microsoft.Extensions.Configuration（JSON） |
| **WebView** | WebView.Avalonia |

---

## 🎯 核心特性

### 1. 创意意图融合
在 AI 生成过程中，可以设置：
- **创作目标**：视频的核心目的
- **目标受众**：面向的观众群体
- **视频基调**：整体风格和氛围
- **关键信息**：必须传达的核心内容

AI 会根据这些意图生成更符合预期的分镜和素材。

### 2. 专业参数控制
支持电影级专业参数：
- **镜头类型**：特写、中景、全景、远景等
- **构图方式**：三分法、对称、黄金分割等
- **光线设置**：自然光、柔光、逆光、侧光等
- **色调风格**：暖色调、冷色调、高对比度等
- **镜头运动**：推拉摇移、跟随、环绕等

### 3. 素材资产管理
- 每个镜头可生成多次图片/视频
- 保留所有生成历史
- 用户显式选择绑定最佳素材
- 支持素材预览和对比

### 4. 批量任务处理
- 任务队列独立执行
- 支持任务取消、重试、删除
- 任务进度实时监控
- 任务历史记录查询

---

## 🗺️ 产品路线图

### 即将推出
- 🔊 TTS 智能配音
- ✂️ 自动剪辑优化
- 🎨 自动风格迁移
- 📱 社交平台一键发布

### 未来规划
- 🎵 背景音乐智能匹配
- 📊 数据分析与优化建议
- 👥 团队协作功能
- ☁️ 云端同步与备份

---

## 🤝 贡献指南

欢迎提交 Issue 和 Pull Request！

### 开发环境
1. 安装 .NET 8.0 SDK
2. 安装 Visual Studio 2022 或 JetBrains Rider
3. 克隆项目并还原依赖

### 提交规范
- 遵循现有代码风格
- 添加必要的单元测试
- 更新相关文档

---

## 📄 许可证

本项目采用 MIT 许可证。详见 LICENSE 文件。

---

## 📧 联系方式

- 问题反馈：提交 GitHub Issue
- 功能建议：提交 GitHub Discussion
- 商务合作：通过 GitHub 联系

---

<p align="center">
  <b>让创意更快落地，让分镜更加专业</b><br>
  用分镜大师，开启高效视频创作之旅！
</p>
