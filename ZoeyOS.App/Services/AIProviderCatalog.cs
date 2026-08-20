using System.Collections.Generic;
using System.Linq;

namespace ZoeyOS.App.Services
{
    /// <summary>Everything the UI needs to know about one chat provider, so SetupWindow and
    /// the Settings model field both read from one source of truth instead of duplicating
    /// provider-specific text in two places. Adding a fifth provider later is: build an
    /// IChatEngine implementation, add one entry here, add one switch arm in
    /// App.xaml.cs's BuildChatEngine()/ActiveProviderIsConfigured().</summary>
    public sealed class AIProviderInfo
    {
        public required string Key { get; init; } // matches AppSettings.ChatProvider, e.g. "gemini"
        public required string DisplayName { get; init; }
        public required string DefaultModel { get; init; }
        public required string ModelExamples { get; init; } // shown as a hint under the model field
        public required string KeyHint { get; init; }
        public required string GetKeyUrl { get; init; }
        public required string GetKeyButtonText { get; init; }
        public required string DocsUrl { get; init; }
        public required string RateLimitsUrl { get; init; }
        public required string ModelsUrl { get; init; }
        public required string PricingUrl { get; init; }
        public required string CostNote { get; init; } // honest free-vs-paid summary
        public bool BundlesImageGen { get; init; }
        public required string KeyShapePrefix { get; init; } // for clipboard auto-paste detection
    }

    public static class AIProviderCatalog
    {
        public static readonly IReadOnlyList<AIProviderInfo> All = new List<AIProviderInfo>
        {
            new()
            {
                Key = "gemini",
                DisplayName = "Gemini (Google)",
                DefaultModel = "gemini-3.6-flash",
                ModelExamples = "gemini-3.6-flash, gemini-3.5-flash-lite, gemini-3-pro",
                KeyHint = "Free, no card needed - sign in with any Google account and copy the key back here.",
                GetKeyUrl = "https://aistudio.google.com/apikey",
                GetKeyButtonText = "Get a free key",
                DocsUrl = "https://ai.google.dev/gemini-api/docs",
                RateLimitsUrl = "https://ai.google.dev/gemini-api/docs/rate-limits",
                ModelsUrl = "https://ai.google.dev/gemini-api/docs/models",
                PricingUrl = "https://ai.google.dev/gemini-api/docs/pricing",
                CostNote = "The free tier is rate-limited (chat: roughly 10-15 requests/minute; images: up to ~500/day) - plenty for personal use, but heavy back-to-back use across all five companions could occasionally hit a limit.",
                BundlesImageGen = true,
                KeyShapePrefix = "AIza"
            },
            new()
            {
                Key = "groq",
                DisplayName = "Groq",
                DefaultModel = "llama-3.3-70b-versatile",
                ModelExamples = "llama-3.3-70b-versatile, llama-3.1-8b-instant",
                KeyHint = "Free forever, no card needed - sign in with any email and copy the key back here.",
                GetKeyUrl = "https://console.groq.com/keys",
                GetKeyButtonText = "Get a free key",
                DocsUrl = "https://console.groq.com/docs/quickstart",
                RateLimitsUrl = "https://console.groq.com/docs/rate-limits",
                ModelsUrl = "https://console.groq.com/docs/models",
                PricingUrl = "https://groq.com/pricing",
                CostNote = "The free tier is rate-limited (roughly 30 requests/minute, up to ~14,400/day depending on the model) - plenty for personal use. Doesn't include image generation - Home/Sift image requests would need a separate Gemini key added too.",
                BundlesImageGen = false,
                KeyShapePrefix = "gsk_"
            },
            new()
            {
                Key = "openai",
                DisplayName = "ChatGPT (OpenAI)",
                DefaultModel = "gpt-4o-mini",
                ModelExamples = "gpt-4o-mini, gpt-4o",
                KeyHint = "Requires an OpenAI account with billing set up - paste a key from your API dashboard.",
                GetKeyUrl = "https://platform.openai.com/api-keys",
                GetKeyButtonText = "Get an API key",
                DocsUrl = "https://platform.openai.com/docs/overview",
                RateLimitsUrl = "https://platform.openai.com/docs/guides/rate-limits",
                ModelsUrl = "https://platform.openai.com/docs/models",
                PricingUrl = "https://openai.com/api/pricing",
                CostNote = "This is a paid, metered API - OpenAI doesn't offer a permanent free tier (new accounts sometimes get a small trial credit that expires). Lightweight models are inexpensive for personal chat use, but you'll need a card on file eventually.",
                BundlesImageGen = false,
                KeyShapePrefix = "sk-"
            },
            new()
            {
                Key = "claude",
                DisplayName = "Claude (Anthropic)",
                DefaultModel = "claude-sonnet-5",
                ModelExamples = "claude-sonnet-5, claude-haiku-4-5, claude-opus-4-8",
                KeyHint = "Requires an Anthropic account with billing set up - paste a key from your API console.",
                GetKeyUrl = "https://console.anthropic.com/settings/keys",
                GetKeyButtonText = "Get an API key",
                DocsUrl = "https://docs.claude.com/en/docs/intro",
                RateLimitsUrl = "https://docs.claude.com/en/api/rate-limits",
                ModelsUrl = "https://docs.claude.com/en/docs/about-claude/models/overview",
                PricingUrl = "https://www.anthropic.com/pricing",
                CostNote = "This is a paid, metered API - Anthropic doesn't offer a permanent free tier (new accounts sometimes get a small trial credit that expires). You'll need a card on file for ongoing use.",
                BundlesImageGen = false,
                KeyShapePrefix = "sk-ant-"
            }
        };

        public static AIProviderInfo Get(string key) =>
            All.FirstOrDefault(p => p.Key == key) ?? All[0];
    }
}
