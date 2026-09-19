# What's here

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
  `%AppData%\\Aurora\\aurora.db`. Each companion's conversation history and
  name persist across app restarts. Nothing here touches the network.
- **Integrations window** (`Views/IntegrationsWindow.xaml`, gear icon in the
  sidebar) — connect SmartThings, Home Assistant, and Hubitat (with automatic
  device/entity discovery and one merged device catalog, no manual per-device
  setup), and mark Alexa/Google as connected once set up outside the app.
- **HomeTools** (`Services/HomeTools.cs`) — the tool definitions and
  execution logic that let the Home companion list, filter, and control real
  devices through SmartThings/Home Assistant/Hubitat.
