using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Aurora.App.Services
{
    /// <summary>
    /// Local, opt-in speaker suggestion helper. It deliberately produces a suggestion only;
    /// it never authenticates a person or grants permissions. Profiles must be confirmed by
    /// the user before Aurora switches profiles.
    /// </summary>
    public sealed class SpeakerRecognitionService
    {
        public const int FeatureCount = 13;
        public const double DefaultSuggestionThreshold = 0.82;

        public string CreateEmbedding(byte[] pcm16Mono16k)
        {
            if (pcm16Mono16k == null || pcm16Mono16k.Length < 3200) return "";
            var samples = new double[pcm16Mono16k.Length / 2];
            for (var i = 0; i < samples.Length; i++)
                samples[i] = BitConverter.ToInt16(pcm16Mono16k, i * 2) / 32768.0;

            var features = ExtractFeatures(samples);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join(",", features.Select(v => v.ToString("R", CultureInfo.InvariantCulture)))));
        }

        public double Compare(string embeddingA, string embeddingB)
        {
            var a = Parse(embeddingA);
            var b = Parse(embeddingB);
            if (a.Count != FeatureCount || b.Count != FeatureCount) return 0;
            var distance = Math.Sqrt(a.Zip(b, (x, y) => (x - y) * (x - y)).Sum() / FeatureCount);
            return Math.Clamp(1.0 - distance * 4.0, 0, 1);
        }

        public (string? ProfileId, double Confidence) Suggest(IEnumerable<(string Id, string Embedding)> profiles, byte[] pcm16Mono16k)
        {
            var sample = CreateEmbedding(pcm16Mono16k);
            if (string.IsNullOrWhiteSpace(sample)) return (null, 0);
            var best = profiles.Select(p => (p.Id, Confidence: Compare(sample, p.Embedding)))
                .OrderByDescending(p => p.Confidence).FirstOrDefault();
            return best == default || best.Confidence < DefaultSuggestionThreshold ? (null, best.Confidence) : best;
        }

        private static double[] ExtractFeatures(double[] s)
        {
            var n = s.Length;
            var mean = s.Average();
            var rms = Math.Sqrt(s.Select(x => x * x).Average());
            var abs = s.Select(Math.Abs).Average();
            var zeroCross = 0.0;
            for (var i = 1; i < n; i++) if ((s[i - 1] >= 0) != (s[i] >= 0)) zeroCross++;
            zeroCross /= Math.Max(1, n - 1);

            // Compact spectral-shape vector using 10 fixed bands. This is intentionally a
            // coarse voice signature, not biometric authentication.
            var bands = new double[10];
            for (var k = 1; k < n; k++)
            {
                var mag = Math.Abs(s[k] - s[k - 1]);
                var band = Math.Min(9, k * 10 / Math.Max(1, n));
                bands[band] += mag;
            }
            var total = bands.Sum();
            if (total > 0) for (var i = 0; i < bands.Length; i++) bands[i] /= total;
            return new[] { mean, rms, abs, zeroCross }.Concat(bands.Take(9)).ToArray();
        }

        private static List<double> Parse(string encoded)
        {
            try
            {
                var csv = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToList();
            }
            catch { return new List<double>(); }
        }
    }
}
