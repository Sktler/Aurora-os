# Project planning hub

The old “known follow-ups” list has been replaced with a planning set that reflects the current `master` branch and the expanded feature backlog.

## Planning docs

- [Project status](project-status.md) — what exists now and the largest current gaps.
- [Next steps](next-steps.md) — a practical dependency-aware build order.
- [Roadmap](roadmap.md) — broad development phases without release-date promises.
- [Feature backlog](feature-backlog.md) — all newly created feature issues (#96–#170).

## Where the original follow-ups went

| Original follow-up | Current planning status |
| --- | --- |
| Background task engine | Tracked by [#141](https://github.com/Sktler/Aurora-os/issues/141), with scheduling/workflow follow-ons #142–#146. |
| Status tray / activity feed | Expanded into [#145](https://github.com/Sktler/Aurora-os/issues/145). |
| Image generation surfaced in chat | Tracked by [#152](https://github.com/Sktler/Aurora-os/issues/152); editing/variations are #153. |
| Free-tier/model rate limits | Addressed through budgets [#104](https://github.com/Sktler/Aurora-os/issues/104), provider health/failover [#105](https://github.com/Sktler/Aurora-os/issues/105), routing [#106](https://github.com/Sktler/Aurora-os/issues/106), and local models [#156](https://github.com/Sktler/Aurora-os/issues/156). |
| Sift does not read Gmail/Drive | Email is [#124](https://github.com/Sktler/Aurora-os/issues/124); cloud files are [#130](https://github.com/Sktler/Aurora-os/issues/130). |
| Only plain-text files are readable | Rich document support is [#128](https://github.com/Sktler/Aurora-os/issues/128); safe creation is [#127](https://github.com/Sktler/Aurora-os/issues/127). |
| Alexa cannot be a one-click connection | Still a platform constraint, not a missing Aurora implementation. Keep it documented rather than pretending a token-only integration is possible. |
| Only Spotify has a first-class music integration | The current code also has a Windows media-control service. Additional provider-specific music work is not part of this feature batch. |
| Phone/mobile version | Historical Android issues exist, but the current `master` tree is Windows-only. A future mobile effort should be planned as its own platform track. |

## Requested ideas included

The new backlog explicitly includes:

- Provider/model emergency kill switch — [#96](https://github.com/Sktler/Aurora-os/issues/96)
- Emotional AI response cues — [#108](https://github.com/Sktler/Aurora-os/issues/108)
- Dashboard layout options — [#109](https://github.com/Sktler/Aurora-os/issues/109)
- Read/send email — [#124](https://github.com/Sktler/Aurora-os/issues/124)
- Create documents/spreadsheets/files — [#127](https://github.com/Sktler/Aurora-os/issues/127)
- Default app/editor integration — [#129](https://github.com/Sktler/Aurora-os/issues/129)
