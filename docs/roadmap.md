# Roadmap

Aurora has grown beyond the original v1 follow-up list. This roadmap groups the work by dependency rather than by calendar date.

## Phase A — reliability, permissions, and trust

Goal: make existing capabilities predictable before giving Aurora more authority.

Primary work:
- Provider kill switch, approvals, temporary grants, audit log, undo, secret storage.
- Provider health/fallback and spend controls.
- Broader automated tests and documentation cleanup.

Related issues: [#96–#105](https://github.com/Sktler/Aurora-os/issues/96).

## Phase B — asynchronous task platform

Goal: let companions do work without blocking the active chat.

Primary work:
- Background task engine.
- Unified activity center.
- Scheduling, reminders, event triggers, dry runs, and workflows.

Related issues: [#141–#146](https://github.com/Sktler/Aurora-os/issues/141).

## Phase C — productivity and file I/O

Goal: move Sift/Nova from mostly reading and advising into safe, reviewable work.

Primary work:
- Email, calendar, contacts.
- Create/edit rich files.
- Default-app handoff and app adapters.
- Cloud storage, notes, tasks, browser context, local semantic file search.

Related issues: [#124–#140](https://github.com/Sktler/Aurora-os/issues/124), [#165](https://github.com/Sktler/Aurora-os/issues/165), and [#166](https://github.com/Sktler/Aurora-os/issues/166).

## Phase D — companion and dashboard evolution

Goal: make the multi-companion design more useful and personal.

Primary work:
- Emotion/state cues and richer orb behavior.
- Dashboard layouts and detachable views.
- Custom companions, delegation, project spaces.
- Better memory controls, chat organization, accessibility, themes, localization.

Related issues: [#108–#123](https://github.com/Sktler/Aurora-os/issues/108).

## Phase E — multimodal, research, and local AI

Goal: use the camera/screen/image foundations already present and reduce dependence on one cloud provider.

Primary work:
- Screen and camera vision.
- OCR and multimodal attachments.
- Inline image generation and editing.
- Deep research with citations.
- Local model providers.
- MCP/plugin management.

Related issues: [#136–#140](https://github.com/Sktler/Aurora-os/issues/136) and [#151–#156](https://github.com/Sktler/Aurora-os/issues/151).

## Phase F — ambient assistant and ecosystem

Goal: make Aurora useful across the day and across connected systems.

Primary work:
- Daily briefings and meeting mode.
- Smart-home scenes, alerts, and energy views.
- PC health assistant.
- Backups and encrypted sync.
- Knowledge ingestion/graph.
- Release channels and diagnostics.

Related issues: [#157–#170](https://github.com/Sktler/Aurora-os/issues/157).

## Release rule of thumb

New capabilities that **write, send, execute, control devices, or run unattended** should depend on the permission/approval/audit foundation rather than each inventing its own safety UI.
