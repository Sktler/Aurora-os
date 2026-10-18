using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
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

    [Fact]
    public void Main_window_profile_name_is_bound_to_the_dashboard_user_name()
    {
        var repoRoot = FindRepositoryRoot();
        var document = XDocument.Load(Path.Combine(repoRoot, "Aurora.App", "Views", "MainWindow.xaml"));
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var profileName = document
            .Descendants(presentation + "TextBlock")
            .SingleOrDefault(element => (string?)element.Attribute(xaml + "Name") == "ProfileNameText");

        Assert.NotNull(profileName);
        Assert.Equal("{Binding UserName}", (string?)profileName.Attribute("Text"));
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
