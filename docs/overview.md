# Aurora (personal build) — v1.8

A Windows desktop app in the spirit of Zoey OS — a dashboard of specialized AI
"companions," each with its own persona, powered by a chat engine you pick
from a dropdown (Gemini, Groq, ChatGPT, Claude, or GitHub Copilot), with
home automation, real-time weather and web search, Spotify control, system
volume control, scoped local file access, voice input/output, and free image
generation.

**v1 is complete and closed out.** v1.1 swapped the engine from Anthropic's
Claude (paid-only) to a free provider so the app costs nothing to run
day-to-day. v1.2 tried Groq (faster, permanent free tier) and added voice
mode plus auto-restart after first-run setup. v1.3 settled on **Gemini** as
the default — its conversational quality sits closer to Claude than Groq's
open-weight models, and its free tier bundles image generation ("Nano
Banana") under the *same* key. v1.4 made the engine choice a real setting
(switch between Gemini and Groq anytime, not just at first install), added
a full reset, and replaced the fake "connected" toggles on SmartThings,
Home Assistant, and Google with real, verified connections. v1.5 added
real-time weather and web search (both free, no setup), a real Spotify
integration (OAuth, no client secret needed), and system volume control —
all usable directly in chat, not just from Settings. v1.6 added real (but
deliberately scoped) file access: Sift can read files in one folder you
explicitly choose, and any companion accepts a file attached directly in
the composer — no blanket file-system access, by design. v1.7 added a
free-text model field in Settings, so Aurora isn't pinned to whichever
Gemini or Groq model happened to be current when it was built - type any
model name and it takes effect on the next message. v1.8 brought Claude
back (alongside adding ChatGPT) as full, switchable engine options — the
provider picker in Setup/Settings is now a dropdown covering all four, each
with its own free-text model field. Worth repeating the v1.1 trade-off
directly: **Gemini and Groq are genuinely free; ChatGPT and Claude are not.**
OpenAI and Anthropic are metered, pay-as-you-go APIs with no permanent free
tier — picking either means adding a card to that provider's account. v1.9
added an experimental fifth option, **GitHub Copilot** — but unlike the
other four, this one is *unofficial*: GitHub doesn't publish a public chat
API for Copilot, so this integration works by exchanging your GitHub token
through the same internal, undocumented endpoint the official editor
extensions use. It requires an account with an active Copilot subscription,
is not sanctioned by GitHub, and could stop working or be blocked at any
time — the setup window and provider catalog both call this out plainly.
Nothing about Gemini/Groq changes if you never touch the new options. What's listed
in [Known follow-ups](known-follow-ups.md) is out of scope for this release — tracked
for a possible v2, not missing pieces of v1.
