using System;

namespace Aurora.App.Models
{
    /// <summary>One local Aurora user. Profile data is local-only and is not authentication.</summary>
    public sealed class UserProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string DisplayName { get; set; } = "User";
        public string SettingsJson { get; set; } = "";
        public string SpeakerEmbedding { get; set; } = "";
        public bool SpeakerEnrolled { get; set; }
    }
}
