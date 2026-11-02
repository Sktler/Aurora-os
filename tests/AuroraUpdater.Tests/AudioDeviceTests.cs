using System.Globalization;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests
{
    public class AudioDeviceTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ResolveInputDeviceNumber_BlankName_UsesSystemDefault(string? name)
        {
            Assert.Equal(-1, AudioDeviceCatalog.ResolveInputDeviceNumber(name));
        }

        [Fact]
        public void ResolveInputDeviceNumber_UnknownDevice_FallsBackToSystemDefault()
        {
            Assert.Equal(-1, AudioDeviceCatalog.ResolveInputDeviceNumber("No such microphone 7f3a"));
            Assert.Equal(-1, AudioDeviceCatalog.ResolveOutputDeviceNumber("No such speaker 7f3a"));
        }

        [Fact]
        public void LiveMicrophoneStream_ReadsPushedAudioThenEndsOnComplete()
        {
            using var stream = new LiveMicrophoneStream();
            stream.Push(new byte[] { 1, 2, 3 });
            stream.Push(new byte[] { 4, 5 });
            stream.Complete();

            var buffer = new byte[10];
            var read = stream.Read(buffer, 0, buffer.Length);

            Assert.Equal(5, read);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer[..5]);
            Assert.Equal(0, stream.Read(buffer, 0, buffer.Length));
        }

        [Fact]
        public void LiveMicrophoneStream_PushAfterCompleteOrDispose_DoesNotThrow()
        {
            var stream = new LiveMicrophoneStream();
            stream.Complete();
            stream.Push(new byte[] { 1 });
            stream.Dispose();
            stream.Push(new byte[] { 1 });
        }

        private static readonly CultureInfo[] Installed =
        {
            new("en-US"), new("es-ES"), new("de-DE"),
        };

        [Fact]
        public void PickRecognizerCulture_PrefersAppLanguage()
        {
            var picked = WakeWordRecognizer.PickRecognizerCulture(Installed, "es", new CultureInfo("en-US"));
            Assert.Equal("es-ES", picked?.Name);
        }

        [Fact]
        public void PickRecognizerCulture_MissingAppLanguage_FallsBackToWindowsCulture()
        {
            var picked = WakeWordRecognizer.PickRecognizerCulture(Installed, "fr", new CultureInfo("de-DE"));
            Assert.Equal("de-DE", picked?.Name);
        }

        [Fact]
        public void PickRecognizerCulture_NothingInstalled_ReturnsNull()
        {
            Assert.Null(WakeWordRecognizer.PickRecognizerCulture(System.Array.Empty<CultureInfo>(), "en", new CultureInfo("en-US")));
        }
    }
}
