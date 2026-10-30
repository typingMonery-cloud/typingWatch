# Website Apps Manager setup

This adds remote app management for the TypingWatch customer website.

## 1. Add `backend/AppsManager.gs` to the same Google Apps Script project

Open the Apps Script project connected to the Typing Watch Google Sheet and paste the contents of `backend/AppsManager.gs` into a new `.gs` file.

The code creates an `Apps` sheet automatically with these columns:

`ID | Name | Platform | Description | DownloadUrl | Status | Icon | SortOrder | UpdatedAt`

## 2. Add the GET routes inside the existing `doGet(e)`

Before the final unknown-action response, add:

```js
if (action === 'apps') return appsPublic_();
if (action === 'admin') return adminGet_(e.parameter || {});
```

If the deployed backend already has the `admin` GET route, do not add it twice.

## 3. Add the admin operation inside the existing `admin_(b)`

Before its final unknown-operation response, add:

```js
if(op==='apps' || op==='apps_save' || op==='apps_delete') return appsAdmin_(b);
```

## 4. Deploy a new Web App version

Deploy the Apps Script Web App as the same `/exec` URL used by the TypingWatch app, Admin Panel and customer website.

Do not change or publish the ADMIN token/key.

## 5. Apps Manager

Open:

`https://typingmonery-cloud.github.io/typingWatch/admin-apps.html`

Enter the existing ADMIN KEY.

You can then:
- add Windows/Android/Web/Future apps
- edit name, platform, description, download URL, icon and order
- set Active, Coming Soon or Hidden
- delete an app

## 6. Public API

Customer website reads active apps from:

`?action=apps`

Hidden apps are not returned. Active/Coming Soon apps are returned and sorted by SortOrder.
