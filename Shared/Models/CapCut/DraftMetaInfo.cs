using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Storyboard.Models.CapCut;

/// <summary>
/// CapCut 草稿元信息模型 (draft_meta_info.json)
/// </summary>
public class DraftMetaInfo
{
    [JsonPropertyName("cloud_package_completed_time")]
    public string CloudPackageCompletedTime { get; set; } = string.Empty;

    [JsonPropertyName("draft_cloud_capcut_purchase_info")]
    public string DraftCloudCapcutPurchaseInfo { get; set; } = string.Empty;

    [JsonPropertyName("draft_cloud_last_action_download")]
    public bool DraftCloudLastActionDownload { get; set; }

    [JsonPropertyName("draft_cloud_package_type")]
    public string DraftCloudPackageType { get; set; } = string.Empty;

    [JsonPropertyName("draft_cloud_materials")]
    public List<object> DraftCloudMaterials { get; set; } = new();

    [JsonPropertyName("draft_cloud_purchase_info")]
    public string DraftCloudPurchaseInfo { get; set; } = string.Empty;

    [JsonPropertyName("draft_cloud_template_id")]
    public string DraftCloudTemplateId { get; set; } = string.Empty;

    [JsonPropertyName("draft_cloud_tutorial_info")]
    public string DraftCloudTutorialInfo { get; set; } = string.Empty;

    [JsonPropertyName("draft_cloud_videocut_purchase_info")]
    public string DraftCloudVideocutPurchaseInfo { get; set; } = string.Empty;

    [JsonPropertyName("draft_cover")]
    public string DraftCover { get; set; } = string.Empty;

    [JsonPropertyName("draft_deeplink_url")]
    public string DraftDeeplinkUrl { get; set; } = string.Empty;

    [JsonPropertyName("draft_enterprise_info")]
    public DraftEnterpriseInfo DraftEnterpriseInfo { get; set; } = new();

    [JsonPropertyName("draft_fold_path")]
    public string DraftFoldPath { get; set; } = string.Empty;

    [JsonPropertyName("draft_id")]
    public string DraftId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("draft_is_ae_produce")]
    public bool DraftIsAeProduce { get; set; }

    [JsonPropertyName("draft_is_ai_packaging_used")]
    public bool DraftIsAiPackagingUsed { get; set; }

    [JsonPropertyName("draft_is_ai_shorts")]
    public bool DraftIsAiShorts { get; set; }

    [JsonPropertyName("draft_is_ai_translate")]
    public bool DraftIsAiTranslate { get; set; }

    [JsonPropertyName("draft_is_article_video_draft")]
    public bool DraftIsArticleVideoDraft { get; set; }

    [JsonPropertyName("draft_is_from_deeplink")]
    public string DraftIsFromDeeplink { get; set; } = "false";

    [JsonPropertyName("draft_is_invisible")]
    public bool DraftIsInvisible { get; set; }

    [JsonPropertyName("draft_materials")]
    public List<DraftMaterial> DraftMaterials { get; set; } = new()
    {
        new DraftMaterial { Type = 0, Value = new List<string>() },
        new DraftMaterial { Type = 1, Value = new List<string>() },
        new DraftMaterial { Type = 2, Value = new List<string>() },
        new DraftMaterial { Type = 3, Value = new List<string>() },
        new DraftMaterial { Type = 6, Value = new List<string>() },
        new DraftMaterial { Type = 7, Value = new List<string>() },
        new DraftMaterial { Type = 8, Value = new List<string>() }
    };

    [JsonPropertyName("draft_materials_copied_info")]
    public List<object> DraftMaterialsCopiedInfo { get; set; } = new();

    [JsonPropertyName("draft_name")]
    public string DraftName { get; set; } = string.Empty;

    [JsonPropertyName("draft_need_rename_folder")]
    public bool DraftNeedRenameFolder { get; set; }

    [JsonPropertyName("draft_new_version")]
    public string DraftNewVersion { get; set; } = string.Empty;

    [JsonPropertyName("draft_removable_storage_device")]
    public string DraftRemovableStorageDevice { get; set; } = string.Empty;

    [JsonPropertyName("draft_root_path")]
    public string DraftRootPath { get; set; } = string.Empty;

    [JsonPropertyName("draft_segment_extra_info")]
    public List<object> DraftSegmentExtraInfo { get; set; } = new();

    [JsonPropertyName("draft_timeline_materials_size_")]
    public long DraftTimelineMaterialsSize { get; set; }

    [JsonPropertyName("draft_type")]
    public string DraftType { get; set; } = string.Empty;

    [JsonPropertyName("tm_draft_cloud_completed")]
    public string TmDraftCloudCompleted { get; set; } = string.Empty;

    [JsonPropertyName("tm_draft_cloud_modified")]
    public long TmDraftCloudModified { get; set; }

    [JsonPropertyName("tm_draft_cloud_space_id")]
    public long TmDraftCloudSpaceId { get; set; }

    [JsonPropertyName("tm_draft_create")]
    public long TmDraftCreate { get; set; }

    [JsonPropertyName("tm_draft_modified")]
    public long TmDraftModified { get; set; }

    [JsonPropertyName("tm_draft_removed")]
    public long TmDraftRemoved { get; set; }

    [JsonPropertyName("tm_duration")]
    public long TmDuration { get; set; }
}

/// <summary>
/// 企业信息
/// </summary>
public class DraftEnterpriseInfo
{
    [JsonPropertyName("draft_enterprise_extra")]
    public string DraftEnterpriseExtra { get; set; } = string.Empty;

    [JsonPropertyName("draft_enterprise_id")]
    public string DraftEnterpriseId { get; set; } = string.Empty;

    [JsonPropertyName("draft_enterprise_name")]
    public string DraftEnterpriseName { get; set; } = string.Empty;

    [JsonPropertyName("enterprise_material")]
    public List<object> EnterpriseMaterial { get; set; } = new();
}

/// <summary>
/// 草稿素材
/// </summary>
public class DraftMaterial
{
    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("value")]
    public List<string> Value { get; set; } = new();
}
