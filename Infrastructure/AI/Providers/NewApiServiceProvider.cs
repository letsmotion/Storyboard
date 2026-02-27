using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;

namespace Storyboard.AI.Providers;

public sealed class NewApiServiceProvider : BaseAIServiceProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public NewApiServiceProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor, ILogger<NewApiServiceProvider> logger)
        : base(logger)
    {
        _configMonitor = configMonitor;
    }

    private AIProviderConfiguration Config => _configMonitor.CurrentValue.Providers.NewApi;

    public override AIProviderType ProviderType => AIProviderType.NewApi;
    public override string DisplayName => "New API";

    public override bool IsConfigured =>
        Config.Enabled &&
        !string.IsNullOrWhiteSpace(Config.ApiKey) &&
        !string.IsNullOrWhiteSpace(Config.Endpoint);

    public override IReadOnlyList<string> SupportedModels => Array.Empty<string>();

    public override AIProviderCapability Capabilities =>
        AIProviderCapability.TextUnderstanding |
        AIProviderCapability.ImageGeneration |
        AIProviderCapability.VideoGeneration;

    public override IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations => new[]
    {
        new ProviderCapabilityDeclaration(AIProviderCapability.TextUnderstanding, "OpenAI-compatible /v1/chat/completions", "text/plain")
    };

    public override async Task<string> ChatAsync(AIChatRequest request, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        EnsureModel(request.Model);

        var payload = BuildRequestPayload(request, stream: false);
        using var httpClient = CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Config.ApiKey);

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("chat/completions", content, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            Logger.LogError("NewApi request failed: {Body}", responseBody);
            throw new InvalidOperationException($"NewApi request failed: {responseBody}");
        }

        var result = JsonSerializer.Deserialize<OpenAiResponse>(responseBody, JsonOptions);
        return result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    public override async IAsyncEnumerable<string> ChatStreamAsync(
        AIChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        EnsureModel(request.Model);

        var payload = BuildRequestPayload(request, stream: true);
        using var httpClient = CreateHttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Config.ApiKey);

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line[5..].Trim();
            if (data == "[DONE]")
            {
                break;
            }

            var chunk = JsonSerializer.Deserialize<OpenAiResponse>(data, JsonOptions);
            var delta = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(delta))
            {
                yield return delta;
            }
        }
    }

    public override async Task<bool> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            Logger.LogWarning("NewApi configuration incomplete.");
            return false;
        }

        try
        {
            var model = string.IsNullOrWhiteSpace(Config.DefaultModels.Text)
                ? "gpt-4o-mini"
                : Config.DefaultModels.Text;
            var request = new AIChatRequest
            {
                Model = model,
                Messages = new[] { new AIChatMessage(AIChatRole.User, "ping") },
                MaxTokens = 16
            };

            _ = await ChatAsync(request, cancellationToken).ConfigureAwait(false);
            Logger.LogInformation("NewApi configuration validated.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "NewApi configuration validation failed.");
            return false;
        }
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("NewApi is not configured.");
        }
    }

    private static void EnsureModel(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("Model is required for NewApi requests.");
        }
    }

    private HttpClient CreateHttpClient()
    {
        return CreateHttpClient(Config.Endpoint, Config.TimeoutSeconds);
    }

    private object BuildRequestPayload(AIChatRequest request, bool stream)
    {
        return new
        {
            model = request.Model,
            messages = request.Messages.Select(m => new
            {
                role = MapRole(m.Role),
                content = BuildMessageContent(m)
            }).ToArray(),
            temperature = request.Temperature,
            top_p = request.TopP,
            max_tokens = request.MaxTokens,
            stream
        };
    }

    private static object BuildMessageContent(AIChatMessage message)
    {
        if (message.IsMultimodal && message.MultimodalContent != null)
        {
            var contentArray = new List<object>();
            foreach (var part in message.MultimodalContent)
            {
                if (part.Type == MessageContentType.Text && !string.IsNullOrWhiteSpace(part.Text))
                {
                    contentArray.Add(new { type = "text", text = part.Text });
                }
                else if (part.Type == MessageContentType.ImageBase64 && !string.IsNullOrWhiteSpace(part.ImageBase64))
                {
                    contentArray.Add(new
                    {
                        type = "image_url",
                        image_url = new { url = $"data:image/png;base64,{part.ImageBase64}" }
                    });
                }
                else if (part.Type == MessageContentType.ImageUrl && !string.IsNullOrWhiteSpace(part.ImageUrl))
                {
                    contentArray.Add(new
                    {
                        type = "image_url",
                        image_url = new { url = part.ImageUrl }
                    });
                }
            }

            return contentArray;
        }

        return message.Content ?? string.Empty;
    }

    private static string MapRole(AIChatRole role)
    {
        return role switch
        {
            AIChatRole.System => "system",
            AIChatRole.User => "user",
            AIChatRole.Assistant => "assistant",
            _ => "user"
        };
    }

    private sealed class OpenAiResponse
    {
        public List<OpenAiChoice>? Choices { get; set; }
    }

    private sealed class OpenAiChoice
    {
        public OpenAiMessage? Message { get; set; }
        public OpenAiMessage? Delta { get; set; }
    }

    private sealed class OpenAiMessage
    {
        public string? Content { get; set; }
    }
}
