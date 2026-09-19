# Getting it running

1. **Install prerequisites**
   - Visual Studio 2022 (Community is fine), with the **.NET desktop
     development** workload checked during install.
   - .NET 8 SDK (Visual Studio installer will offer this automatically).

2. **Open it**
   - Double-click `Aurora.sln`, or open Visual Studio → *Open a project or
     solution* → select `Aurora.sln`.
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
