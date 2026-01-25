using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Storyboard.AI.Prompts;

/// <summary>
/// 提示词管理服务
/// </summary>
public class PromptManagementService
{
    private readonly ILogger<PromptManagementService> _logger;
    private readonly string _promptsDirectory;
    private readonly Dictionary<string, PromptTemplate> _templates = new();

    public PromptManagementService(ILogger<PromptManagementService> logger)
    {
        _logger = logger;
        _promptsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts");
        
        if (!Directory.Exists(_promptsDirectory))
        {
            Directory.CreateDirectory(_promptsDirectory);
            _logger.LogInformation("创建提示词目录: {Directory}", _promptsDirectory);
        }
    }

    /// <summary>
    /// 加载所有提示词模板
    /// </summary>
    public async Task LoadAllTemplatesAsync()
    {
        _templates.Clear();

        var files = Directory.GetFiles(_promptsDirectory, "*.json");
        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var template = JsonSerializer.Deserialize<PromptTemplate>(json);
                if (template != null)
                {
                    _templates[template.Id] = template;
                    _logger.LogInformation("加载提示词模板: {Name} ({Id})", template.Name, template.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载提示词模板失败: {File}", file);
            }
        }

    }

    /// <summary>
    /// 获取提示词模板
    /// </summary>
    public PromptTemplate? GetTemplate(string id)
    {
        return _templates.TryGetValue(id, out var template) ? template : null;
    }

    /// <summary>
    /// 获取所有模板
    /// </summary>
    public IReadOnlyList<PromptTemplate> GetAllTemplates()
    {
        return _templates.Values.ToList();
    }

    /// <summary>
    /// 保存或更新模板
    /// </summary>
    public async Task SaveTemplateAsync(PromptTemplate template)
    {
        template.UpdatedAt = DateTime.Now;
        _templates[template.Id] = template;

        var filePath = Path.Combine(_promptsDirectory, $"{template.Id}.json");
        var json = JsonSerializer.Serialize(template, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        
        await File.WriteAllTextAsync(filePath, json);
        _logger.LogInformation("保存提示词模板: {Name} ({Id})", template.Name, template.Id);
    }

    /// <summary>
    /// 删除模板
    /// </summary>
    public async Task DeleteTemplateAsync(string id)
    {
        if (_templates.Remove(id))
        {
            var filePath = Path.Combine(_promptsDirectory, $"{id}.json");
            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath));
                _logger.LogInformation("删除提示词模板: {Id}", id);
            }
        }
    }

    /// <summary>
    /// 渲染提示词
    /// </summary>
    public string RenderPrompt(PromptTemplate template, Dictionary<string, object> parameters)
    {
        var prompt = template.UserPromptTemplate;

        foreach (var param in template.Parameters)
        {
            var value = parameters.TryGetValue(param.Key, out var v)
                ? v?.ToString()
                : param.Value.DefaultValue;

            if (param.Value.Required && string.IsNullOrEmpty(value))
            {
                throw new ArgumentException($"必需参数缺失: {param.Key}");
            }

            prompt = prompt.Replace($"{{{{{param.Key}}}}}", value ?? string.Empty);
        }

        return prompt;
    }

    /// <summary>
    /// 渲染提示词（带创作意图注入）
    /// </summary>
    public string RenderPromptWithIntent(
        PromptTemplate template,
        Dictionary<string, object> parameters,
        string? creativeGoal = null,
        string? targetAudience = null,
        string? videoTone = null,
        string? keyMessage = null)
    {
        var basePrompt = RenderPrompt(template, parameters);

        // 如果没有任何创作意图，直接返回基础 Prompt
        if (string.IsNullOrWhiteSpace(creativeGoal) &&
            string.IsNullOrWhiteSpace(targetAudience) &&
            string.IsNullOrWhiteSpace(videoTone) &&
            string.IsNullOrWhiteSpace(keyMessage))
        {
            return basePrompt;
        }

        // 构建创作意图上下文
        var intentContext = "\n\n【创作意图】\n";

        if (!string.IsNullOrWhiteSpace(creativeGoal))
            intentContext += $"创作目标：{creativeGoal}\n";

        if (!string.IsNullOrWhiteSpace(targetAudience))
            intentContext += $"目标受众：{targetAudience}\n";

        if (!string.IsNullOrWhiteSpace(videoTone))
            intentContext += $"视频基调：{videoTone}\n";

        if (!string.IsNullOrWhiteSpace(keyMessage))
            intentContext += $"核心信息：{keyMessage}\n";

        intentContext += "\n请在生成内容时充分考虑以上创作意图，确保输出与创作目标、受众特征、视频基调和核心信息保持一致。";

        _logger.LogInformation("注入创作意图到 Prompt: {TemplateId}", template.Id);

        return basePrompt + intentContext;
    }
}
