# 0.1.0 Demo implementation and verification

Baseline: `fc07b60`. Scope excludes packaging/distribution (#210).

Existing work reviewed: #250 (settings navigation), #251 and #259 (voice),
#254 (Emergency Stop anchoring), #255 (localized dialog layout), #256 (test
storage isolation), #257–258 (profile menu and shared menu styling). No open
pull requests were returned by the repository search on 2026-11-05.

Dependency order and review groups:

1. Privacy and conversation reliability: #216, #260–264.
2. Settings and setup: #237, #265–267.
3. Presentation and localization: #238, #241–242, #268.
4. Layout recommendations and final regression: #248, #204.

| Issue | Remaining implementation | Verification/status |
| --- | --- | --- |
| #216 | Persist Aurora's location consent and honor it before the Windows probe | Pending |
| #237 | Load models on provider selection; reject stale responses | Pending |
| #238 | Wider profile window and stretchable rows | Pending |
| #241 | Refresh visible static and computed localized text | Pending |
| #242 | Audit and extend translated UI coverage with English fallback | Pending |
| #248 | Document concrete layout options and implementation priorities | Pending |
| #260 | Actionable provider errors and bounded safe model recovery | Pending |
| #261 | Preserve dictation following a wake phrase | Pending |
| #262 | Render folder/provider names in selected combo items | Pending |
| #263 | Reserve space for Emergency Stop in each window | Pending |
| #264 | Preview Enter and clear accepted composer text immediately | Pending |
| #265 | Embed provider/key/model controls in AI Engine | Pending |
| #266 | Language in System and before provider setup | Pending |
| #267 | Reuse capabilities controls inside Tools | Pending |
| #268 | Dynamic shared brushes and theme-aware controls | Pending |
| #204 | Final build, tests, and demo-flow regression matrix | Pending |

Environment: Linux cloud host; .NET SDK 10.0.401 installed under `/tmp` for
cross-targeted Release compilation. Windows speech, credential vault, location,
WPF rendering/input, and physical device checks require Windows and remain
outstanding until exercised. Automated tests must use isolated settings storage.
