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
The app checks the latest GitHub Release for `typingMonery-cloud/typingWatch`. If it detects a newer version, it offers to open the GitHub release page. It does not silently download or install updates.

To publish a release, first build and test the exact source on Windows, increment `AppVersion`, package the tested output, and attach the package to a GitHub Release using a matching version tag. Do not publish an untested package as a working release.
