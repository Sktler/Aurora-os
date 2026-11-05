# Changing your key/provider, connecting integrations, or resetting

All of this lives behind the **gear icon** in the sidebar (now called
**Settings**, since it covers more than integrations):

- **AI engine** — choose Gemini, Groq, OpenAI, Claude, or GitHub Copilot,
  enter a key, and select or type a model directly in this panel. Switching
  providers loads that provider's live model list when a saved key is available.
  **Save provider and model** applies to the next request without a restart.
  The model field remains editable if discovery fails or you use a model that
  is not listed. A leading `models/` prefix is stripped when saving. The same
  editor appears in first-run setup, alongside profile/password controls.
  Keys are stored through Windows Credential Vault. Provider pricing,
  account permissions, and model availability still apply.
- **System > Interface language** — change the UI language immediately.
  The welcome screen also asks for a language before provider setup. Speech
  recognition follows the app language when a matching Windows recognizer
  is installed; otherwise it uses the existing Windows/English fallback.
- **Tools** — edit the Windows capability switches, camera controls, image
  integration status, and MCP controls directly inside Settings. Capabilities
  remain opt-in. Device access also requires Windows permission. Disabling
  Location clears dashboard weather; weather for a place explicitly named in
  a chat remains available without retrieving device location.
- **Appearance** — theme and orb-skin changes apply immediately. Profile,
  chat, recovery, menu, and dropdown surfaces use the shared theme palette.
- **Reset Aurora completely** — wipes every key, token, and every
  companion's renamed name and chat history, then restarts as if freshly
  installed. Requires the developer credential and confirmation; cannot be undone.
- **Voice** — pick any voice Windows has installed (whatever's available on
  your PC - different genders, accents, etc.), test it, and toggle whether
  companions speak replies out loud automatically (on by default).
- **Weather & web search** — dashboard weather requires both Aurora location
  consent and Windows location permission. Web search uses the existing service.
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
- **SmartThings / Home Assistant / Hubitat** — paste your token (and URL, for
  Home Assistant, plus Maker API URL for Hubitat) and hit **Save & Test**.
  This makes a real API call and tells you what actually happened —
  "Connected, found N devices" or the real error if the token's wrong, not
  just "a token was typed in."
- **Google (Gmail, Drive & Docs)** — does a genuine Google OAuth sign-in
  (opens your browser, you approve access, Aurora gets a token back) rather
  than a fake toggle. One real requirement here: **you need your own free
  Google Cloud OAuth client** (Client ID + Secret) — Aurora can't ship one
  on your behalf, since every installed app is expected to register its own
  rather than share one. The window has a step-by-step "How to get a Client
  ID / Secret" expander that walks through it (a few minutes in Google
  Cloud Console, no cost). Once connected, it shows the actual email you
  signed in as. Note: the sign-in itself is real and verified, but Sift
  doesn't yet pull real Gmail/Drive data into conversations — see [Known follow-ups](known-follow-ups.md).
- **Alexa** — stays a manual toggle; see Known follow-ups for why this one
  genuinely can't become a real one-click connection.
