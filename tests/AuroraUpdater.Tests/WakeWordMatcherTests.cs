using Aurora.App.Services;
using Xunit;

public class WakeWordMatcherTests
{
    [Theory]
    [InlineData("hey aurora", 0.6f, true)]
    [InlineData("Hey Aurora", 0.55f, true)]
    [InlineData("hey aurora", 0.3f, false)]
    [InlineData("hello there", 0.9f, false)]
    [InlineData("", 0.9f, false)]
    public void Matches(string text, float confidence, bool expected) =>
        Assert.Equal(expected, WakeWordMatcher.IsWakeWord(text, confidence));
}
