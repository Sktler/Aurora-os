# Known follow-ups (out of scope for v1)

v1 is done: build it, run it, chat with all five companions, rename them,
switch chat engines, verify and connect SmartThings/Home Assistant/Hubitat/
Google/Spotify, and have Home and Aurora/Scout actually act (devices, weather,
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
