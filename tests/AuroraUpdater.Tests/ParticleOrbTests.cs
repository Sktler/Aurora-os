using System.IO;
using Xunit;

namespace AuroraUpdater.Tests;

public sealed class ParticleOrbTests
{
    [Theory]
    [InlineData("Aurora.App/Views/MainWindow.xaml")]
    [InlineData("Aurora.App/Views/MiniCompanionWindow.xaml")]
    public void Orb_surfaces_use_the_animated_particle_control(string relativePath)
    {
        var repoRoot = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(repoRoot, relativePath));

        Assert.Contains("ParticleOrb", xaml);
    }

    [Fact]
    public void Particle_orb_stops_rendering_when_unloaded()
    {
        var repoRoot = FindRepositoryRoot();
        var code = File.ReadAllText(Path.Combine(repoRoot, "Aurora.App", "Views", "ParticleOrb.xaml.cs"));

        Assert.Contains("Loaded += (_, _) => StartAnimating();", code);
        Assert.Contains("Unloaded += (_, _) => StopAnimating();", code);
    }

    [Fact]
    public void Particle_orb_uses_the_provided_artwork_with_a_face_and_nodding_motion()
    {
        var repoRoot = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(repoRoot, "Aurora.App", "Views", "ParticleOrb.xaml"));
        var code = File.ReadAllText(Path.Combine(repoRoot, "Aurora.App", "Views", "ParticleOrb.xaml.cs"));

        Assert.Contains("AuroraOrb.png", xaml);
        Assert.Contains("RenderTransformOrigin", xaml);
        Assert.Contains("OrbTranslation.X", code);
        Assert.Contains("OrbNod", xaml);
        Assert.Contains("LeftEyeScale", xaml);
        Assert.Contains("RightEyeScale", xaml);
        Assert.Contains("MouthScale", xaml);
        Assert.Contains("OrbNod.Angle", code);
        Assert.Contains("LeftEyeScale.ScaleY", code);
        Assert.Contains("MouthScale.ScaleY", code);
        Assert.DoesNotContain("RotationSpeed", code);
        Assert.DoesNotContain("OrbScale", code);
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
