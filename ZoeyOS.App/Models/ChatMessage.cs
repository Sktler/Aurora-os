using System;

namespace ZoeyOS.App.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public string CompanionId { get; set; } = "";
        public string Role { get; set; } = "user"; // "user" | "assistant"
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
