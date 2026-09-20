# Changelog

## v0.5.0

- Added Simplified Chinese and English UI.
- Added System default / 简体中文 / English language setting.
- Added customizable global hotkeys for Capture, Undo, and Finish.
- Hotkeys are applied immediately and persist in `%APPDATA%\MultiShot\settings.ini`.
- Added conflict rollback: if a new hotkey is occupied, previous hotkeys remain active.
- Added Settings entry to the controller and tray menu.
- Added GitHub-ready bilingual documentation and Windows CI build workflow.

## v0.4

- Added current-group counter inside the capture overlay.
- Added pixel magnifier and pointer coordinates for free-region capture.
- Replaced repeated controller popups with a lightweight capture-count toast.
- Prevented MultiShot's own toast from leaking into the next screenshot.
- Avoided creating empty temporary folders when a capture is cancelled.

## v0.3.1

- Fixed stale/ghost screenshot counts after temporary files were cleaned up.

## v0.3

- Added single-instance protection.
- Added safer temporary screenshot cleanup.
- Added tray pending-count status.

## v0.2

- Added top-level window snapping.
- Added manual free-region fallback.
- Added undo-last capture.

## v0.1

- Initial proof of concept: capture multiple screenshots and paste the whole group at once.
