# Jamendo music setup

Aurora now uses Jamendo as its in-app music provider. The integration supports catalog search, direct stream playback, pause/resume, next/previous, artwork metadata, and Aurora chat tools.

## 1. Create a Jamendo application

Open the Jamendo Developer Portal:

https://devportal.jamendo.com/

Create an application and copy its **Client ID**.

## 2. Add the Client ID to Aurora

Aurora stores local settings in:

`%AppData%\Aurora\settings.json`

Add or update:

```json
"JamendoClientId": "YOUR_CLIENT_ID",
"JamendoConnected": true
```

Do not commit the Client ID to this repository. The value belongs in the user's local `settings.json`.

## 3. What Aurora can do

The general Aurora/Scout music tools now use Jamendo:

- `jamendo_play` - search the Jamendo catalog and start a track
- `jamendo_now_playing` - report the current Jamendo track
- `jamendo_pause`
- `jamendo_resume`
- `jamendo_skip_next`
- `jamendo_skip_previous`

Jamendo's tracks API requires a Client ID and returns stream URLs through the `audio` field. Aurora requests MP3 VBR (`mp32`) streams. See the official API documentation for current catalog and licensing details.

## Important licensing note

Jamendo contains music under different licenses. Aurora should respect the license attached to each track and the terms of the Jamendo API/application plan, especially if Aurora is distributed commercially.
