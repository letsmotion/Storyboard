# TTS UI 集成说明（更新于 2026-03-05）

## 1. 集成目标

为镜头编辑流程提供可直接使用的配音能力，覆盖：

- 单镜头配音生成
- 单镜头音频上传
- 音色与语速设置
- 已生成/已上传音频的内置播放与删除
- 批量配音生成（列表多选入口）

## 2. 当前 UI 入口

### 2.1 单镜头入口

- 页面：`App/Views/ShotEditorView.axaml`
- 标签页：`配音生成`
- 核心操作：
  - 输入 `AudioText`
  - 选择 `TtsVoice`
  - 调整 `TtsSpeed`
  - 点击 `RequestGenerateAudioCommand`
  - 上传音频（`OnUploadAudioClicked`）
  - 内置播放（`OnToggleAudioPlayClicked` / `OnStopAudioClicked`）
  - 删除 `DeleteAudioCommand`

### 2.2 批量入口

- 页面：`App/Views/MainWindow.axaml`
- 按钮：`批量配音`
- 命令：`ShotList.BatchGenerateAudioCommand`
- 对话框：`App/Views/BatchAudioGenerationDialog.axaml`

## 3. 绑定模型与命令

### 3.1 `ShotItem`（`Shared/Models/ShotItem.cs`）

TTS 相关属性：

- `AudioText`
- `GeneratedAudioPath`
- `TtsVoice`
- `TtsSpeed`
- `AudioDuration`
- `IsGeneratingAudio`
- `AudioStatusMessage`
- `HasGeneratedAudio`

TTS 相关事件与命令：

- 事件：
  - `GenerateAudioRequested`
  - `PlayAudioRequested`
  - `DeleteAudioRequested`
- 命令：
  - `RequestGenerateAudioCommand`
  - `PlayAudioCommand`
  - `DeleteAudioCommand`

### 3.2 转换器

- `App/Converters/GeneratingAudioTextConverter.cs`
  - 按钮文本：`生成中...` / `生成配音`
- `App/Converters/PathToFileNameConverter.cs`
  - 从路径提取文件名用于展示

## 4. ViewModel 与消息链路

### 4.1 单镜头链路

1. `ShotItem.RequestGenerateAudioCommand`
2. `ShotListViewModel` 捕获 `GenerateAudioRequested`
3. 发送 `AudioGenerationRequestedMessage`
4. `AudioGenerationViewModel` 处理生成
5. 发送 `AudioGenerationCompletedMessage`
6. UI 刷新状态

相关消息定义：`App/Messages/GenerationMessages.cs`

- `AudioGenerationRequestedMessage`
- `AudioGenerationCompletedMessage`
- `AudioPlayRequestedMessage`
- `AudioDeleteRequestedMessage`

### 4.2 批量链路

1. `ShotList.BatchGenerateAudioCommand`
2. 发送 `BatchAudioGenerationRequestedMessage`
3. `MainViewModel` 弹出批量对话框
4. `BatchAudioGenerationViewModel.GenerateCommand` 执行批量生成
5. 更新进度和统计

批量参数：

- `SelectedVoice`
- `Speed`
- `SkipExistingAudio`
- `UseExistingText`

## 5. 交互现状

### 5.1 已实现

- 单镜头生成、播放、删除闭环可用。
- 单镜头支持上传音频，并写入 `GeneratedAudioPath`。
- 单镜头内置播放器支持播放/暂停/停止、进度条与时间显示。
- 批量生成支持进度、取消、失败明细。
- 生成状态和错误信息可见。
- 工程保存/重开后可恢复音频相关字段（文本、路径、音色、语速、时长）。

### 5.2 待完善

- 批量配音与更多入口仍缺少统一内置播放器体验。
- 批量对话框“关闭”按钮当前仅触发取消逻辑，窗口关闭体验可继续优化。

## 6. 手工测试清单

### 6.1 单镜头

1. 空文本点击生成，提示“请输入配音文本”。
2. 输入文本后生成成功，`GeneratedAudioPath` 变化。
3. 上传音频后，文件名与时长显示更新。
4. 点击播放/暂停/停止，进度条与时间按预期更新。
5. 生成中按钮禁用，结束后恢复。
6. 删除后 `HasGeneratedAudio` 变为 `false`。

### 6.2 批量

1. 勾选多个镜头后打开批量配音对话框。
2. 启用“跳过已有配音”时，已有音频镜头被计入跳过。
3. 过程中可取消，状态显示“生成已取消”。
4. 结束后显示成功/失败/跳过统计。

## 7. 与实现文档的关系

- 详细服务层实现见：`docs/TTS_IMPLEMENTATION.md`
- 项目整体里程碑见：`docs/TTS_SUMMARY.md`
