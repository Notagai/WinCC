# WinCC Agent Instructions

This is the **source of truth for AI coding agents** working in this repository.

## Current authorization boundary

**Phase 0: planning and agent setup only.** Do not create application code, runtime scaffolding, installers, keyboard hooks, drivers, credential providers, or lock-screen implementations until the owner explicitly authorizes a bounded implementation task. An issue or PR description must make that scope clear. Do not infer authorization from the roadmap.

## Before touching files

1. Read README.md and docs/PRODUCT.md.
2. Read docs/ARCHITECTURE.md, docs/SECURITY.md, and docs/ROADMAP.md when relevant.
3. Check existing files, issues, branches, and open PRs; do not assume the repo is still empty.
4. State your intended scope and acceptance criteria; avoid unrelated rewrites.
5. Treat proposed stack choices as **proposals**, not fixed requirements.

## Product invariant

WinCC means **Windows Command Center**, targeting Windows 11. Its first goal is a home screen that visually overlays or coexists with Wallpaper Engine without integrating with or controlling Wallpaper Engine. Only later is a custom privacy-lock UI planned. Stage 1 is *not* a secure Windows session; Stage 2 always relies on Windows' native lock and sign-in boundary.

## Engineering rules

- Prefer small, reversible, reviewable changes. One task per branch/PR.
- Preserve ordinary Windows Explorer and the built-in Windows sign-in fallback.
- Never promise that Windows-reserved Win+L can be reliably intercepted.
- No bypasses of Windows authentication or suppression of secure OS controls.
- Avoid process injection, kernel drivers, shell patching, or Winlogon/credential-provider changes without explicit authorization, threat review, VM testing, and a recovery plan.
- Keep UI code separate from host/native interop. Keep credential handling separate from WebView/frontend code.
- Keep Wallpaper Engine an independent process. No undocumented coupling to its internals.
- Do not store secrets, API keys, passwords, recovery tokens, or biometrics in the repository or test fixtures.
- Do not add dependencies, workflows with privileged permissions, remote telemetry, or startup persistence without explaining why and gaining task-level authorization.
- Clearly identify unverified assumptions, especially Windows shell positioning, Explorer z-order, session notifications, and multi-monitor behavior.

## Quality bar (once implementation begins)

- Update docs when decisions or behavior change.
- Include a short manual Windows 11 verification plan with each Windows-dependent change.
- Cover normal startup, Explorer restart, display change, app crash, and native-lock fallback where relevant.
- Give real commands/results for tests you actually ran. If you cannot run Windows-dependent checks, say so; never mark them as passed.
- Prefer no-op behavior to unsafe behavior on errors; do not pretend a custom privacy screen is native security.
- Keep changes accessible and reversible. Provide a clean exit/uninstall route before introducing persistent shell changes.

## Output expected from an agent

Every PR should state: goal, files/components changed, assumptions, verification performed, untested areas, risks, and rollback path. Link to an issue if one exists. Do not silently change architectural decisions.

## Docs authority

- docs/PRODUCT.md: requirements and exclusions
- docs/ARCHITECTURE.md: proposed technical design
- docs/SECURITY.md: security boundaries (highest priority when decisions conflict)
- docs/ROADMAP.md: sequencing
- docs/decisions/: accepted/planned decisions and rationale

If requirements conflict, request owner clarification before implementing the disputed behavior; safe documentation clarification is allowed.
