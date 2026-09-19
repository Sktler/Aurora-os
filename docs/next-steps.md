# Next steps

This is a suggested engineering order based on dependencies in the current code. It is not a commitment to dates or releases.

## 1. Stabilize the existing app first

Before broad feature expansion:

- Review the existing bug queue and close or reproduce stale reports.
- Bring setup and architecture docs in line with the current .NET 10 codebase.
- Expand automated coverage around provider switching, permissions, file safety, tool routing, and startup.
- Keep optional integrations from blocking startup or core chat.

## 2. Build the safety/control foundation

These features reduce risk for everything that follows:

1. [#96 provider kill switch](https://github.com/Sktler/Aurora-os/issues/96)
2. [#97 high-impact action approval center](https://github.com/Sktler/Aurora-os/issues/97)
3. [#98 temporary permission grants](https://github.com/Sktler/Aurora-os/issues/98)
4. [#99 action audit log](https://github.com/Sktler/Aurora-os/issues/99)
5. [#103 Windows credential vault](https://github.com/Sktler/Aurora-os/issues/103)
6. [#101 per-companion capability permissions](https://github.com/Sktler/Aurora-os/issues/101)

These should land before Aurora gains broader write/send/automation powers.

## 3. Build the task backbone

The old follow-up list correctly identified background execution as a major missing layer.

1. [#141 background task engine](https://github.com/Sktler/Aurora-os/issues/141)
2. [#145 activity/status center](https://github.com/Sktler/Aurora-os/issues/145)
3. [#142 reminders and recurring schedules](https://github.com/Sktler/Aurora-os/issues/142)
4. [#143 event-triggered tasks](https://github.com/Sktler/Aurora-os/issues/143)
5. [#146 dry-run mode](https://github.com/Sktler/Aurora-os/issues/146)
6. [#144 workflow builder](https://github.com/Sktler/Aurora-os/issues/144)

## 4. Expand the highest-value productivity I/O

Once approvals and background execution are sound:

1. [#124 email read/send](https://github.com/Sktler/Aurora-os/issues/124)
2. [#127 file/document creation](https://github.com/Sktler/Aurora-os/issues/127)
3. [#128 PDF/DOCX/XLSX support](https://github.com/Sktler/Aurora-os/issues/128)
4. [#129 default app integration](https://github.com/Sktler/Aurora-os/issues/129)
5. [#132 diff-based file editing](https://github.com/Sktler/Aurora-os/issues/132)
6. [#125 calendar tools](https://github.com/Sktler/Aurora-os/issues/125)
7. [#130 cloud files](https://github.com/Sktler/Aurora-os/issues/130)

## 5. Improve the companion experience

The requested UX ideas fit naturally after the safety/task backbone:

- [#108 emotion-aware response cues](https://github.com/Sktler/Aurora-os/issues/108)
- [#109 dashboard layouts](https://github.com/Sktler/Aurora-os/issues/109)
- [#110 custom companion builder](https://github.com/Sktler/Aurora-os/issues/110)
- [#111 companion delegation](https://github.com/Sktler/Aurora-os/issues/111)
- [#112 project workspaces](https://github.com/Sktler/Aurora-os/issues/112)
- [#114 memory controls](https://github.com/Sktler/Aurora-os/issues/114)

## 6. Add multimodal, research, and local AI

- [#151 live camera vision](https://github.com/Sktler/Aurora-os/issues/151)
- [#152 inline image generation](https://github.com/Sktler/Aurora-os/issues/152)
- [#154 deep research](https://github.com/Sktler/Aurora-os/issues/154)
- [#156 Ollama/LM Studio local models](https://github.com/Sktler/Aurora-os/issues/156)
- [#136 screen understanding](https://github.com/Sktler/Aurora-os/issues/136)
- [#140 multimodal attachments](https://github.com/Sktler/Aurora-os/issues/140)

## 7. Then broaden ecosystem features

After the core execution model is stable, work through smart-home routines, app adapters, backups/sync, daily briefings, meetings, MCP management, and the remaining backlog in [feature-backlog.md](feature-backlog.md).
