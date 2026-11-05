# WinCC data and interface contracts (proposed)

> Schema-1 layout and transaction contracts are implemented as a candidate after owner authorization. The WebView message protocol below remains a planning proposal: the current native WPF renderer calls typed local methods and exposes no executable bridge. See [ADR 0003](decisions/0003-native-implementation-candidate.md) and [validation](VALIDATION.md).

## 0. Canvas editing authority

See [CANVAS-EDITOR.md](CANVAS-EDITOR.md). Geometry, z-order and widget collection changes are permitted **only in Edit Mode**. Normal Mode is the default on every launch. Edit Mode holds an in-memory **draft** and a snapshot of the last durable layout. **Done** validates and saves the entire draft atomically; **Cancel** discards it. Do not persist each drag or pointer movement. Notes text and other functional widget data are stored separately from layout and remain editable in Normal Mode.

The JSON example below is an illustrative saved layout, **not** a forced default. An empty `widgets: []` array is valid. The earlier two-column arrangement is only an optional preset.

## 1. Layout file

Path proposal: `%LOCALAPPDATA%\WinCC\layout.json`

Illustrative JSON (not a fixture or a real user layout):

```json
{
  "schemaVersion": 1,
  "activeProfileId": "default",
  "profiles": [
    {
      "id": "default",
      "name": "Default",
      "widgets": [
        {
          "id": "clock-1",
          "type": "clock",
          "monitorKey": "primary",
          "rectDip": { "x": 32, "y": 32, "width": 260, "height": 120 },
          "zIndex": 0,
          "settings": { "hourFormat": "system" }
        }
      ]
    }
  ]
}
```

### Rules

- `schemaVersion`: integer, currently proposed `1`; reject unsupported major versions with safe default and preserve the original file.
- `activeProfileId`, profile `id`, widget `id`: unique bounded strings; IDs are not filenames or shell command targets.
- `type`: explicit allowlisted widget type. G2 allowlist: `clock`; later catalog: `calendar`, `spotify`, `system`, `weather`, `notes`, `ai-island`.
- `monitorKey`: logical persisted monitor identity. The placeholder `primary` resolves on each start to the current primary display. More stable identities for secondary screens require an accepted monitor-ID strategy; never persist `HMONITOR`.
- `rectDip`: monitor-local logical DIPs (96 units/inch). Not desktop-absolute physical pixels. `x`, `y`, `width`, `height` must be finite, bounded numbers; `width > 0`, `height > 0`. Clip/relocate to usable display area at restore.
- `zIndex`: only orders widgets relative to other **WinCC widgets**, never global Windows HWND z-order. Intentional overlaps are permitted, and Edit Mode must provide a way to select obscured widgets.
- `settings`: per-widget schema, validated by widget type. For clock, `hourFormat` is `system`, `12`, or `24`.
- Reject duplicate widget IDs, unrecognized commands, unexpected types and oversized file content.
- Define an upper file-size threshold in the implementation ADR; avoid silently allowing unbounded JSON.

## 2. Coordinate and monitor contract

Monitor-local DIPs are the **storage format**. The host's monitor service provides:
- `monitorKey` (stable logical identity with fallback rules),
- `boundsPx` and `workAreaPx` (physical virtual desktop coordinates),
- `dpiX`/`dpiY`, scale factor and primary flag,
- a display-topology generation ID for invalidation.

The frontend receives monitor-local bounds in logical DIPs. Negative **virtual desktop** coordinates must never be clipped before subtracting the monitor origin. The host is authoritative for coordinate conversion and validation; frontend preview coordinates are advisory only.

On lost monitor: remap widgets to primary, retain original monitor association in recoverable layout metadata if feasible, and prevent offscreen widgets. On restore: reevaluate placement and clamp to current work area. Never assume “DISPLAY1” is permanent identity.

## 3. Host/frontend message protocol

Use a narrow, typed local WebView2 message channel, not a general native-code execution bridge. All messages are JSON objects, with:
- `protocolVersion`: integer 1
- `id`: request correlation ID, bounded string
- `type`: allowlisted event name
- `payload`: type-validated body

Example, conceptual UI → Host committed drag:

```json
{
  "protocolVersion": 1,
  "id": "req-104",
  "type": "widget.position.commit",
  "payload": {
    "widgetId": "clock-1",
    "monitorKey": "primary",
    "rectDip": { "x": 100, "y": 80, "width": 260, "height": 120 }
  }
}
```

Example, conceptual Host → UI acknowledgement:

```json
{
  "protocolVersion": 1,
  "id": "req-104",
  "type": "widget.position.result",
  "payload": {
    "ok": true,
    "committedRectDip": { "x": 100, "y": 80, "width": 260, "height": 120 }
  }
}
```

### Proposed G2 mode-aware allowlist

UI → Host: `app.bootstrap.request`, `editor.enter.request`, `editor.draft.update`, `editor.done.request`, `editor.cancel.request`, `app.exit.request`.

Host → UI: `app.bootstrap.result`, `editor.mode.changed`, `editor.draft.result`, `editor.save.result`, `monitors.changed`, `app.error`.

- `editor.enter.request`: explicit user-initiated entry; host snapshots the current durable layout and creates a draft.
- `editor.draft.update`: add/remove/move/resize/z-order mutation with validated widget IDs, type constraints and monitor-local geometry; host rejects outside Edit Mode. Pointer moves can preview locally without sending one native request per pixel.
- `editor.done.request`: validate the **entire** draft, persist it durably, and switch to Normal Mode only on success.
- `editor.cancel.request`: discard draft and return to Normal Mode; require confirmation when unsaved changes would be lost.
- `editor.save.result`: report **disk persistence** success/failure, not merely in-memory acceptance.

The previous `widget.position.commit` example is a **legacy conceptual geometry payload**, not a direct Normal Mode API. Any geometry mutation must be wrapped in an Edit Mode transaction and must not be independently persisted during a drag.

All message types are planning proposals and require a protocol ADR before implementation.

Do **not** add a generic `execute`, `powershell`, `launch` or `invokeNative` method. Future launcher commands require their own validated, specific protocol and authorization model.

### Reliability and validation

- Check protocol version, ID length, type, required payload fields, numeric finiteness, permitted widget IDs, active profile and user-owned display bounds.
- Bound serialized payload length; do not blindly deserialize untrusted UI input to arbitrary .NET types.
- Acknowledge commit **only after in-memory state has accepted it**; persist asynchronously and report separate save failure diagnostics. Do not claim disk persistence merely because a preview changed.
- No WebView message may directly set native HWND styles, install hooks, write arbitrary files or spawn unvalidated executables.
- UI should use the app's bundled local assets; no remote scripts, arbitrary navigation or injected third-party widgets by default.
- Treat `app.exit.request` as a user-interaction command with a known safe exit; prevent accidental exit from untrusted page navigation.

## 4. Persistence expectations

- Persist layout on successful **Done** only, not on every pointer movement. Write to a same-volume temp file and perform a replace/move using robust Windows filesystem semantics.
- Keep backup or last-known-good data; validate on load.
- Handle shutdown/force-crash recovery, inaccessible configuration folder, malformed JSON and future schema versions. An interrupted Edit Mode session restores the previous durable layout and launches in Normal Mode.
- Do not silently overwrite incompatible layout formats.

## 5. Diagnostics event taxonomy

Initial proposed events (no telemetry):
- `host.started`, `host.exiting`, `host.crashed`
- `desktop.strategy.selected`, `desktop.strategy.failed`
- `display.topology.changed`, `display.dpi.changed`
- `shell.recreated`, `renderer.failed`, `renderer.recovered`
- `layout.loaded`, `layout.validation_failed`, `layout.saved`, `layout.save_failed`

Log timestamp, event name, OS build and **non-sensitive** reason codes; do not log full local paths, widget text, secrets or user content without explicit reason. Logging format and rotation must be defined before G2 release.

## 6. Future source interfaces (illustrative names only)

```text
IDesktopPlacementStrategy
  Probe() -> PlacementProbeResult
  Attach(windowHandle, monitor) -> PlacementResult
  Refresh(displayGeneration) -> PlacementResult
  Detach() -> void

IMonitorTopology
  Enumerate() -> immutable monitor snapshot
  WatchChanges(callback) -> disposable subscription

ILayoutRepository
  Load() -> validated layout or recovery status
  BeginDraft() -> editable snapshot of durable state
  ApplyDraftMutation(change) -> validated draft result
  SaveDraftAndExitEditMode() -> durable save result
  DiscardDraft() -> restore prior layout

IWidgetCatalog
  Resolve(widgetType, schemaVersion) -> known widget schema

IHostBridge
  ReceiveValidated(message) -> typed action/result
  SendTypedEvent(event) -> delivery result
```

These are **contracts to review**, not existing namespaces or a request to generate code now.
