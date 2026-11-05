# Roadmap and task boundaries

## Phase 0: agent-readiness (**current**, implementation prohibited)

- [x] Central AGENTS.md instructions and agent-specific entry points.
- [x] README with current status and document index.
- [x] Product, architecture, security, and decision notes.
- [x] PR and issue templates.
- [ ] Owner approves initial Windows desktop technical spike and final stack decisions.

**Gate:** Do not move to Phase 1 solely because this roadmap exists. An explicit scoped task is required.

## Phase 1: desktop placement spike (future)

Test transparent native desktop-host placement on Windows 11, wallpaper co-existence, click behavior, z-order, Explorer restarts, virtual desktops, and multi-monitor handling. Record OS build and results. No locks or credential features.

**Exit criteria:** reproducible test results, fallback path, known limits, safe close/exit.

## Phase 2: minimal home MVP (future)

One draggable clock widget with persisted layout, launcher placeholder only if requested, theme boundaries, manual startup behavior. Avoid auto-start until explicitly requested.

**Exit criteria:** normal windows remain usable, wallpaper visible, layout persists, minimal performance overhead.

## Phase 3: home functionality (future)

Widget SDK, launcher, themes, optional auto-start, system events, accessibility and performance improvements.

## Phase 4: Stage 1 privacy-lock research (future)

State coordinator, independent application credentials, and safe/native-lock fallback. Explicitly state that Stage 1 is a deterrent, not Windows session security.

## Phase 5: optional deeper Windows integration (future)

Assess interception frameworks in a VM; research supported credential-provider integration separately. Preserve native authentication and a tested recovery method. No unsupported security bypasses.

## Suggested first task for a future agent

'Investigate and document Windows 11 native desktop layering options for WinCC without implementing the lock screen.' Require a scoped issue, a test plan, and owner approval before making an executable prototype.
