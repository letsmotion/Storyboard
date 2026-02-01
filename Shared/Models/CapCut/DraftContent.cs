using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Storyboard.Models.CapCut;

/// <summary>
/// CapCut 鑽夌鍐呭妯″瀷 (draft_content.json)
/// </summary>
public class DraftContent
{
    [JsonPropertyName("canvas_config")]
    public CanvasConfig CanvasConfig { get; set; } = new();

    [JsonPropertyName("color_space")]
    public int ColorSpace { get; set; } = -1;

    [JsonPropertyName("config")]
    public DraftConfig Config { get; set; } = new();

    [JsonPropertyName("cover")]
    public string? Cover { get; set; }

    [JsonPropertyName("create_time")]
    public long CreateTime { get; set; }

    [JsonPropertyName("duration")]
    public long Duration { get; set; }

    [JsonPropertyName("extra_info")]
    public object? ExtraInfo { get; set; }

    [JsonPropertyName("fps")]
    public double Fps { get; set; } = 30.0;

    [JsonPropertyName("free_render_index_mode_on")]
    public bool FreeRenderIndexModeOn { get; set; }

    [JsonPropertyName("group_container")]
    public object? GroupContainer { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("is_drop_frame_timecode")]
    public bool IsDropFrameTimecode { get; set; }

    [JsonPropertyName("keyframe_graph_list")]
    public List<object> KeyframeGraphList { get; set; } = new();

    [JsonPropertyName("keyframes")]
    public Keyframes Keyframes { get; set; } = new();

    [JsonPropertyName("lyrics_effects")]
    public List<object> LyricsEffects { get; set; } = new();

    [JsonPropertyName("last_modified_platform")]
    public Platform LastModifiedPlatform { get; set; } = new();

    [JsonPropertyName("platform")]
    public Platform Platform { get; set; } = new();

    [JsonPropertyName("materials")]
    public Materials Materials { get; set; } = new();

    [JsonPropertyName("mutable_config")]
    public object? MutableConfig { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("new_version")]
    public string NewVersion { get; set; } = "110.0.0";

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("relationships")]
    public List<object> Relationships { get; set; } = new();

    [JsonPropertyName("render_index_track_mode_on")]
    public bool RenderIndexTrackModeOn { get; set; } = true;

    [JsonPropertyName("retouch_cover")]
    public object? RetouchCover { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = "default";

    [JsonPropertyName("static_cover_image_path")]
    public string StaticCoverImagePath { get; set; } = string.Empty;

    [JsonPropertyName("time_marks")]
    public object? TimeMarks { get; set; }

    [JsonPropertyName("tracks")]
    public List<Track> Tracks { get; set; } = new();

    [JsonPropertyName("update_time")]
    public long UpdateTime { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; } = 360000;
}

/// <summary>
/// 鐢诲竷閰嶇疆
/// </summary>
public class CanvasConfig
{
    [JsonPropertyName("height")]
    public int Height { get; set; } = 1080;

    [JsonPropertyName("ratio")]
    public string Ratio { get; set; } = "original";

    [JsonPropertyName("width")]
    public int Width { get; set; } = 1920;
}

/// <summary>
/// 鑽夌閰嶇疆
/// </summary>
public class DraftConfig
{
    [JsonPropertyName("adjust_max_index")]
    public int AdjustMaxIndex { get; set; } = 1;

    [JsonPropertyName("attachment_info")]
    public List<object> AttachmentInfo { get; set; } = new();

    [JsonPropertyName("combination_max_index")]
    public int CombinationMaxIndex { get; set; } = 1;

    [JsonPropertyName("export_range")]
    public object? ExportRange { get; set; }

    [JsonPropertyName("extract_audio_last_index")]
    public int ExtractAudioLastIndex { get; set; } = 1;

    [JsonPropertyName("lyrics_recognition_id")]
    public string LyricsRecognitionId { get; set; } = string.Empty;

    [JsonPropertyName("lyrics_sync")]
    public bool LyricsSync { get; set; } = true;

    [JsonPropertyName("lyrics_taskinfo")]
    public List<object> LyricsTaskinfo { get; set; } = new();

    [JsonPropertyName("maintrack_adsorb")]
    public bool MaintrackAdsorb { get; set; } = true;

    [JsonPropertyName("material_save_mode")]
    public int MaterialSaveMode { get; set; } = 0;

    [JsonPropertyName("multi_language_current")]
    public string MultiLanguageCurrent { get; set; } = "none";

    [JsonPropertyName("multi_language_list")]
    public List<object> MultiLanguageList { get; set; } = new();

    [JsonPropertyName("multi_language_main")]
    public string MultiLanguageMain { get; set; } = "none";

    [JsonPropertyName("multi_language_mode")]
    public string MultiLanguageMode { get; set; } = "none";

    [JsonPropertyName("original_sound_last_index")]
    public int OriginalSoundLastIndex { get; set; } = 1;

    [JsonPropertyName("record_audio_last_index")]
    public int RecordAudioLastIndex { get; set; } = 1;

    [JsonPropertyName("sticker_max_index")]
    public int StickerMaxIndex { get; set; } = 1;

    [JsonPropertyName("subtitle_keywords_config")]
    public object? SubtitleKeywordsConfig { get; set; }

    [JsonPropertyName("subtitle_recognition_id")]
    public string SubtitleRecognitionId { get; set; } = string.Empty;

    [JsonPropertyName("subtitle_sync")]
    public bool SubtitleSync { get; set; } = true;

    [JsonPropertyName("subtitle_taskinfo")]
    public List<object> SubtitleTaskinfo { get; set; } = new();

    [JsonPropertyName("system_font_list")]
    public List<object> SystemFontList { get; set; } = new();

    [JsonPropertyName("video_mute")]
    public bool VideoMute { get; set; }

    [JsonPropertyName("zoom_info_params")]
    public object? ZoomInfoParams { get; set; }
}

/// <summary>
/// 鍏抽敭甯ч泦鍚?
/// </summary>
public class Keyframes
{
    [JsonPropertyName("adjusts")]
    public List<object> Adjusts { get; set; } = new();

    [JsonPropertyName("audios")]
    public List<object> Audios { get; set; } = new();

    [JsonPropertyName("effects")]
    public List<object> Effects { get; set; } = new();

    [JsonPropertyName("filters")]
    public List<object> Filters { get; set; } = new();

    [JsonPropertyName("handwrites")]
    public List<object> Handwrites { get; set; } = new();

    [JsonPropertyName("stickers")]
    public List<object> Stickers { get; set; } = new();

    [JsonPropertyName("texts")]
    public List<object> Texts { get; set; } = new();

    [JsonPropertyName("videos")]
    public List<object> Videos { get; set; } = new();
}

/// <summary>
/// 骞冲彴淇℃伅
/// </summary>
public class Platform
{
    [JsonPropertyName("app_id")]
    public int AppId { get; set; } = 3704;

    [JsonPropertyName("app_source")]
    public string AppSource { get; set; } = "lv";

    [JsonPropertyName("app_version")]
    public string AppVersion { get; set; } = "5.9.0";

    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("hard_disk_id")]
    public string HardDiskId { get; set; } = string.Empty;

    [JsonPropertyName("mac_address")]
    public string MacAddress { get; set; } = string.Empty;

    [JsonPropertyName("os")]
    public string Os { get; set; } = "windows";

    [JsonPropertyName("os_version")]
    public string OsVersion { get; set; } = string.Empty;
}

/// <summary>
/// 绱犳潗闆嗗悎
/// </summary>
public class Materials
{
    [JsonPropertyName("ai_translates")]
    public List<object> AiTranslates { get; set; } = new();

    [JsonPropertyName("audio_balances")]
    public List<object> AudioBalances { get; set; } = new();

    [JsonPropertyName("audio_effects")]
    public List<object> AudioEffects { get; set; } = new();

    [JsonPropertyName("audio_fades")]
    public List<object> AudioFades { get; set; } = new();

    [JsonPropertyName("audio_track_indexes")]
    public List<object> AudioTrackIndexes { get; set; } = new();

    [JsonPropertyName("audios")]
    public List<object> Audios { get; set; } = new();

    [JsonPropertyName("beats")]
    public List<object> Beats { get; set; } = new();

    [JsonPropertyName("canvases")]
    public List<object> Canvases { get; set; } = new();

    [JsonPropertyName("chromas")]
    public List<object> Chromas { get; set; } = new();

    [JsonPropertyName("color_curves")]
    public List<object> ColorCurves { get; set; } = new();

    [JsonPropertyName("digital_humans")]
    public List<object> DigitalHumans { get; set; } = new();

    [JsonPropertyName("drafts")]
    public List<object> Drafts { get; set; } = new();

    [JsonPropertyName("effects")]
    public List<object> Effects { get; set; } = new();

    [JsonPropertyName("flowers")]
    public List<object> Flowers { get; set; } = new();

    [JsonPropertyName("green_screens")]
    public List<object> GreenScreens { get; set; } = new();

    [JsonPropertyName("handwrites")]
    public List<object> Handwrites { get; set; } = new();

    [JsonPropertyName("hsl")]
    public List<object> Hsl { get; set; } = new();

    [JsonPropertyName("images")]
    public List<object> Images { get; set; } = new();

    [JsonPropertyName("log_color_wheels")]
    public List<object> LogColorWheels { get; set; } = new();

    [JsonPropertyName("loudnesses")]
    public List<object> Loudnesses { get; set; } = new();

    [JsonPropertyName("manual_deformations")]
    public List<object> ManualDeformations { get; set; } = new();

    [JsonPropertyName("masks")]
    public List<object> Masks { get; set; } = new();

    [JsonPropertyName("material_animations")]
    public List<object> MaterialAnimations { get; set; } = new();

    [JsonPropertyName("material_colors")]
    public List<object> MaterialColors { get; set; } = new();

    [JsonPropertyName("multi_language_refs")]
    public List<object> MultiLanguageRefs { get; set; } = new();

    [JsonPropertyName("placeholder_infos")]
    public List<object> PlaceholderInfos { get; set; } = new();

    [JsonPropertyName("placeholders")]
    public List<object> Placeholders { get; set; } = new();

    [JsonPropertyName("plugin_effects")]
    public List<object> PluginEffects { get; set; } = new();

    [JsonPropertyName("primary_color_wheels")]
    public List<object> PrimaryColorWheels { get; set; } = new();

    [JsonPropertyName("realtime_denoises")]
    public List<object> RealtimeDenoises { get; set; } = new();

    [JsonPropertyName("shapes")]
    public List<object> Shapes { get; set; } = new();

    [JsonPropertyName("smart_crops")]
    public List<object> SmartCrops { get; set; } = new();

    [JsonPropertyName("smart_relights")]
    public List<object> SmartRelights { get; set; } = new();

    [JsonPropertyName("sound_channel_mappings")]
    public List<object> SoundChannelMappings { get; set; } = new();

    [JsonPropertyName("speeds")]
    public List<SpeedMaterial> Speeds { get; set; } = new();

    [JsonPropertyName("stickers")]
    public List<object> Stickers { get; set; } = new();

    [JsonPropertyName("tail_leaders")]
    public List<object> TailLeaders { get; set; } = new();

    [JsonPropertyName("text_templates")]
    public List<object> TextTemplates { get; set; } = new();

    [JsonPropertyName("texts")]
    public List<object> Texts { get; set; } = new();

    [JsonPropertyName("time_marks")]
    public List<object> TimeMarks { get; set; } = new();

    [JsonPropertyName("transitions")]
    public List<object> Transitions { get; set; } = new();

    [JsonPropertyName("video_effects")]
    public List<object> VideoEffects { get; set; } = new();

    [JsonPropertyName("video_trackings")]
    public List<object> VideoTrackings { get; set; } = new();

    [JsonPropertyName("videos")]
    public List<VideoMaterial> Videos { get; set; } = new();

    [JsonPropertyName("vocal_beautifys")]
    public List<object> VocalBeautifys { get; set; } = new();

    [JsonPropertyName("vocal_separations")]
    public List<object> VocalSeparations { get; set; } = new();
}

/// <summary>
/// 速度素材
/// </summary>
public class SpeedMaterial
{
    [JsonPropertyName("curve_speed")]
    public object? CurveSpeed { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N").ToUpper();

    [JsonPropertyName("mode")]
    public int Mode { get; set; } = 0;

    [JsonPropertyName("speed")]
    public double Speed { get; set; } = 1.0;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "speed";
}

/// <summary>
/// 裁剪信息
/// </summary>
public class Crop
{
    [JsonPropertyName("upper_left_x")]
    public double UpperLeftX { get; set; } = 0.0;

    [JsonPropertyName("upper_left_y")]
    public double UpperLeftY { get; set; } = 0.0;

    [JsonPropertyName("upper_right_x")]
    public double UpperRightX { get; set; } = 1.0;

    [JsonPropertyName("upper_right_y")]
    public double UpperRightY { get; set; } = 0.0;

    [JsonPropertyName("lower_left_x")]
    public double LowerLeftX { get; set; } = 0.0;

    [JsonPropertyName("lower_left_y")]
    public double LowerLeftY { get; set; } = 1.0;

    [JsonPropertyName("lower_right_x")]
    public double LowerRightX { get; set; } = 1.0;

    [JsonPropertyName("lower_right_y")]
    public double LowerRightY { get; set; } = 1.0;
}

/// <summary>
/// 视频素材
/// </summary>
public class VideoMaterial
{
    [JsonPropertyName("audio_fade")]
    public object? AudioFade { get; set; }

    [JsonPropertyName("category_id")]
    public string CategoryId { get; set; } = string.Empty;

    [JsonPropertyName("category_name")]
    public string CategoryName { get; set; } = "local";

    [JsonPropertyName("check_flag")]
    public int CheckFlag { get; set; } = 63487;

    [JsonPropertyName("crop")]
    public Crop Crop { get; set; } = new();

    [JsonPropertyName("crop_ratio")]
    public string CropRatio { get; set; } = "free";

    [JsonPropertyName("crop_scale")]
    public double CropScale { get; set; } = 1.0;

    [JsonPropertyName("duration")]
    public long Duration { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N").ToUpper();

    [JsonPropertyName("local_material_id")]
    public string LocalMaterialId { get; set; } = string.Empty;

    [JsonPropertyName("material_id")]
    public string MaterialId { get; set; } = string.Empty;

    [JsonPropertyName("material_name")]
    public string MaterialName { get; set; } = string.Empty;

    [JsonPropertyName("media_path")]
    public string MediaPath { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "video";

    [JsonPropertyName("width")]
    public int Width { get; set; }
}
/// <summary>
/// 杞ㄩ亾
/// </summary>
public class Track
{
    [JsonPropertyName("attribute")]
    public int Attribute { get; set; } = 0;

    [JsonPropertyName("flag")]
    public int Flag { get; set; } = 0;

    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("is_default_name")]
    public bool IsDefaultName { get; set; } = true;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("segments")]
    public List<Segment> Segments { get; set; } = new();

    [JsonPropertyName("type")]
    public string Type { get; set; } = "video";
}

/// <summary>
/// 鐗囨
/// </summary>
public class Segment
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("cartoon")]
    public bool Cartoon { get; set; } = false;

    [JsonPropertyName("clip")]
    public Clip? Clip { get; set; }

    [JsonPropertyName("common_keyframes")]
    public List<object> CommonKeyframes { get; set; } = new();

    [JsonPropertyName("enable_adjust")]
    public bool EnableAdjust { get; set; } = true;

    [JsonPropertyName("enable_color_correct_adjust")]
    public bool EnableColorCorrectAdjust { get; set; } = false;

    [JsonPropertyName("enable_color_curves")]
    public bool EnableColorCurves { get; set; } = true;

    [JsonPropertyName("enable_color_match_adjust")]
    public bool EnableColorMatchAdjust { get; set; } = false;

    [JsonPropertyName("enable_color_wheels")]
    public bool EnableColorWheels { get; set; } = true;

    [JsonPropertyName("enable_lut")]
    public bool EnableLut { get; set; } = true;

    [JsonPropertyName("enable_smart_color_adjust")]
    public bool EnableSmartColorAdjust { get; set; } = false;

    [JsonPropertyName("extra_material_refs")]
    public List<string> ExtraMaterialRefs { get; set; } = new();

    [JsonPropertyName("group_id")]
    public string GroupId { get; set; } = string.Empty;

    [JsonPropertyName("hdr_settings")]
    public HdrSettings? HdrSettings { get; set; }

    [JsonPropertyName("intensifies_audio")]
    public bool IntensifiesAudio { get; set; } = false;

    [JsonPropertyName("is_placeholder")]
    public bool IsPlaceholder { get; set; } = false;

    [JsonPropertyName("is_tone_modify")]
    public bool IsToneModify { get; set; } = false;

    [JsonPropertyName("keyframe_refs")]
    public List<object> KeyframeRefs { get; set; } = new();

    [JsonPropertyName("last_nonzero_volume")]
    public double LastNonzeroVolume { get; set; } = 1.0;

    [JsonPropertyName("material_id")]
    public string MaterialId { get; set; } = string.Empty;

    [JsonPropertyName("render_index")]
    public int RenderIndex { get; set; } = 0;

    [JsonPropertyName("reverse")]
    public bool Reverse { get; set; } = false;

    [JsonPropertyName("source_timerange")]
    public TimeRange? SourceTimerange { get; set; }

    [JsonPropertyName("speed")]
    public double Speed { get; set; } = 1.0;

    [JsonPropertyName("target_timerange")]
    public TimeRange TargetTimerange { get; set; } = new();

    [JsonPropertyName("template_id")]
    public string TemplateId { get; set; } = string.Empty;

    [JsonPropertyName("template_scene")]
    public string TemplateScene { get; set; } = "default";

    [JsonPropertyName("track_attribute")]
    public int TrackAttribute { get; set; } = 0;

    [JsonPropertyName("track_render_index")]
    public int TrackRenderIndex { get; set; } = 0;

    [JsonPropertyName("uniform_scale")]
    public UniformScale? UniformScale { get; set; }

    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    [JsonPropertyName("volume")]
    public double Volume { get; set; } = 1.0;
}

/// <summary>
/// 鏃堕棿鑼冨洿
/// </summary>
public class TimeRange
{
    [JsonPropertyName("duration")]
    public long Duration { get; set; }

    [JsonPropertyName("start")]
    public long Start { get; set; }
}

/// <summary>
/// 鐗囨灞炴€?
/// </summary>
public class Clip
{
    [JsonPropertyName("alpha")]
    public double Alpha { get; set; } = 1.0;

    [JsonPropertyName("flip")]
    public Flip Flip { get; set; } = new();

    [JsonPropertyName("rotation")]
    public double Rotation { get; set; } = 0.0;

    [JsonPropertyName("scale")]
    public Scale Scale { get; set; } = new();

    [JsonPropertyName("transform")]
    public Transform Transform { get; set; } = new();
}

/// <summary>
/// 缈昏浆
/// </summary>
public class Flip
{
    [JsonPropertyName("horizontal")]
    public bool Horizontal { get; set; }

    [JsonPropertyName("vertical")]
    public bool Vertical { get; set; }
}

/// <summary>
/// 缂╂斁
/// </summary>
public class Scale
{
    [JsonPropertyName("x")]
    public double X { get; set; } = 1.0;

    [JsonPropertyName("y")]
    public double Y { get; set; } = 1.0;
}

/// <summary>
/// 鍙樻崲
/// </summary>
public class Transform
{
    [JsonPropertyName("x")]
    public double X { get; set; } = 0.0;

    [JsonPropertyName("y")]
    public double Y { get; set; } = 0.0;
}

/// <summary>
/// HDR 璁剧疆
/// </summary>
public class HdrSettings
{
    [JsonPropertyName("intensity")]
    public double Intensity { get; set; } = 1.0;

    [JsonPropertyName("mode")]
    public int Mode { get; set; } = 1;

    [JsonPropertyName("nits")]
    public int Nits { get; set; } = 1000;
}

/// <summary>
/// 缁熶竴缂╂斁
/// </summary>
public class UniformScale
{
    [JsonPropertyName("on")]
    public bool On { get; set; } = true;

    [JsonPropertyName("value")]
    public double Value { get; set; } = 1.0;
}
