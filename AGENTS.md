# WinCC Agent Instructions

**This file is the canonical entry point for AI agents.** Treat the implementation specification and test contracts as authoritative for technical details.

## Scope and current status

**WinCC = Windows Command Center**, a customizable **Windows 11 desktop home screen**, independent of Wallpaper Engine. Features eventually include home widgets, launcher, saved layouts, settings and desktop resilience.

**ABSOLUTE SCOPE EXCLUSION:** No lock screen, authentication, credentials, login UI, biometrics, Winlogon, native Windows sign-in modifications, Win+L interception or any such work in WinCC. That is a different repository, not a later WinCC phase.

**PHASE 0 ONLY RIGHT NOW.** The repository contains planning documentation, not an executable product. Do not generate source code, scaffold an app, create an installer, commit assets or modify the Windows desktop without a specifically authorized task. A design spec is **not** an implementation request.

## Mandatory reading order

1. `README.md` — project status and scope.
2. `docs/PRODUCT.md` — product intent and hard exclusions.
3. `docs/IMPLEMENTATION.md` — detailed component plan, system behavior, dependencies and gate-by-gate work.
4. `docs/CONTRACTS.md` — planned state, coordinates and host-UI messages.
5. `docs/TEST-MATRIX.md` — required Windows 11 manual evidence and failure tests.
6. `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`, `docs/SECURITY.md`, and ADRs — context, task sequencing and recovery.

If a specific issue conflicts with a security/scope exclusion, stop and flag it. If a technical design conflicts with Windows 11 observed behavior, record a failing test and propose an ADR, not an undocumented workaround.

## Exact execution workflow for authorized future tasks

1. **Establish phase/gate:** identify G1/G2/etc. and specific test IDs from TEST-MATRIX.md. Verify explicit owner authorization.
2. **Inspect state:** look for changed branch head, active PRs, decisions, existing implementation and any other agent's work.
3. **Plan:** name exact source files, APIs, public interfaces, expected Windows behavior, error/fallback and how to validate each requirement.
4. **Implement minimal slice:** one component or experiment per reviewable PR. Never quietly jump to later gates.
5. **Verify:** run real available tests and record commands/results. For native Windows behavior, state OS build and exact manual repro. If unavailable, mark `NOT RUN`, not passed.
6. **Report:** scope, changed files, expected vs observed behavior, test IDs, known limitations, compatibility risks, recovery, rollback.
7. **Maintain docs:** update spec or add a new ADR when a proposed choice becomes accepted. Do not silently mutate requirements to fit code.

## Invariants

- Normal Windows Explorer/taskbar/Start and app foreground window behavior must remain intact.
- Wallpaper Engine must continue to run and manage wallpaper **independently**. WinCC must not inject into it.
- Do not build a permanent topmost overlay that blocks ordinary applications or desktop controls.
- Desktop window techniques using Progman/WorkerW are undocumented and **experiments only** until proven on specified Windows builds.
- Do not assume HTML transparency solves native host compositing or input pass-through. WPF/WebView2 `HwndHost` airspace is a known issue; compare `WebView2CompositionControl` and other renderers at G1.
- Never assume Windows virtual desktop origin is (0,0); support negative monitor positions and per-monitor DPI.
- Keep native Win32 interop in a small separate boundary, not inside React widget components.
- App must have reliable exit/fallback **even when WebView2 is broken**; test process kill/Explorer restart.
- No privileged services, shell replacement, process injection, driver, remote telemetry, auto-start or secret storage unless separately justified and authorized.
- The exact implementation stack and package versions are not finalized. Record the choice after the G1 spike.

## PR review minimum

PRs must name phase, acceptance test IDs, test environment, actual tests and results, untested assumptions, rollback, and whether docs/contracts changed. See `.github/pull_request_template.md`.

### Decision priority

Safety and scope exclusions > explicit owner task > approved ADRs > implementation contracts/specs > candidate architecture. Report conflicts openly instead of guessing.
