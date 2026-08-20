# Aurora (personal build) — v1.8

A Windows desktop app in the spirit of Zoey OS — a dashboard of specialized AI
"companions," each with its own persona, powered by a chat engine you pick
from a dropdown (Gemini, Groq, ChatGPT, or Claude), with home automation,
real-time weather and web search, Spotify control, system volume control,
scoped local file access, voice input/output, and free image generation.

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
tier — picking either means adding a card to that provider's account. Nothing
about Gemini/Groq changes if you never touch the new options. What's listed
under "Known follow-ups" below is out of scope for this release — tracked
for a possible v2, not missing pieces of v1.

## What's here

- **Dashboard UI** (`Views/MainWindow.xaml`) — left rail of glowing companion
  orbs, right panel with a chat thread for whichever companion is selected.
- **Five default companions**, seeded on first run, each user-renameable
  (pencil icon next to the name in the chat header):
  - **Aurora** — Main orchestrator; default point of contact for anything
    that isn't clearly a specialist's job. Has real-time weather, web
    search, Spotify, and system volume tools.
  - **Scout** — Research and web information. Same tool access as Aurora.
  - **Nova** — Planning, organization, task management.
  - **Sift** — Inbox, documents, information management. Can list and read
    files in one folder you designate (Settings), and any companion accepts
    a file attached directly in the composer.
  - **Home** — Smart home control; genuinely acts on devices via tool-use,
    not just describes doing so.
- **GeminiClient** (`Services/GeminiClient.cs`) — calls Google's Gemini API
  directly (`generativelanguage.googleapis.com`), free tier, no billing
  required. Every companion shares this client but supplies its own system
  prompt, so it acts like a different specialist. Also supports a tool-use
  loop (`SendWithToolsAsync`) for companions that need to call out to other
  services mid-conversation.
- **WeatherClient** (`Services/WeatherClient.cs`) — real-time conditions via
  Open-Meteo, completely free, no API key, no account.
- **WebSearchClient** (`Services/WebSearchClient.cs`) — a quick free
  instant-answer lookup (DuckDuckGo, no key) for simple facts; falls back to
  opening full results in your browser for anything deeper.
- **SpotifyClient** / **SpotifyAuthClient** (`Services/Spotify*.cs`) — real
  OAuth sign-in (Authorization Code + PKCE, no client secret needed) and
  Web API calls for what's playing, searching, and play/pause/skip.
- **SystemVolumeControl** (`Services/SystemVolumeControl.cs`) — direct
  control of the PC's master output volume via Windows' own Core Audio
  API, no external package.
- **FileTools** (`Services/FileTools.cs`) — Sift's document access. Scoped
  to exactly one folder the user picks explicitly in Settings; every path
  is re-validated to stay inside that folder before anything's read, so
  there's no way to escape it via a crafted file name. Text-style formats
  only for now (txt, md, csv, json, code files, etc.) - PDFs, Word docs,
  spreadsheets, and images aren't parsed yet. The composer's 📎 attach
  button uses the same reader for a single explicitly-picked file, no
  folder access needed.
- **ImageGenClient** (`Services/ImageGenClient.cs`) — generates images
  through Gemini's free image model by default, using the *same* Gemini key
  as chat (no separate provider key needed). Can be pointed at OpenAI
  instead by changing `ImageProvider` to `"openai"` and supplying
  `ImageProviderApiKey`.
- **VoiceService** (`Services/VoiceService.cs`) — wraps Windows' built-in
  speech recognition/synthesis, fully local and free (no API key). The mic
  button in the composer transcribes a spoken message into the text box; the
  speaker toggle next to it reads that companion's replies aloud.
- **MemoryStore** (`Services/MemoryStore.cs`) — local SQLite database at
  `%AppData%\Aurora\aurora.db`. Each companion's conversation history and
  name persist across app restarts. Nothing here touches the network.
- **Integrations window** (`Views/IntegrationsWindow.xaml`, gear icon in the
  sidebar) — connect SmartThings and Home Assistant (with automatic device/
  entity discovery, no manual per-device setup), and mark Alexa/Google as
  connected once set up outside the app.
- **HomeTools** (`Services/HomeTools.cs`) — the tool definitions and
  execution logic that let the Home companion list and control real devices
  through SmartThings/Home Assistant.

## Getting it running

1. **Install prerequisites**
   - Visual Studio 2022 (Community is fine), with the **.NET desktop
     development** workload checked during install.
   - .NET 8 SDK (Visual Studio installer will offer this automatically).

2. **Open it**
   - Double-click `ZoeyOS.sln`, or open Visual Studio → *Open a project or
     solution* → select `ZoeyOS.sln`.
   - Let NuGet restore packages (Sqlite, System.Text.Json,
     CommunityToolkit.Mvvm, System.Speech) — happens automatically on first
     build.

3. **Add your API key**
   - Press F5 to run. On first launch (no key saved yet), a **setup window**
     pops up automatically. Pick **Gemini** (default — bundles free image
     generation with the same key) or **Groq** (faster, higher rate limits,
     but no image generation included).
   - Click **"Get a free key"** to open the right sign-in page in your
     browser — Google AI Studio for Gemini, Groq Console for Groq. Sign in,
     create a key, and copy it — no credit card, no billing setup. Alt-tab
     back to Aurora and it'll auto-paste the key from your clipboard if it
     looks right for the provider you picked.
   - Below that, a row of links (**API docs · Rate limits · Models ·
     Pricing**) always points at the provider you currently have selected —
     switch the radio button and the links switch with it.
   - Hitting **Save and continue** restarts Aurora automatically so the new
     key takes effect right away — no manual relaunch needed.
   - Companions (and, on Gemini, image generation) come online as soon as
     the key is saved; without one, a companion will just tell you what's
     missing instead of crashing.

4. **Run it**
   - F5 in Visual Studio. You should see the dashboard with five orbs on the
     left, and Aurora's chat panel open by default.
   - Each companion's composer has a 🎤 mic button (speak a message instead
     of typing it) and a 🔇/🔊 toggle (have that companion's replies read
     aloud). Both run fully offline through Windows' built-in speech engine —
     no extra setup, no API key.

## Changing your key/provider, connecting integrations, or resetting

All of this lives behind the **gear icon** in the sidebar (now called
**Settings**, since it covers more than integrations):

- **Change API key / provider** — reopens the same setup window used on
  first run, pre-filled with whatever's currently configured. A dropdown
  picks between **Gemini, Groq, ChatGPT, or Claude** — swap providers or
  just paste in a new key anytime; saving restarts Aurora so it takes
  effect immediately. Gemini and Groq are genuinely free; ChatGPT (OpenAI)
  and Claude (Anthropic) are paid, metered APIs with no permanent free
  tier — the setup window says this plainly for whichever one you pick.
- **Model** — a free-text field under the AI engine section, not a fixed
  dropdown, so Aurora is never pinned to whichever model happened to be
  current when it was built. Type any model name your active provider
  serves (e.g. Gemini's `gemini-3.6-flash`, Groq's
  `llama-3.3-70b-versatile`, OpenAI's `gpt-4o-mini`, or Claude's
  `claude-sonnet-5`) and hit **Save model** - it takes effect on your very
  next message, no restart needed. A leading `models/` (as Google's own
  docs sometimes write it) is stripped automatically either way.
- **Reset Aurora completely** — wipes every key, token, and every
  companion's renamed name and chat history, then restarts as if freshly
  installed. Asks for confirmation first; can't be undone.
- **Voice** — pick any voice Windows has installed (whatever's available on
  your PC - different genders, accents, etc.), test it, and toggle whether
  companions speak replies out loud automatically (on by default).
- **Weather & web search** — nothing to configure; already work.
- **System volume** — a slider and mute toggle for your PC's master output
  volume. Companions can also adjust this when you ask them to.
- **Spotify** — real OAuth sign-in (opens your browser, you approve access).
  Needs a **free Client ID** from Spotify's developer dashboard - the
  window's "How to get a Client ID" expander walks through it (a couple of
  minutes, no client secret needed since this uses the PKCE flow meant for
  desktop apps). Reading what's playing and searching works on any account;
  actually starting/pausing/skipping playback needs Spotify **Premium** and
  an active device (Spotify open somewhere).
- **Sift's documents folder** — click "Choose folder..." and pick exactly
  one folder via the normal Windows folder picker; that's the only place
  Sift can read from. "Clear" revokes access entirely. Text-style files
  only for now.
- **SmartThings / Home Assistant** — paste your token (and URL, for Home
  Assistant) and hit **Save & Test**. This makes a real API call and tells
  you what actually happened — "Connected, found N devices" or the real
  error if the token's wrong, not just "a token was typed in."
- **Google (Gmail, Drive & Docs)** — does a genuine Google OAuth sign-in
  (opens your browser, you approve access, Aurora gets a token back) rather
  than a fake toggle. One real requirement here: **you need your own free
  Google Cloud OAuth client** (Client ID + Secret) — Aurora can't ship one
  on your behalf, since every installed app is expected to register its own
  rather than share one. The window has a step-by-step "How to get a Client
  ID / Secret" expander that walks through it (a few minutes in Google
  Cloud Console, no cost). Once connected, it shows the actual email you
  signed in as. Note: the sign-in itself is real and verified, but Sift
  doesn't yet pull real Gmail/Drive data into conversations — see Known
  follow-ups below.
- **Alexa** — stays a manual toggle; see Known follow-ups for why this one
  genuinely can't become a real one-click connection.

## Known follow-ups (out of scope for v1)

v1 is done: build it, run it, chat with all five companions, rename them,
switch chat engines, verify and connect SmartThings/Home Assistant/Google/
Spotify, and have Home and Aurora/Scout actually act (devices, weather,
search, music, volume) instead of just describing it. These are
deliberately deferred to a possible v2, not gaps in v1:

- **Background task engine** — right now sending a message blocks that one
  companion; true "runs in the background while you do something else"
  behavior needs a task queue.
- **Status tray / activity feed** — the plan called for a persistent bar
  showing what's running across companions; not built in v1.
- **Image generation surfaced in chat** — `ImageGenClient` works standalone
  (returns a base64 image ready to display) but isn't hooked into the chat
  flow yet (e.g. detecting "generate an image of ___" mid-conversation and
  rendering the result inline as a picture, not just returned data).
- **Free-tier rate limits** — whichever engine you pick, the free key is
  genuinely $0, but rate-limited (Gemini: chat ~10-15 req/min, images
  ~500/day; Groq: ~30 req/min, ~14,400/day). Fine for personal use; heavy
  back-to-back use across all five companions could occasionally hit a
  limit.
- **Sift doesn't read Gmail/Drive yet** — the Google integration now does a
  real OAuth sign-in and genuinely verifies the connection (shows your
  actual email once connected), but nothing calls the Gmail/Drive APIs yet
  to pull real data into a conversation. Local file access is built,
  though: Sift can list/read files in one folder you designate in Settings,
  and any companion can have a file attached directly in the composer.
- **Only plain-text files are readable** — the documents folder and the
  attach button both work for text-style formats (txt, md, csv, json, code
  files, etc.) but not PDFs, Word docs, spreadsheets, or images - those need
  real parsing that isn't built yet.
- **Alexa can't be a real one-click connection** — Alexa only talks to
  external apps through a published Smart Home Skill (its own Amazon
  Developer account, a hosted Lambda function, Amazon's account-linking
  flow). No token or key shortcuts that. The toggle in Integrations just
  tracks that you've set that up elsewhere; it isn't a live connection.
- **Only Spotify got a real music integration** — Windows Media Player,
  Apple Music, and Pandora were part of the original ask but weren't built:
  WMP is legacy and rarely used anymore, Apple Music has no public playback
  API outside a paid Apple Developer Program membership, and Pandora shut
  down third-party API access years ago. Spotify was the one with a
  genuinely free, working API, so that's what got built.
- **Phone version** — this is WPF, Windows-only by design per your call for
  a Windows-first build. A phone companion app would be a separate project
  (likely .NET MAUI) sharing the same backend concepts.

## Project layout

```
ZoeyOS.sln
ZoeyOS.App/
  App.xaml / App.xaml.cs        - startup, wires up services, provider
                                   selection, full reset
  Models/                       - Companion, ChatMessage, DiscoveredDevice
  Services/                     - AppSettings, IChatEngine (shared interface),
                                   GeminiClient, GroqClient, VoiceService,
                                   WeatherClient, WebSearchClient,
                                   SpotifyClient, SpotifyAuthClient,
                                   SystemVolumeControl, MemoryStore,
                                   ImageGenClient, SmartThingsClient,
                                   HomeAssistantClient, GoogleAuthClient,
                                   HomeTools, SystemTools, FileTools
  ViewModels/                   - DashboardViewModel, CompanionViewModel,
                                   IntegrationsViewModel
  Views/                        - MainWindow.xaml, IntegrationsWindow.xaml
                                   (now the Settings window: engine switch,
                                   reset, voice, volume, real connection
                                   tests, Google/Spotify OAuth), SetupWindow.xaml
                                   (first-run + reopenable provider/key
                                   setup), Converters.cs (all XAML value
                                   converters in one file rather than one
                                   file each)
```
