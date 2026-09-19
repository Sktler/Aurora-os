# Project status

_Last reviewed against `master`: 2026-09-19._

## Current application

Aurora is currently a **Windows WPF desktop application** targeting **.NET 10** (`net10.0-windows10.0.17763.0`). The current repository contains `Aurora.App` plus focused test projects.

### AI and companions

- Five specialist companions remain the core interaction model: Aurora, Scout, Nova, Sift, and Home.
- The provider layer includes Gemini, Groq, OpenAI/ChatGPT, Claude, and an experimental GitHub Copilot client.
- Providers share an `IChatEngine` abstraction.
- Companion state already includes status, accent color, tool-access category, last activity, and a flag for future background execution.
- Conversation/memory data persists locally through SQLite.

### Local Windows capabilities

The current service layer includes:

- Voice input/output plus a local wake-word service using **“Hey Aurora.”**
- Webcam discovery, live preview, photo capture, and camera tools.
- Windows application launching, file read/write primitives, clipboard access, screen capture, terminal execution, network/power controls, and permission gates.
- System metrics and Windows update services.
- A mini-companion window, dashboard, settings, memory view, particle orb, and other desktop UI components.
- MCP support exists at the service level.

### External integrations

Current code includes integrations or clients for:

- Weather and web search.
- Spotify and Windows media control.
- SmartThings, Home Assistant, and Hubitat.
- Google OAuth groundwork.
- Image generation.

## Main gaps visible from the current code

- There is no full background task queue, scheduler, or unified activity center yet.
- `FileTools` still exposes read/list operations for plain-text-style files; rich document formats are not handled there.
- Low-level Windows file writing exists, but there is no polished conversational workflow for creating documents/spreadsheets with previews and safe overwrite handling.
- Google authentication exists, but full Gmail/Drive conversation tools are not yet present.
- `ImageGenClient` exists, but image generation is not yet a first-class inline chat experience.
- MCP support exists, but users do not yet have a full connection/tool management UI.
- Permission controls are spread across capabilities; there is no single approval center, temporary-grant model, or action audit trail.
- Provider switching exists, but emergency disconnect, spend limits, health/failover, per-companion providers, and local providers are not yet complete product features.
- Existing automated tests cover only a small part of the app compared with the size of the service surface.

## Documentation drift to clean up

Some older docs still describe the earlier v1.x state and older prerequisites. The project file now targets .NET 10, so setup/overview docs should be kept aligned with `master` as features land.

## Platform note

The current `master` tree does **not** contain an Android project, even though the issue history includes substantial Android work. Treat mobile as a separate platform decision rather than assuming those historical issues describe the current build.
