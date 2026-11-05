# WinCC Agent Instructions

This is the **canonical instruction file** for AI agents contributing to WinCC.

## Scope and authorization

WinCC is a **Windows 11 home screen / Windows Command Center only**. Its scope includes desktop UI, widget runtime, launcher, layout customization, Wallpaper Engine coexistence, and supporting Windows integration.

**Do not add lock-screen, login, credential, biometric, authentication, Winlogon, or security-shortcut interception functionality to this repository.** Those are a separate potential project and are not part of WinCC.

**Current phase: Phase 0, repository planning only.** Do not implement or scaffold the executable product, runtime, or installer until the owner explicitly authorizes a bounded task. A roadmap entry is not implicit authorization.

## Before changes

1. Read README.md and docs/PRODUCT.md.
2. Consult docs/ARCHITECTURE.md, docs/ROADMAP.md, docs/SECURITY.md and relevant ADRs.
3. Inspect current files/issues/PRs; don't assume no other agent has made changes.
4. State task boundaries, acceptance criteria, and assumptions before making significant changes.
5. Treat stack choices as proposals, not settled implementation requirements.

## Engineering expectations

- Small, reversible, reviewable changes; one distinct task per PR.
- Keep ordinary Explorer, taskbar, and Windows interaction functional.
- Keep Wallpaper Engine independent, with no private API coupling or modification of its runtime.
- UI and native Windows interop must be modular; avoid embedding undocumented shell assumptions in widget code.
- Treat WorkerW/Progman positioning, z-order, clicks, virtual desktops, DPI, display changes, and Explorer restarts as unverified until tested on Windows 11.
- No driver, process injection, shell patch, persistence, elevated helper, or privileged service unless separately scoped, justified, safely tested, and authorized.
- No API keys, secrets, local identifiers, personal files, or machine configuration in commits.
- Avoid new dependencies or automation with privileged access unless justified in the task.
- Do not silently enforce a particular visual design or rewrite approved product goals.

## Verification and reporting

- If application work is authorized, include Windows 11 build and manual verification instructions for native behavior.
- Test normal launch/exit, Explorer restart, wallpaper coexistence, monitor changes, and ordinary app window behavior where relevant.
- Never claim Windows tests passed if the environment cannot run them.
- Keep a nonintrusive, working exit/recovery mechanism before adding startup behavior.
- Explain code changed, tests run, unverified assumptions, risks, and rollback in every PR.
- Update decisions/documentation when choices become final.

## Source documents

- docs/PRODUCT.md: owner goals and explicit exclusions
- docs/ARCHITECTURE.md: candidate home-screen architecture
- docs/ROADMAP.md: progression and gates
- docs/SECURITY.md: host stability and data safeguards
- docs/decisions/: architecture records

If a request conflicts with the home-screen-only scope, do not implement it in WinCC; the owner can handle it in a different repository.
