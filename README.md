# Typing Watch

Windows desktop typing-time tracker project.

## Current features in the source
- Floating always-on-top watch
- Hindi and English typing-time totals
- Overall typing time and 100-hour target
- Live WPM meter and configurable warning/beep
- Data persistence across restarts
- Online user count and announcement from the existing Google Apps Script backend
- Settings dashboard and license/payment interface
- GitHub release update-checking support configured for this repository

## Owner
OWNER: ASHISH FROM FARRUKHABAD

## Build safely
1. Download the source ZIP from the repository or use the configured-source ZIP provided with this project.
2. Extract it to a folder on the Windows computer.
3. Run `Build_TypingWatch.bat`.
4. Check `Build_Log.txt`. Only if it says `BUILD SUCCESSFUL`, run the generated `TypingWatch.exe`.
5. Test startup, saved typing time, WPM/beep, Hindi/English totals, online count, and announcement before replacing any existing working build.

The source ZIP is not a precompiled EXE. The build and runtime have not been verified in this repository environment, so do not treat a successful upload as proof that the app compiles or runs.

## Update system
The app checks `version.json` in `typingMonery-cloud/typingWatch`. When a newer version is available, it downloads the repository EXE, verifies that it is a Windows executable, then replaces the running EXE after closing it and restarts TypingWatch. Typing totals and settings remain in the existing AppData folder.

To publish an update, build the source on Windows using the repository workflow, verify the EXE artifact, then update `version.json` to the same version and publish the built `TypingWatch.exe` on the main branch. Do not change the Google Apps Script URL or existing data files.
