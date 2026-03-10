# TTS 语音合成功能实现文档（更新于 2026-03-05）

## 1. 实现范围

本文档覆盖当前项目中 TTS 能力的服务层实现、配置结构、UI 触发链路与已知限制。

当前已交付：

- NewAPI TTS Provider（OpenAI 兼容接口）
- TTS 服务层（单镜头 + 批量）
- 单镜头 UI 配音
- 单镜头音频上传
- 单镜头内置播放器（播放/暂停/停止/进度）
- 批量配音对话框
- CapCut 草稿导出中的音轨读取（基于 `GeneratedAudioPath`）

## 2. 核心架构

### 2.1 关键接口与类型

- `Infrastructure/Media/TtsProviders.cs`
  - `ITtsProvider`
  - `TtsGenerationRequest`
  - `TtsGenerationResult`
- `Infrastructure/AI/Core/MediaProviderTypes.cs`
  - `TtsProviderType`（`Qwen` / `Volcengine` / `NewApi`）

### 2.2 Provider 实现

- `Infrastructure/Media/Providers/NewApiTtsProvider.cs`
  - 接口路径：`POST /v1/audio/speech`
  - 默认模型兜底：`tts-1`
  - 支持音色：`alloy`, `echo`, `fable`, `onyx`, `nova`, `shimmer`
  - 支持格式：`mp3`, `opus`, `aac`, `flac`, `wav`, `pcm`
  - 语速范围：`0.25 ~ 4.0`
  - 支持 `ProviderHint` 透传（用于 NewAPI 路由）

### 2.3 服务层

- `Application/Abstractions/ITtsService.cs`
  - `GenerateAsync`
  - `GenerateForShotAsync`
  - `GenerateBatchAsync`
  - `GetAvailableProviders` / `GetProvider` / `GetDefaultProvider`
- `Infrastructure/Services/TtsService.cs`
  - 单镜头默认输出目录：
    - `Documents/Storyboard/output/projects/{ProjectId}/audio/`
  - 文件命名：
    - `shot_{ShotId}_audio.mp3`
  - 批量策略：
    - 顺序执行
    - 单镜头异常只记录日志，不中断剩余镜头

## 3. 配置结构

核心配置位于 `ai.defaults.json`：

```json
{
  "Providers": {
    "NewApi": {
      "Endpoint": "http://127.0.0.1:3000/v1",
      "ApiKey": "YOUR_KEY",
      "Enabled": true,
      "TimeoutSeconds": 120,
      "DefaultModels": {
        "Tts": "tts-1"
      }
    }
  },
  "Tts": {
    "DefaultProvider": "NewApi",
    "Providers": {
      "NewApi": {
        "Voice": "alloy",
        "Speed": 1.0,
        "ResponseFormat": "mp3",
        "ProviderHint": ""
      }
    }
  }
}
```

说明：

- `DefaultProvider`：默认 Provider 类型。
- `Voice` / `Speed` / `ResponseFormat`：Provider 级默认参数。
- `ProviderHint`：可选路由提示参数。

## 4. 调用链路

### 4.1 单镜头配音

1. `ShotEditorView.axaml` 触发 `RequestGenerateAudioCommand`。
2. `ShotItem.GenerateAudioRequested` 事件被 `ShotListViewModel` 捕获。
3. `ShotListViewModel` 发送 `AudioGenerationRequestedMessage`。
4. `AudioGenerationViewModel` 调用 `ITtsService.GenerateForShotAsync`。
5. 结果回写到 `ShotItem.GeneratedAudioPath`，并发送完成消息。
6. `ShotEditorView.axaml.cs` 提供内置音频播放控制（不走系统默认播放器）。

### 4.2 批量配音

1. `MainWindow.axaml` 调用 `ShotList.BatchGenerateAudioCommand`。
2. `ShotListViewModel` 发送 `BatchAudioGenerationRequestedMessage`。
3. `MainViewModel` 打开 `BatchAudioGenerationDialog`。
4. `BatchAudioGenerationViewModel` 循环调用 `GenerateForShotAsync`。
5. 更新进度、成功/失败/跳过统计，并回写镜头状态。

## 5. 导出链路现状

- `Infrastructure/Services/CapCutExportService.cs` 会尝试读取镜头的 `GeneratedAudioPath` 并注入音轨。
- 该能力属于“部分集成”：已能挂载音频素材，但尚不等同于完整音频编排能力（无多轨编辑、无混音控制 UI）。

## 6. 关键文件清单

### 后端与服务

- `Infrastructure/Media/TtsProviders.cs`
- `Infrastructure/Media/Providers/NewApiTtsProvider.cs`
- `Application/Abstractions/ITtsService.cs`
- `Infrastructure/Services/TtsService.cs`
- `Infrastructure/AI/Core/AIServiceConfig.cs`

### UI 与消息

- `App/Views/ShotEditorView.axaml`
- `Shared/Models/ShotItem.cs`
- `App/Messages/GenerationMessages.cs`
- `App/ViewModels/Generation/AudioGenerationViewModel.cs`
- `App/ViewModels/Generation/BatchAudioGenerationViewModel.cs`
- `App/Views/BatchAudioGenerationDialog.axaml`
- `App/ViewModels/Shot/ShotListViewModel.cs`
- `App/ViewModels/MainViewModel.cs`

## 7. 已知限制

1. 播放器覆盖范围有限  
   当前内置播放器主要覆盖镜头编辑器内入口，其他入口仍待统一。

2. 音频编辑能力未覆盖  
   仍缺少波形、裁剪、混音与淡入淡出等编辑能力。

## 8. 建议下一步

### P0

- 批量配音任务与全局任务队列整合（可观测、可取消、可重试）。

### P1

- 时间轴音频轨道、音频剪辑、混音与导出参数控制。
