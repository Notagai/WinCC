# Security boundaries and recovery policy

## Trust model

WinCC's custom privacy-lock screen (Stage 1) is **not** the secure Windows lock screen. Its presence does not establish that files, applications, or the signed-in session are protected. The native Windows lock (Stage 2) is the security boundary.

## Required design principles

- Windows controls real lock/unlock and credentials.
- Never disable built-in Windows credential providers or native recovery/sign-in paths.
- Do not suppress Windows lock mechanisms to give a false impression of security.
- Do not store any real credentials, biometric templates, Windows passwords, or recovery codes in repo, telemetry, screenshots, sample fixtures, or frontend.
- Run ordinary UI at normal user integrity, not administrator.
- Require explicit authorization and an isolated VM before native credential-provider, input-driver, injection, or invasive shell experiments.
- Keep credential verification out of WebView/React code.
- Fail closed where possible: if Stage 1 fails, request native locking; do not claim Stage 2 occurred until Windows confirms session state.
- Provide a documented escape/recovery path and test it before startup persistence, shell replacement, or other intrusive integration.

## Logging and local data

Keep log output free of secrets and credential material; use minimum diagnostic data. No analytics/network calls are part of the baseline. Store configuration locally only when a real application is authorized.

## Threat assumptions

Stage 1 is a *casual deterrent*. A person with access to the unlocked Windows session may be able to bypass ordinary application overlays. Never market it as protection against privileged users, malicious processes, or physical access attacks.

Security-sensitive feature proposals need a threat model, rollback procedure, and explicit approval before implementation.
