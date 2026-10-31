/*
 * TypingWatch Chat add-on for the newer TW backend.
 *
 * This file is safe to add as a separate .gs file in the existing Apps Script
 * project. It does not replace Control, Licenses, LiveUsers, Installations,
 * DownloadEvents, AdminAudit, or any existing functions.
 *
 * Add these two route cases to the existing doGet(e) switch:
 *   case "chat":
 *     result = typingWatchChatGet_();
 *     break;
 *
 * Add these two route cases to the existing doPost(e) switch:
 *   case "chatsend":
 *   case "chat_send":
 *     return json_(typingWatchChatSend_(data));
 *
 * The desktop app sends action=chatSend and reads action=chat.
 */

const TW_CHAT_SHEET_NAME = "Chat";
const TW_CHAT_HEADERS = ["Time", "UserID", "Message"];

function typingWatchChatSheet_() {
  const ss = getSS_();
  let sh = ss.getSheetByName(TW_CHAT_SHEET_NAME);
  if (!sh) {
    sh = ss.insertSheet(TW_CHAT_SHEET_NAME);
    sh.getRange(1, 1, 1, TW_CHAT_HEADERS.length).setValues([TW_CHAT_HEADERS]);
    sh.setFrozenRows(1);
  } else if (sh.getLastRow() === 0) {
    sh.getRange(1, 1, 1, TW_CHAT_HEADERS.length).setValues([TW_CHAT_HEADERS]);
    sh.setFrozenRows(1);
  }
  return sh;
}

function typingWatchChatGet_() {
  const sh = typingWatchChatSheet_();
  const rows = sh.getDataRange().getValues();
  const messages = [];
  const first = Math.max(1, rows.length - 100);

  for (let i = first; i < rows.length; i++) {
    const row = rows[i];
    if (!row[0] || !row[1] || !row[2]) continue;

    const time = row[0] instanceof Date
      ? row[0].toISOString()
      : String(row[0]);

    const name = String(row[1]).replace(/[\\r\\n|]+/g, " ").trim();
    const message = String(row[2]).replace(/[\\r\\n]+/g, " ").trim();

    if (!name || !message) continue;
    messages.push(time + "|||" + name + "|||" + message);
  }

  return {
    ok: true,
    success: true,
    messages: messages.join("\\n")
  };
}

function typingWatchChatSend_(data) {
  const name = String(data.userId || data.UserID || data.name || "")
    .replace(/[\\r\\n|]+/g, " ")
    .trim()
    .substring(0, 30);

  const message = String(data.message || data.text || "")
    .replace(/[\\r\\n]+/g, " ")
    .trim()
    .substring(0, 300);

  if (!name || !message) {
    return {
      ok: false,
      success: false,
      error: "Chat name and message are required."
    };
  }

  const lock = LockService.getScriptLock();
  lock.waitLock(10000);
  try {
    typingWatchChatSheet_().appendRow([new Date(), name, message]);
    SpreadsheetApp.flush();
  } finally {
    lock.releaseLock();
  }

  return {
    ok: true,
    success: true,
    sent: true
  };
}
