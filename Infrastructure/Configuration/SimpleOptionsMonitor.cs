using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Storyboard.AI.Core;

namespace Storyboard.Infrastructure.Configuration;

/// <summary>
/// 简单的 IOptionsMonitor 实现,用于包装 AIConfigurationComposer
/// </summary>
public class SimpleOptionsMonitor<T> : IOptionsMonitor<T> where T : class
{
    private readonly AIConfigurationComposer _composer;

    public SimpleOptionsMonitor(AIConfigurationComposer composer)
    {
        _composer = composer;
    }

    public T CurrentValue => (_composer.LoadConfiguration() as T)!;

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
