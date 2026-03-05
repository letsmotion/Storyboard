# TTS 语音合成功能实现文档

## 📋 实现概述

已完成 NewAPI TTS 语音合成功能的完整实现，包括：

### ✅ 已实现的功能

1. **核心接口和类型**
   - `ITtsProvider` - TTS 提供商接口
   - `TtsProviderType` - TTS 提供商类型枚举
   - `TtsGenerationRequest` - TTS 生成请求
   - `TtsGenerationResult` - TTS 生成结果

2. **NewAPI TTS 提供商**
   - `NewApiTtsProvider` - 实现 OpenAI 兼容的 TTS API
   - 支持的模型：`tts-1`, `tts-1-hd`
   - 支持的音色：`alloy`, `echo`, `fable`, `onyx`, `nova`, `shimmer`
   - 支持的格式：`mp3`, `opus`, `aac`, `flac`, `wav`, `pcm`
   - 支持语速控制：0.25 - 4.0

3. **TTS 服务**
   - `ITtsService` - TTS 服务接口
   - `TtsService` - TTS 服务实现
   - 支持单个镜头生成配音
   - 支持批量镜头生成配音
   - 自动管理输出路径

4. **数据库支持**
   - 为 `Shot` 实体添加音频相关字段：
     - `AudioText` - 配音文本
     - `GeneratedAudioPath` - 生成的音频文件路径
     - `TtsVoice` - TTS 音色
     - `TtsSpeed` - TTS 语速
     - `TtsModel` - TTS 模型
     - `AudioDuration` - 音频时长
     - `GenerateAudio` - 是否生成音频
   - 数据库迁移文件：`20260305000000_AddAudioFieldsToShot.cs`

5. **配置支持**
   - 更新 `AIServicesConfiguration` 支持 TTS 配置
   - 更新 `ai.defaults.json` 添加 TTS 默认配置
   - 支持提供商级别的配置覆盖

6. **依赖注入**
   - 在 `App.axaml.cs` 中注册 TTS 服务
   - 自动注入到需要的 ViewModel 中

## 🚀 使用方法

### 1. 基本使用

```csharp
// 注入 ITtsService
public class YourViewModel
{
    private readonly ITtsService _ttsService;

    public YourViewModel(ITtsService ttsService)
    {
        _ttsService = ttsService;
    }

    // 生成语音
    public async Task GenerateSpeechAsync()
    {
        var result = await _ttsService.GenerateAsync(
            text: "这是一段测试文本",
            voice: "alloy",
            speed: 1.0,
            responseFormat: "mp3",
            outputPath: "output/test.mp3"
        );

        Console.WriteLine($"生成音频：{result.AudioBytes.Length} 字节");
        Console.WriteLine($"预估时长：{result.DurationSeconds:F2} 秒");
    }
}
```

### 2. 为镜头生成配音

```csharp
// 为单个镜头生成配音
public async Task GenerateAudioForShotAsync(long shotId)
{
    var audioPath = await _ttsService.GenerateForShotAsync(
        shotId: shotId,
        text: "镜头配音文本",
        voice: "nova",
        speed: 1.2
    );

    Console.WriteLine($"音频已保存到：{audioPath}");
}
```

### 3. 批量生成配音

```csharp
// 批量为多个镜头生成配音
public async Task GenerateBatchAudioAsync()
{
    var shotTexts = new Dictionary<long, string>
    {
        { 1, "第一个镜头的配音文本" },
        { 2, "第二个镜头的配音文本" },
        { 3, "第三个镜头的配音文本" }
    };

    var progress = new Progress<(int Current, int Total, long ShotId)>(p =>
    {
        Console.WriteLine($"进度：{p.Current}/{p.Total}，正在处理镜头 {p.ShotId}");
    });

    var results = await _ttsService.GenerateBatchAsync(
        shotTexts: shotTexts,
        voice: "alloy",
        speed: 1.0,
        progress: progress
    );

    foreach (var (shotId, audioPath) in results)
    {
        Console.WriteLine($"镜头 {shotId} 音频：{audioPath}");
    }
}
```

### 4. 获取可用的提供商

```csharp
// 获取所有可用的 TTS 提供商
var providers = _ttsService.GetAvailableProviders();
foreach (var provider in providers)
{
    Console.WriteLine($"提供商：{provider.DisplayName}");
    Console.WriteLine($"支持的模型：{string.Join(", ", provider.SupportedModels)}");
    Console.WriteLine($"支持的音色：{string.Join(", ", provider.SupportedVoices)}");
}

// 获取默认提供商
var defaultProvider = _ttsService.GetDefaultProvider();
Console.WriteLine($"默认提供商：{defaultProvider.DisplayName}");
```

## ⚙️ 配置说明

### ai.defaults.json 配置

```json
{
  "Providers": {
    "NewApi": {
      "Endpoint": "http://127.0.0.1:3000/v1",
      "TimeoutSeconds": 120,
      "Enabled": true,
      "DefaultModels": {
        "Text": "doubao-seed-1-6-251015",
        "Image": "doubao-seedream-4-5-251128",
        "Video": "doubao-seedance-1-0-lite-i2v-250428",
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

### 配置项说明

- `DefaultProvider`: 默认使用的 TTS 提供商
- `Voice`: 默认音色（alloy, echo, fable, onyx, nova, shimmer）
- `Speed`: 默认语速（0.25 - 4.0）
- `ResponseFormat`: 默认输出格式（mp3, opus, aac, flac, wav, pcm）
- `ProviderHint`: 提供商提示（可选，用于 NewAPI 路由）

## 📁 文件结构

```
Infrastructure/
├── Media/
│   ├── TtsProviders.cs                    # TTS 接口定义
│   └── Providers/
│       └── NewApiTtsProvider.cs           # NewAPI TTS 实现
├── Services/
│   └── TtsService.cs                      # TTS 服务实现
└── AI/Core/
    ├── MediaProviderTypes.cs              # 添加 TtsProviderType
    ├── AIServiceConfig.cs                 # 添加 TTS 配置
    └── IAIServiceProvider.cs              # 添加 TextToSpeech 能力

Application/
└── Abstractions/
    └── ITtsService.cs                     # TTS 服务接口

Domain/
└── Entities/
    └── Shot.cs                            # 添加音频字段

Migrations/
└── 20260305000000_AddAudioFieldsToShot.cs # 数据库迁移

App/
└── App.axaml.cs                           # 注册 TTS 服务

ai.defaults.json                           # 添加 TTS 配置
```

## 🎯 下一步工作

### 1. UI 集成（高优先级）

需要创建以下 UI 组件：

#### 1.1 镜头配音面板
- 在镜头编辑界面添加"配音"标签页
- 输入配音文本
- 选择音色和语速
- 生成配音按钮
- 播放预览按钮

#### 1.2 批量配音对话框
- 批量选择镜头
- 统一设置音色和语速
- 显示生成进度
- 支持取消操作

#### 1.3 音频管理
- 显示已生成的音频列表
- 支持播放、删除、重新生成
- 显示音频时长和文件大小

### 2. 音频时间轴集成（中优先级）

- 在时间轴上显示音频轨道
- 音频与视频同步
- 支持音频剪辑和调整

### 3. 音频导出（中优先级）

- 在视频合成时包含音频
- 支持多音轨混音
- 音量调节和淡入淡出

### 4. 扩展功能（低优先级）

- 支持更多 TTS 提供商（阿里云、火山引擎）
- 支持自定义音色
- 支持情感控制
- 支持多语言

## 🧪 测试建议

### 单元测试

```csharp
[Fact]
public async Task GenerateAsync_ShouldReturnAudioBytes()
{
    // Arrange
    var ttsService = GetTtsService();

    // Act
    var result = await ttsService.GenerateAsync(
        text: "测试文本",
        voice: "alloy",
        speed: 1.0
    );

    // Assert
    Assert.NotNull(result.AudioBytes);
    Assert.True(result.AudioBytes.Length > 0);
    Assert.Equal(".mp3", result.FileExtension);
}
```

### 集成测试

1. 测试 NewAPI 连接
2. 测试不同音色生成
3. 测试不同语速生成
4. 测试批量生成
5. 测试错误处理

## 📝 API 参考

### NewAPI TTS Endpoint

```
POST /v1/audio/speech
Content-Type: application/json
Authorization: Bearer YOUR_API_KEY

{
  "model": "tts-1",
  "input": "要转换的文本",
  "voice": "alloy",
  "speed": 1.0,
  "response_format": "mp3"
}
```

### 响应

```
Content-Type: audio/mpeg

<binary audio data>
```

## 🔧 故障排除

### 问题：TTS 生成失败

**可能原因：**
1. NewAPI 服务未启动
2. API Key 配置错误
3. 端点 URL 配置错误
4. 网络连接问题

**解决方法：**
1. 检查 NewAPI 服务状态
2. 验证 `ai.defaults.json` 中的配置
3. 查看日志文件获取详细错误信息

### 问题：音频文件无法播放

**可能原因：**
1. 文件格式不支持
2. 文件损坏
3. 编码问题

**解决方法：**
1. 尝试使用 mp3 格式
2. 重新生成音频
3. 使用专业音频播放器测试

## 📚 相关文档

- [NewAPI 文档](https://docs.newapi.pro/zh/docs/api)
- [OpenAI TTS API](https://platform.openai.com/docs/guides/text-to-speech)
- [FFmpeg 音频处理](https://ffmpeg.org/ffmpeg-filters.html#Audio-Filters)

## 🎉 总结

TTS 语音合成功能已完整实现，包括：
- ✅ 核心接口和类型定义
- ✅ NewAPI TTS 提供商实现
- ✅ TTS 服务实现
- ✅ 数据库支持
- ✅ 配置支持
- ✅ 依赖注入

下一步需要进行 UI 集成，让用户可以通过界面使用 TTS 功能。
