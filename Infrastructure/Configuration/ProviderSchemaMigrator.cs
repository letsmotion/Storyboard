using Microsoft.Extensions.Logging;

namespace Storyboard.Infrastructure.Configuration;

public class ProviderSchemaMigrator
{
    private readonly ILogger<ProviderSchemaMigrator> _logger;
    private readonly ProviderStateStore _stateStore;

    public const int CurrentSchemaVersion = 1;

    public ProviderSchemaMigrator(ILogger<ProviderSchemaMigrator> logger, ProviderStateStore stateStore)
    {
        _logger = logger;
        _stateStore = stateStore;
    }

    public void MigrateIfNeeded()
    {
        var state = _stateStore.Load();

        if (state.SchemaVersion >= CurrentSchemaVersion)
        {
            _logger.LogDebug("Schema version already up to date: {Version}", state.SchemaVersion);
            return;
        }

        _logger.LogInformation("Schema version upgrade: {OldVersion} -> {NewVersion}",
            state.SchemaVersion, CurrentSchemaVersion);

        state.SchemaVersion = CurrentSchemaVersion;
        _stateStore.Save(state);

        _logger.LogInformation("Schema migration complete");
    }
}
