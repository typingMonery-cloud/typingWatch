# Typing Watch backend deployment

The source of truth for the server API is `backend/Code.gs`.

## One-time setup / update

1. Open the Google Apps Script project attached to the spreadsheet that stores the Typing Watch data.
2. Replace the editor contents with `backend/Code.gs` from this repository. Keep the existing spreadsheet and its data; do not delete the tabs.
3. Save the project.
4. Run `setupSheets` once from the editor. Approve the Google permissions prompt. The function creates only missing tabs/headers and fills in missing defaults; it does not clear existing rows.
5. Open **Project Settings → Script properties** and confirm that `ADMIN_TOKEN` exists. If the older project uses `ADMIN_KEY`, the backend accepts either property. Never put this token in a public file or share it in a public chat.
6. Click **Deploy → Manage deployments → Edit** on the existing Web App deployment, select **New version**, and deploy. Keep the same Web App deployment URL if possible so the EXE and Admin Panel continue to use it.
7. Open the deployment URL with `?action=health`. A successful response includes `"ok":true` and `"service":"Typing Watch"`.
8. Open `admin.html` from the repository's configured hosting URL and press **Test Server**, then refresh the dashboard. Enter the admin token only when performing a write operation.

## Supported routes

GET: `health`, `stats`, `config`, `licenses`, `downloads`, `chat`, `dashboard`.

POST: `heartbeat`, `download`, `chatSend`, `check_license`, and protected admin operations for announcements, version, online threshold, maintenance, settings, and license creation/status.

## Important

- The EXE currently reports heartbeat and reads `stats` / `config`; it reads its locally stored license using the existing package-key format. The backend's license table is an admin/server-side license registry and is not yet the EXE's authoritative online license validator.
- GitHub Actions validates the backend syntax and checks the app/admin/backend action contract. This does not publish a new Google Apps Script deployment automatically.
- Do not remove existing spreadsheet tabs or replace the EXE's local data directory during this update.
