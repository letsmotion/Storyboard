<h1 align="center">🎬 Storyboard Studio</h1>

<p align="center"><b>Professional local storyboard workbench: video import → intelligent frame extraction → AI analysis → image/video generation → batch jobs → final render</b></p>

<p align="center"><a href="README.md">中文</a> | English</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4" alt=".NET 8">
  <img src="https://img.shields.io/badge/Avalonia-11.x-3A7CF0" alt="Avalonia">
  <img src="https://img.shields.io/badge/SQLite-Embedded-003B57" alt="SQLite">
  <img src="https://img.shields.io/badge/FFmpeg-Bundled-4CBB17" alt="FFmpeg">
  <img src="https://img.shields.io/badge/AI-Multi%20Provider-FF6B6B" alt="AI Providers">
</p>

> 🚀 Professional storyboard tool for video creators! Import videos or enter text descriptions to quickly generate complete storyboard scripts and assets. Supports Qwen, Volcengine, and other AI platforms, combining local rendering with cloud model calls for efficient creative production.

---

## 📖 Introduction

**Storyboard Studio** is a professional local storyboard workbench application that provides video creators and production teams with complete workflow support from creative ideation to final production.

### 🎯 Core Value

- **Two Creation Modes**: Video import auto-storyboarding + text description storyboard generation
- **AI Intelligent Analysis**: Automatically identify shot types, scene settings, and action commands
- **Professional Parameter Control**: Support for composition, lighting, color tone, lens type, and other professional parameters
- **Creative Intent Integration**: Incorporate creative goals, target audience, and video tone into AI generation
- **Batch Task Processing**: Analysis, generation, and composition tasks execute independently without interference
- **Local Deployment**: Local data storage, bundled FFmpeg, no external dependencies

---

## ✨ Key Features

### 📁 Project Management
- ✅ Create/open/switch projects with SQLite local persistence
- ✅ Recent project history
- ✅ Project-level metadata management (creative intent, video info)

### 🎥 Video Import & Analysis
- ✅ Support mainstream video format imports
- ✅ Automatic video metadata extraction (duration/resolution/frame rate)
- ✅ FFprobe intelligent video analysis

### 🖼️ Intelligent Frame Extraction (Four Modes)
- ✅ **Fixed Count Mode**: Extract specified number of key frames
- ✅ **Dynamic Interval Mode**: Dynamically adjust intervals based on scene changes
- ✅ **Equal Time Mode**: Extract at fixed time intervals
- ✅ **Keyframe Detection**: Intelligently identify key frames based on scene changes

### ✏️ Storyboard Editing
- ✅ Full shot field editing (shot type, core content, action commands, scene settings)
- ✅ Drag-and-drop reordering for flexible shot arrangement
- ✅ Timeline visualization
- ✅ Multiple view modes: Grid, List, Timeline

### 🤖 AI Intelligent Features

#### AI Shot Analysis
- ✅ Analyze first/last frame features to generate structured shot descriptions
- ✅ Three processing strategies: Overwrite/Append/Skip
- ✅ Integrate creative intent (creative goals, target audience, video tone, key messages)

#### Text-to-Storyboard
- ✅ Automatically split natural language descriptions into multiple shots
- ✅ Intelligently identify scene transitions and shot changes
- ✅ Support creative intent-guided generation

### 🎨 Asset Generation

#### Image Generation
- ✅ Independent generation of first and last frames
- ✅ Professional parameter support: composition, lighting, color tone, lens type
- ✅ Multi-generation history retention with explicit user binding
- ✅ Support for Qwen and Volcengine Seedream

#### Video Generation
- ✅ Generate video clips based on shot descriptions
- ✅ Support scene description, action description, style description
- ✅ Camera movement and effects parameter configuration
- ✅ Support for Volcengine Seedance

### ⚙️ Configuration Management
- ✅ Multi-provider support (Qwen, Volcengine)
- ✅ Multi-model configuration with independent text/image/video settings
- ✅ In-app visual configuration interface
- ✅ Local rendering and cloud models coexist

### 📊 Batch Tasks & Task Management
- ✅ Batch analysis, generation, and composition tasks
- ✅ Task queue management with cancel/retry/delete support
- ✅ Task history records
- ✅ Independent task execution without interference

### 📤 Export & Output
- ✅ Export storyboard JSON files
- ✅ FFmpeg final video composition
- ✅ Output directories: `output/projects/<ProjectId>/images` and `videos`

---

## 🌐 Web Demo
UI only, no backend implementation
Demo URL: http://47.100.163.84/

---

## 🖼️ UI Preview

### Home - Create Project
![Home](resources/home.png)

### Main Page - Project Workspace
![Main](resources/main.png)

### Storyboard Editing - Shot Management
![Storyboard](resources/storyboard.png)

### Batch Generation - Task Processing
![Batch](resources/batch.png)

### Task Management - Queue Monitoring
![Task Management](resources/taskmanage.png)

### Export - Video Composition
![Export](resources/export.png)

### AI Configuration - Model Settings
![AI Provider](resources/AiProder.png)

---

## 🧭 Complete Workflow

```
Create Project → Import Video/Enter Text → Extract Frames/AI Storyboard → Edit Shots → Generate Images → Generate Videos → Compose Final Video → Export
```

### Workflow Details

1. **Create Project** → SQLite database creates project record
2. **Import Video** → FFprobe extracts metadata (duration, resolution, frame rate)
3. **Intelligent Frame Extraction** → FFmpeg extracts key frames based on selected mode
4. **AI Analysis** → Qwen/Volcengine analyzes first/last frames, generates shot descriptions
5. **Text-to-Storyboard** → Enter text prompt, AI automatically generates multiple shots
6. **Edit Shots** → Manually adjust shot parameters, order, descriptions
7. **Generate Images** → Volcengine Seedream generates first/last frame images
8. **Generate Videos** → Volcengine Seedance generates video clips
9. **Compose Final Video** → FFmpeg combines all shots into final video
10. **Export** → Save storyboard JSON and final video file

Each stage can execute independently, supporting manual editing and batch task processing.

---

## 🚀 Quick Start

### Requirements
- .NET 8.0 SDK
- Windows / Linux / macOS (cross-platform support)

### Running Methods

#### Method 1: Command Line
```bash
# Clone project
git clone <repository-url>
cd 分镜大师

# Restore dependencies
dotnet restore

# Build project
dotnet build

# Run application
dotnet run
```

#### Method 2: Visual Studio 2022
1. Open `Storyboard.sln`
2. Press F5 to run directly

#### Method 3: Release Version
1. Download Release version
2. Extract and run `Storyboard.exe` directly

---

## ⚙️ Configuration Management (Multi-model / Local Model)

### Configuration Entry Points
- **Recommended**: In-app "Provider Settings" interface (visual configuration)
- **Advanced**: Edit `appsettings.json` directly

### Supported AI Providers

#### Text Understanding
- Qwen (Alibaba)
- Volcengine (ByteDance)
- OpenAI
- Azure OpenAI

#### Image Generation
- Qwen
- Volcengine Seedream

#### Video Generation
- Volcengine Seedance

### Configuration Structure Example

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

### Configuration Capabilities
- ✅ Multiple providers and models coexist
- ✅ UI allows selection of default provider
- ✅ Local rendering/composition and cloud models can be configured in parallel
- ✅ Switch between different models per task

---

## 🗂️ Project Structure

```
分镜大师/
├─ App/                          # Avalonia UI Layer
│  ├─ Views/                     # XAML View Components
│  ├─ ViewModels/                # MVVM ViewModels
│  ├─ Converters/                # Value Converters
│  ├─ Messages/                  # Messaging
│  └─ App.axaml.cs               # Dependency Injection Configuration
│
├─ Application/                  # Application Layer (Use Cases)
│  ├─ Abstractions/              # Service Interfaces
│  ├─ Services/                  # Application Services
│  └─ DTOs/                      # Data Transfer Objects
│
├─ Domain/                       # Domain Layer (Business Logic)
│  └─ Entities/                  # Core Domain Models
│     ├─ Project.cs              # Project Entity
│     ├─ Shot.cs                 # Shot Entity
│     └─ ShotAsset.cs            # Asset Entity
│
├─ Infrastructure/               # Infrastructure Layer
│  ├─ AI/                        # AI Service Integration
│  │  ├─ Core/                   # AI Provider Interfaces
│  │  ├─ Providers/              # Qwen, Volcengine Implementations
│  │  ├─ Prompts/                # Prompt Template Management
│  │  └─ AIServiceManager.cs     # AI Service Orchestration
│  ├─ Media/                     # Media Processing
│  │  ├─ Providers/              # Image/Video Generation Providers
│  │  └─ FfmpegLocator.cs        # FFmpeg Integration
│  ├─ Persistence/               # Database & EF Core
│  ├─ Configuration/             # Configuration Management
│  └─ Services/                  # Infrastructure Services
│
├─ Shared/                       # Shared Models & DTOs
├─ Tools/ffmpeg/                 # Bundled FFmpeg/FFprobe
├─ appsettings.json              # Application Configuration
└─ Storyboard.sln                # Solution File
```

### Architecture Highlights
- **Layered Architecture**: Domain / Application / Infrastructure / App
- **MVVM Pattern**: Reactive UI with MVVM Toolkit + Messenger
- **Dependency Injection**: Complete DI configuration
- **Async Programming**: Comprehensive use of async/await
- **Multi-threading**: Task queue with concurrency support (default: 2 concurrent tasks)

---

## 📦 Data & Output

### Data Storage
- **Database Location**: `Data/storyboard.db` (under app base directory)
- **Database Type**: SQLite + Entity Framework Core
- **Migration Support**: Automatic incremental migrations on startup

### Output Directories
- **Image Output**: `output/projects/<ProjectId>/images/`
- **Video Output**: `output/projects/<ProjectId>/videos/`
- **Storyboard Export**: JSON format

---

## 🧰 FFmpeg Dependency

Project includes bundled `Tools/ffmpeg`:
- `ffmpeg.exe`: Video processing and composition
- `ffprobe.exe`: Video metadata analysis

Video import, frame extraction, and local video composition automatically use bundled FFmpeg, no additional installation required.

---

## 🧪 Tech Stack

| Technology Domain | Technology Choice |
|------------------|-------------------|
| **Framework** | .NET 8 + Avalonia 11.x |
| **UI Framework** | Avalonia (Cross-platform XAML) |
| **Architecture Pattern** | MVVM + Layered Architecture |
| **State Management** | MVVM Toolkit (CommunityToolkit.Mvvm) |
| **Messaging** | MVVM Messenger (WeakReferenceMessenger) |
| **Database** | SQLite + Entity Framework Core 8.0 |
| **ORM** | EF Core (with migrations) |
| **Logging** | Serilog (Console, Debug, File) |
| **Media Processing** | FFmpeg/FFprobe (bundled) |
| **Image Processing** | SkiaSharp 2.88.9 |
| **AI Integration** | Semantic Kernel + Multi-provider Adapters |
| **HTTP Client** | Microsoft.Extensions.Http |
| **Dependency Injection** | Microsoft.Extensions.DependencyInjection |
| **Configuration** | Microsoft.Extensions.Configuration (JSON) |
| **WebView** | WebView.Avalonia |

---

## 🎯 Core Features

### 1. Creative Intent Integration
During AI generation, you can set:
- **Creative Goal**: Core purpose of the video
- **Target Audience**: Intended viewer demographic
- **Video Tone**: Overall style and atmosphere
- **Key Messages**: Core content that must be conveyed

AI generates storyboards and assets that better match expectations based on these intents.

### 2. Professional Parameter Control
Supports cinema-grade professional parameters:
- **Shot Types**: Close-up, medium shot, full shot, long shot, etc.
- **Composition**: Rule of thirds, symmetry, golden ratio, etc.
- **Lighting**: Natural light, soft light, backlight, side light, etc.
- **Color Tone**: Warm tone, cool tone, high contrast, etc.
- **Camera Movement**: Push/pull/pan/tilt, follow, orbit, etc.

### 3. Asset Management
- Each shot can generate images/videos multiple times
- Retain all generation history
- User explicitly selects and binds best assets
- Support asset preview and comparison

### 4. Batch Task Processing
- Task queue executes independently
- Support task cancel, retry, delete
- Real-time task progress monitoring
- Task history query

---

## 🗺️ Product Roadmap

### Coming Soon
- 🔊 TTS Intelligent Voiceover
- ✂️ Auto-editing Optimization
- 🎨 Automatic Style Transfer
- 📱 One-click Social Platform Publishing

### Future Plans
- 🎵 Background Music Intelligent Matching
- 📊 Data Analysis & Optimization Suggestions
- 👥 Team Collaboration Features
- ☁️ Cloud Sync & Backup

---

## 🤝 Contributing

Welcome to submit Issues and Pull Requests!

### Development Environment
1. Install .NET 8.0 SDK
2. Install Visual Studio 2022 or JetBrains Rider
3. Clone project and restore dependencies

### Submission Guidelines
- Follow existing code style
- Add necessary unit tests
- Update relevant documentation

---

## 📄 License

This project is licensed under the MIT License. See LICENSE file for details.

---

## 📧 Contact

- Issue Feedback: Submit GitHub Issue
- Feature Suggestions: Submit GitHub Discussion
- Business Cooperation: Contact via GitHub

---

<p align="center">
  <b>Make creativity land faster, make storyboards more professional</b><br>
  Start your efficient video creation journey with Storyboard Studio!
</p>
