# AGENTS.md

## Project overview

Aurora is a Windows-first WPF desktop AI companion application. It provides a dashboard of specialized companions, selectable AI providers, persistent local memory, voice input/output, image generation, web/weather tools, Spotify, scoped local-file access, and smart-home integrations.

The main application is in `Aurora.App/`, with the solution in `Aurora.sln` and automated tests in `tests/AuroraUpdater.Tests/`.

## Repository map

- `Aurora.App/App.xaml.cs` — application startup, settings loading, provider selection, service wiring, reset/restart behavior.
- `Aurora.App/Models/` — application/domain models such as companions, messages, and discovered devices.
- `Aurora.App/Services/` — API clients, settings, memory, voice, image generation, smart-home tools, Windows automation, and shared AI interfaces.
- `Aurora.App/ViewModels/` — MVVM view models, including dashboard, companions, and integrations/settings.
- `Aurora.App/Views/` — WPF XAML windows and code-behind.
- `tests/AuroraUpdater.Tests/` — xUnit tests and integration-style source/configuration checks.
- `.github/workflows/windows-build.yml` — CI build definition.

Before changing behavior, inspect the existing implementation and related tests. Prefer extending the current architecture over introducing parallel systems.

## Platform and build

- Target platform: Windows.
- Main project: `Aurora.App/Aurora.App.csproj`.
- Target framework: `.NET 10` with WPF and Windows Forms enabled.
- Nullable reference types are enabled.
- Preserve the existing package versions and project configuration unless the task specifically requires changing them.

### Restore and build

Run from the repository root:

```powershell
dotnet restore Aurora.App/Aurora.App.csproj
dotnet build Aurora.App/Aurora.App.csproj --configuration Release --no-restore
```

For a normal local development build, this is also acceptable after restore:

```powershell
dotnet build Aurora.App/Aurora.App.csproj
```

CI currently uses Windows with .NET 10 and performs restore followed by a Release build. Keep changes compatible with that environment.

### Tests

The test project is:

```
tests/AuroraUpdater.Tests/AuroraUpdater.Tests.csproj
```

Run:

```powershell
dotnet test tests/AuroraUpdater.Tests/AuroraUpdater.Tests.csproj
```

For changes that affect both application behavior and tests, run the relevant tests and then a Release build. If a test cannot run because the environment lacks Windows-specific prerequisites, report that explicitly rather than claiming it passed.

## Coding conventions

- Follow the existing C# style in the surrounding file.
- Keep nullable warnings meaningful; do not silence them with unnecessary null-forgiving operators.
- Prefer small, focused changes.
- Reuse existing services, interfaces, models, and helper methods before adding new abstractions.
- Preserve MVVM boundaries: UI behavior belongs in view models/services where practical rather than putting application logic directly in XAML code-behind.
- Keep XAML bindings and property names synchronized with their view models.
- When changing a constructor used by XAML, check every dependency because constructor exceptions surface as WPF `XamlParseException` errors.
- Use async APIs for network and other potentially blocking work.
- Do not block the WPF UI thread with synchronous network calls or long-running operations.
- Preserve user-facing error handling. A missing API key or unavailable integration should normally produce a useful error state/message rather than crash application startup.

## AI providers

Aurora uses the shared `IChatEngine` abstraction so the active provider can be changed through settings.

Current provider implementations include Gemini, Groq, OpenAI, Claude, and GitHub Copilot. Provider selection and construction are wired through `App.xaml.cs` and provider metadata/settings services.

When changing an AI provider:

1. Inspect `AppSettings.cs`, `AIProviderCatalog.cs`, `App.xaml.cs`, and the provider client.
2. Preserve the common `IChatEngine` contract.
3. Preserve the free-text model setting behavior.
4. Do not hard-code API keys, tokens, or other credentials.
5. Do not assume an API feature is available merely because another provider supports it.
6. Preserve clear API/connection error messages.

### Gemini

`Aurora.App/Services/GeminiClient.cs` calls Google's Gemini REST API directly and also implements the tool-calling loop used by companions that control external services.

Gemini tool schemas are deliberately converted from Aurora's internal tool format before sending them to Gemini. Be careful when changing schema conversion or conversation-role handling because malformed function declarations and unsupported roles can produce HTTP 400 errors.

Do not put Gemini keys in source code. Runtime credentials belong in the application's settings flow.

## Settings and credentials

`Aurora.App/Services/AppSettings.cs` persists configuration under the user's application-data directory:

```
%AppData%\\Aurora\\settings.json
```

The local memory database is normally:

```
%AppData%\\Aurora\\aurora.db
```

Never commit:

- API keys
- OAuth client secrets
- access/refresh tokens
- passwords
- private certificates
- personal configuration files containing credentials

If a test needs credentials, use environment/configuration injection or a fake/mocked service rather than real secrets.

## WPF and UI rules

- Keep the existing Aurora visual language and companion dashboard behavior unless the task explicitly requests a redesign.
- Test XAML changes for binding errors and constructor failures.
- When adding a UI setting, wire the full path: XAML -> view model -> settings/service -> persistence/runtime behavior.
- A button or toggle should perform the real operation it represents; do not add fake connected states or placeholder success messages when an integration can be tested.
- Preserve accessibility and usable keyboard/mouse interaction where practical.
- Avoid unnecessary changes to generated/resource assets.

## Integrations and external services

Aurora currently contains integrations/services for smart-home platforms, Google OAuth, Spotify, weather, web search, voice, image generation, and Windows automation.

When modifying an integration:

- Keep provider-specific API logic inside its service/client.
- Keep orchestration/tool definitions in the appropriate service rather than embedding HTTP calls in a view.
- Validate configuration before making network calls.
- Return actionable errors.
- Do not claim an integration is connected unless the existing code has actually verified it.
- Preserve the existing scoped-access model for local files and Windows capabilities.

## File access and Windows automation

Local file access is intentionally scoped. Do not broaden file access to the entire machine unless the user explicitly asks for that behavior and the security implications are handled.

Windows automation capabilities are individually controlled by settings. Do not silently enable broader filesystem, terminal, screen, clipboard, application, network, power, camera, microphone, UI-automation, or MCP capabilities.

## Memory and data persistence

Conversation memory is local SQLite data. Changes to message/history persistence should preserve existing companion separation and user-renamable companion names.

Avoid destructive migrations or resets unless explicitly requested. If a schema change is necessary, inspect the existing memory implementation and tests before modifying it.

## Testing changes

For every meaningful code change:

1. Build the affected project.
2. Run the relevant tests.
3. If the change affects WPF/XAML startup, provider construction, settings, or integrations, exercise the affected startup/configuration path where possible.
4. Fix compile/test failures caused by the change before finishing.
5. Do not claim tests were run if they were not.

For UI-only changes where automated tests are not practical, at minimum perform a build and inspect bindings/resources for obvious failures.

## Git and change scope

- Do not rewrite unrelated code.
- Do not reformat whole files merely because a small change is needed.
- Do not delete working functionality to make a build pass.
- Keep commits focused when committing changes.
- Before changing an existing file, understand its current behavior and preserve unrelated functionality.
- If a requested change conflicts with existing behavior, make the smallest change that satisfies the request and explain the compatibility impact.

## Agent workflow

Before editing:

1. Identify the relevant project, service, view model, view, and tests.
2. Read the existing implementation.
3. Check for existing settings/configuration and provider abstractions.
4. Search for all call sites when changing a public method, setting, model, or interface.
5. Make the smallest coherent change.

After editing:

1. Inspect the final diff for accidental changes or secrets.
2. Run the appropriate tests.
3. Run a Release build for substantial changes.
4. Report exactly what was changed and what verification was performed.

## Important project boundaries

- Aurora is Windows/WPF-first; do not convert it to a cross-platform UI framework unless explicitly requested.
- Keep the shared AI-provider abstraction intact.
- Keep API credentials out of source control.
- Keep local-file and Windows automation access scoped and opt-in.
- Preserve real integration verification instead of simulated connection states.
- Do not replace working provider/integration implementations with placeholders merely to simplify a change.
