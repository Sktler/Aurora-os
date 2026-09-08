using System.IO;
using System.Xml;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class XamlWellFormedTests
{
    [Fact]
    public void Application_xaml_files_are_well_formed_xml()
    {
        var repoRoot = FindRepositoryRoot();
        var xamlFiles = Directory.GetFiles(
            Path.Combine(repoRoot, "Aurora.App"),
            "*.xaml",
            SearchOption.AllDirectories);

        Assert.NotEmpty(xamlFiles);

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit
        };

        foreach (var xamlFile in xamlFiles)
        {
            using var reader = XmlReader.Create(xamlFile, settings);
            while (reader.Read())
            {
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Aurora.sln")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Aurora.App")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Aurora repository root.");
    }
}
