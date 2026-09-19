# Changing your key/provider, connecting integrations, or resetting

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
