using Xunit;

namespace AuroraUpdater.Tests;

[CollectionDefinition("App.Settings", DisableParallelization = true)]
public sealed class AppSettingsCollection : ICollectionFixture<AppSettingsTestScope>
{
}
