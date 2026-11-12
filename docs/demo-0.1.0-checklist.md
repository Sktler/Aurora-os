# 0.1.0 Demo implementation and verification

Baseline: `fc07b60`. Packaging/distribution under #210 is excluded. No package
versions, provider interface, database schema, or existing permission scopes
were changed. Configuration and conversation memory remain local.

## Review and merge order

1. [PR #269](https://github.com/Sktler/Aurora-os/pull/269): location consent,
   conversation input, complete wake-phrase dictation, provider errors/fallback,
   selected-item labels, and reserved Emergency Stop space.
2. [PR #270](https://github.com/Sktler/Aurora-os/pull/270), based on #269:
   shared provider setup/editor, inline Tools, and language selection.
3. [PR #271](https://github.com/Sktler/Aurora-os/pull/271), based on #270:
   live theme/localization, Profiles sizing, localized safety-control spacing,
   and welcome-flow polish.
4. [PR #272](https://github.com/Sktler/Aurora-os/pull/272), based on #271: layout recommendations, updated
   settings documentation, final evidence, and a regression ensuring a later
   provider failure cannot erase the original fallback explanation or replay
   a tool.

Existing implementation reviewed before editing: #250 (settings navigation),
#251/#259 (voice), #254 (Emergency Stop anchoring), #255 (localized dialog
layout), #256 (test storage isolation), and #257/#258 (profile/menu styling).
The initial search returned no open pull requests for the scoped work.

## Issue checklist

Implementation is complete for the changes below. The final milestone sign-off
still needs the explicitly outstanding interactive checks in the next section.

| Issue | Implementation and acceptance evidence | PR / verification |
| --- | --- | --- |
| #216 | `WindowsLocationEnabled` records Aurora's Allow/Deny choice. `LocationService` checks it before Windows access and after awaits, while Windows still supplies OS consent. Revocation clears displayed weather and blocks late results. Tools exposes the existing opt-in flow for changing consent. | #269, #270, #271. Isolated denied-consent/persistence regression passes; OS permission dialogs and live weather remain outstanding. |
| #237 | Setup and Settings share `ProviderSettingsViewModel`. Selection loads the saved provider key/model and requests its catalog. Generation checks discard stale results after a switch/key edit; free-text model input is retained. | #270. Provider-switch race and persistence tests pass. Live account catalogs remain outstanding. |
| #238 | Profiles opens at 920×620 DIPs with minimum sizing/work-area bounds. List rows stretch, names wrap, and action rows wrap. | #271. XAML compiles and shared layout checks pass; Profiles at varied DPI remains outstanding. |
| #241 | Explicit UI status/formatted-label bindings depend on language revision; changing a control's label no longer restores its old text. Open context menus and computed default folder/provider labels refresh without replacing user data or bindings. | #271. Live WPF binding, English/Spanish/French/German transition, and updated-label regressions pass. Full application walk-through remains outstanding. |
| #242 | Added supported-language coverage for appearance, provider errors, setup guidance, status labels, and Emergency Stop. Formatted labels retain their data arguments; unknown text keeps the English/source fallback. | #271. Literal XAML label audit and existing localization tests pass. Provider/model IDs, commands, voice IDs, and brand names are intentional technical exceptions. |
| #248 | Investigated current presets, splitters, profile persistence, pins, and narrow-window constraints. Recommendations prioritize an adaptive center minimum, accessible layout controls, and bounded widget ordering, with acceptance criteria and product decisions recorded. | Final verification PR. See [layout recommendations](layout-customization-recommendations.md). No speculative layout system was added. |
| #260 | All five provider clients use actionable HTTP/connection messages instead of raw response bodies/credential-bearing URLs. Gemini may try its live-listed `generateContent` default once after an initial missing-model response; no provider switch, saved-model rewrite, auth/quota retry, or tool replay. The original failure and fallback explanation remain visible on subsequent errors. | #269 and final verification PR. Mock HTTP tests cover success, non-recoverable auth failure, and failure after one tool execution. Live provider failures remain outstanding. |
| #261 | Wake mode uses dictation and routes the complete suffix of an inline wake phrase. A standalone wake phrase retains the existing second capture. Current confidence/permission checks remain; the bounded capture timeout is longer and independently testable. | #269. Matcher, service routing, confidence, timeout, and voice pipeline tests pass. Physical microphone/recognizer testing remains outstanding. |
| #262 | Folder/provider options provide readable names when the shared ComboBox selection template falls back to string rendering. | #269/#270. Label regressions pass; interactive Chat verification remains outstanding. |
| #263 | Each window reserves a toolbar above its original content. The existing anchored popup remains the Emergency Stop action; translated labels wrap within the window and determine reserved height. | #269/#271. Existing move/resize/lockdown/cleanup tests and the new four-language non-overlap test pass. Multi-monitor/DPI pointer testing remains outstanding. |
| #264 | The dashboard handles Enter in the preview event before multiline TextBox handling. Accepted text clears immediately; later edits survive while the response is pending. Shift+Enter retains multiline editing. | #269. Companion submission regressions and Release compilation pass. Actual dashboard Enter/Send/Shift+Enter interaction remains outstanding. |
| #265 | AI Engine embeds provider/key/model controls. The separate Models panel/tile is removed; old Models pins/routes map to AI Engine. Setup shares the editor while retaining profile/password/recovery controls. | #270. Persistence, legacy-pin, navigation, and construction/build checks pass. Interactive save/reopen remains outstanding. |
| #266 | The language picker is in System and the welcome screen before provider setup. It updates localization immediately, persists existing configurations, and restarts active recognition when needed. | #270. Setup-flow placement and localization tests pass; complete first-run interaction remains outstanding. |
| #267 | Tools embeds `CapabilitiesView`; the compatibility window uses the same controls. The original permission persistence, camera, image, and MCP operations are retained. | #270. Embedded-view/navigation checks and Release compilation pass. Actual device/MCP operations remain outstanding. |
| #268 | Shared surfaces and controls consume dynamic theme brushes. Frozen/translucent resources and color resources update; fixed dropdown colors are removed, and accent buttons retain readable foregrounds. | #271. Loaded WPF frozen-resource/theme test, theme/contrast tests, and menu rendering harness pass. Full multi-window/DPI inspection remains outstanding. |
| #204 | Removed the welcome screen's simulated installation countdown, updated settings guidance, reviewed the final diff, and ran Windows Release/test/presentation checks. | All PRs. Automated validation passes; the manual demo matrix below remains open. |

## Automated evidence

- Linux SDK 10.0.401: restore and cross-targeted Release application/test-project
  builds pass using `-p:EnableWindowsTargeting=true`, with zero warnings/errors.
- A local `dotnet test --project ...` attempt ran **zero tests** because Linux
  lacks `Microsoft.WindowsDesktop.App`; it is not counted as a passing run.
- [#269 Windows CI](https://github.com/Sktler/Aurora-os/actions/runs/42128379675):
  Release build and 240 tests pass.
- [#270 Windows CI](https://github.com/Sktler/Aurora-os/actions/runs/42129237051):
  Release build and 243 tests pass.
- [#271 Windows CI](https://github.com/Sktler/Aurora-os/actions/runs/42130177707):
  Release build passes with zero warnings/errors; **248 tests pass, zero failed
  or skipped**; the WPF menu harness reports **zero failed checks**. PNGs are
  retained in the `demo-presentation-checks` workflow artifact.
- [#272 Windows CI](https://github.com/Sktler/Aurora-os/actions/runs/42131119098)
  at `c1bb65d`: Release build passes with zero warnings/errors; **250 tests pass,
  zero failed or skipped**, including post-tool fallback and live connection-label
  regressions. The WPF menu harness again reports **zero failed checks**.
  The following report-only commit does not change application or test code.

Tests which write credentials/settings use `AppSettingsTestScope`; provider
requests use fake HTTP handlers and keys. No personal settings, credentials,
or conversation databases were used for validation.

## Local Windows verification on November 12, 2026

Verified at `1fb644b` with the existing local camera patch. Visual Studio MCP
built the original solution in Release with zero build errors or warnings and
ran **267 tests, all passed, zero skipped**. The separate WPF menu harness
also reported zero failed checks.

A disposable verification host loaded the real WPF resources and windows,
with isolated settings, an in-memory credential store, and a fake chat provider.
The following runtime checks passed.

- Setup cancel/retry and saving/reloading a synthetic profile, password verifier,
  and seeded recovery code.
- Dashboard Enter and Send submission, immediate clearing, busy/blank input,
  and preserving text typed while a response is pending.
- Settings, Chat, and Profiles construction across four themes and four languages.
  This construction result does not establish complete translation coverage.
- Denied Aurora location consent returning no location.
- Aurora's MCP client connecting to a disposable local stdio server, discovering
  its test tool, disconnecting, and removing the connection.
- One real webcam through discovery, permission check, initialization, preview,
  concurrent refresh/permission checks, first JPEG capture, revocation, and
  disposal during a pending scan. The captured test photo was checked and deleted.

The pass found three remaining presentation/input defects.

- The welcome window and language picker have inadequate contrast on a white
  background.
- Continue and some Settings/Profiles labels remain English after changing
  the interface language.
- The dashboard textbox has `AcceptsReturn=false`. Its WPF line-break command
  is disabled, so the required Shift+Enter multiline behavior remains incomplete.

These runtime checks use WPF routed events/commands and service calls. The
physical keyboard/pointer, OS permission combinations, spoken microphone,
live-account, second-camera, multi-monitor, and display-scaling cases below
remain open. Full first-run process restart and recovery-code generation were
also outside the runtime assertions.

One initial fallback debugger launch briefly ran ordinary Aurora startup
against the existing profile before it was stopped. No permission prompt was
answered. Subsequent runtime assertions used the isolated host. The detailed
local report and renders are under `artifacts/assessments/2026-11-12-demo-mcp-checklist.md`
and `artifacts/demo-mcp-20261112/`.

## Outstanding Windows demo matrix and blockers

The original verification workspace was Linux. Windows CI exercises isolated
WPF windows and rendering. The full flows below still need interactive
verification with a disposable Aurora profile and the listed prerequisites.

| Flow | Required exercise | Environment needed |
| --- | --- | --- |
| First run | Select each language before setup; create password/recovery code; cancel/retry; choose a configured provider and save. Confirm reopening does not lose settings. | Windows desktop with isolated Aurora AppData/profile |
| Location | Aurora Deny with Windows Allow; Aurora Allow with Windows Deny; both Allow; revoke while a request is pending; confirm unavailable state and no late location/weather display. | Windows location privacy controls, live NWS access |
| Provider/model | Switch rapidly between saved providers; use a custom model; invalid key; quota limit; retired Gemini model; default unavailable; switch during loading. | Test accounts/keys for selected providers; do not use production secrets |
| Voice | Inline wake phrase plus a long open-ended request; standalone wake phrase then dictation; manual listening; low confidence; mic denied/revoked; selected microphone and recognition language. | Windows speech packs and physical input device |
| Composer/Chat | Enter submits, Shift+Enter adds a line, Send clears accepted text immediately, text typed while waiting survives; blank/busy prompts remain safe; verify actual folder/provider selection text. | Interactive Windows session |
| Settings/Tools | Save provider/model without a restart; reopen settings; old Models pin; change System language; toggle capability consent; connect/discover/disconnect a local test MCP server. | Windows desktop and explicitly permitted test MCP server |
| Camera | Save Camera off and verify that Tools and companion camera commands cannot open the device. Save Camera on, refresh the list, select and initialize a camera, and verify the first photo. With preview running, refresh and check permission. Save Camera off during preview and confirm the device is released. Reopen Aurora and close it during a camera scan. Repeat selection with a second camera if available. | Windows desktop with at least one webcam and approved Windows camera access |
| Themes/Profiles | Open Main, Chat, Settings, Profiles, recovery dialogs, menus, and dropdowns together. Cycle all themes/languages; verify contrast, focus, wrapped names/actions, scrolling, and safety controls. | Windows at 100%, 150%, and 200% scale; small and large displays |
| Safety | Move/resize/minimize/restore each window; test reserved Emergency Stop hit area; stop a pending provider request; confirm lockdown and no tool replay. Revoke only disposable test keys. | Interactive Windows desktop; disposable provider configuration |

See [settings behavior](settings.md) and [layout investigation](layout-customization-recommendations.md).
