using System;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests
{
    public class ProfileFeatureTests
    {
        [Fact]
        public void SpeakerEmbedding_is_stable_for_the_same_audio()
        {
            var service = new SpeakerRecognitionService();
            var audio = new byte[16000 * 2];
            for (var i = 0; i < audio.Length / 2; i++)
            {
                var sample = (short)(Math.Sin(i * 2 * Math.PI * 180 / 16000) * 8000);
                BitConverter.GetBytes(sample).CopyTo(audio, i * 2);
            }

            var embedding = service.CreateEmbedding(audio);

            Assert.False(string.IsNullOrWhiteSpace(embedding));
            Assert.Equal(1.0, service.Compare(embedding, embedding), 5);
        }

        [Fact]
        public void SpeakerEmbedding_rejects_empty_audio()
        {
            var service = new SpeakerRecognitionService();

            Assert.Equal(string.Empty, service.CreateEmbedding(Array.Empty<byte>()));
        }

        [Fact]
        public void SpeakerSuggestion_requires_a_confident_enrolled_match()
        {
            var service = new SpeakerRecognitionService();
            var audio = new byte[16000 * 2];
            for (var i = 0; i < audio.Length / 2; i++)
            {
                var sample = (short)(Math.Sin(i * 2 * Math.PI * 180 / 16000) * 8000);
                BitConverter.GetBytes(sample).CopyTo(audio, i * 2);
            }

            var embedding = service.CreateEmbedding(audio);
            var suggestion = service.Suggest(new[] { ("profile-1", embedding) }, audio);

            Assert.Equal("profile-1", suggestion.ProfileId);
            Assert.True(suggestion.Confidence >= SpeakerRecognitionService.DefaultSuggestionThreshold);
        }
    }
}
