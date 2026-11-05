# WinCC operational safety

This document concerns **desktop reliability and local data** for a Windows 11 home-screen application, not operating-system authentication.

## Desktop stability

- WinCC should run under normal user permissions.
- Do not replace Explorer or modify the system shell by default.
- Always offer a usable exit/recovery path when introducing an overlay, window hosting or startup registration.
- Do not interfere with ordinary application interaction, taskbar controls or system keyboard shortcuts.
- Prefer a documented fallback window mode when undocumented desktop placement fails.
- Test Explorer restart, display hotplug, sleep/resume and fullscreen transitions on Windows 11 before declaring stability.

## Dependencies and permissions

- Wallpaper Engine is independent and optional.
- Avoid privileged services, kernel extensions, injection and hidden persistence.
- Any optional shell styling integration must be approved, isolated and removable.
- Do not introduce network communications, auto-updaters or telemetry without an approved requirement.

## User data

- Widget layout and settings should default to local storage.
- Never commit secrets, local user files or machine-specific identifiers.
- Minimize and redact diagnostic logs; do not upload them automatically.
- Provide safe config reset and migration processes when persistence is implemented.

## Development rules

No functionality is implemented during Phase 0. New behavior needs explicit scope, tests and a rollback path. Record assumptions and compatibility problems rather than claiming untested features work.
