# Typing Watch

Windows desktop typing-time tracker project.

## Current features
- Floating always-on-top watch
- Hindi and English typing-time totals
- Overall typing time and 100-hour target
- Live WPM meter and configurable warning/beep
- Data persistence across restarts
- Online user count and announcement from the existing Google Apps Script backend
- Settings dashboard and license/payment interface
- GitHub release update-checking support (repository settings must be configured in the application source)

## Owner
OWNER: ASHISH FROM FARRUKHABAD

## Important
This repository is being set up for the Typing Watch project. Do not replace the working Windows build until a new build has been tested successfully. Release ZIPs and source files should be uploaded after verifying they match the currently working build.

## Update system
The desktop app checks the latest GitHub Release. To enable update checks, configure the repository owner and repository name in the C# source, build and test the app, then publish a GitHub Release with the version tag and packaged download attached.
