const SHEETS = {
  LIVE_USERS: 'LiveUsers',
  CONTROL: 'Control',
  LICENSES: 'Licenses',
  DOWNLOADS: 'Downloads'
};

function getBook_() { return SpreadsheetApp.getActiveSpreadsheet(); }
function sheet_(name) { return getBook_().getSheetByName(name); }
function json_(obj) { return ContentService.createTextOutput(JSON.stringify(obj)).setMimeType(ContentService.MimeType.JSON); }

function setupSheets() {
  const ss = getBook_();
  const defs = [
    [SHEETS.LIVE_USERS, ['UserID','LastSeen','Typing','Language','WPM','Version']],
    [SHEETS.CONTROL, ['Key','Value']],
    [SHEETS.LICENSES, ['LicenseKey','Status','UserID','ActivatedAt','ExpiresAt']],
    [SHEETS.DOWNLOADS, ['Time','Version','Source','UserID']]
  ];
  defs.forEach(d => {
    let sh = ss.getSheetByName(d[0]);
    if (!sh) sh = ss.insertSheet(d[0]);
    if (sh.getLastRow() === 0) sh.getRange(1,1,1,d[1].length).setValues([d[1]]);
  });
  const control = ss.getSheetByName(SHEETS.CONTROL);
  const existing = control.getRange(1,1,Math.max(1,control.getLastRow()),2).getValues();
  const keys = existing.map(r => String(r[0]));
  const defaults = {
    OnlineThresholdMinutes: '5',
    AppVersion: '1.0.2',
    Information: 'Typing Watch is online.',
    DownloadCount: '0'
  };
  Object.keys(defaults).forEach(k => {
    if (keys.indexOf(k) < 0) control.appendRow([k, defaults[k]]);
  });
}

function doPost(e) {
  try {
    const body = e && e.postData && e.postData.contents ? JSON.parse(e.postData.contents) : {};
    const action = String(body.action || '').toLowerCase();
    if (action === 'heartbeat') return heartbeat_(body);
    if (action === 'download') return download_(body);
    if (action === 'admin') return admin_(body);
    return json_({ok:false,error:'Unknown action'});
  } catch (err) {
    return json_({ok:false,error:String(err)});
  }
}

function doGet(e) {
  try {
    const action = String((e && e.parameter && e.parameter.action) || 'stats').toLowerCase();
    if (action === 'stats') return stats_();
    if (action === 'config') return config_();
    if (action === 'licenses') return licenses_();
    if (action === 'downloads') return downloads_();
    return json_({ok:false,error:'Unknown action'});
  } catch (err) {
    return json_({ok:false,error:String(err)});
  }
}

function heartbeat_(b) {
  const sh = sheet_(SHEETS.LIVE_USERS);
  if (!sh) { setupSheets(); return heartbeat_(b); }
  const id = String(b.userId || '').trim();
  if (!id) return json_({ok:false,error:'Missing userId'});
  const rows = sh.getDataRange().getValues();
  let row = -1;
  for (let i=1;i<rows.length;i++) if (String(rows[i][0]) === id) { row=i+1; break; }
  const values = [id,new Date(),String(b.typing||'false'),String(b.language||''),Number(b.wpm||0),String(b.version||'')];
  if (row < 0) sh.appendRow(values); else sh.getRange(row,1,1,values.length).setValues([values]);
  return json_({ok:true});
}

function stats_() {
  const sh = sheet_(SHEETS.LIVE_USERS);
  if (!sh) { setupSheets(); return stats_(); }
  const rows = sh.getDataRange().getValues();
  const control = controlMap_();
  const mins = Number(control.OnlineThresholdMinutes || 5);
  const cutoff = new Date(Date.now() - mins*60000);
  const users = [];
  let typingNow = 0, hindi = 0, english = 0;
  for (let i=1;i<rows.length;i++) {
    const r=rows[i]; if (!r[0] || !(r[1] instanceof Date)) continue;
    if (r[1] < cutoff) continue;
    const u={userId:String(r[0]),lastSeen:r[1].toISOString(),typing:String(r[2]).toLowerCase()==='true',language:String(r[3]||''),wpm:Number(r[4]||0),version:String(r[5]||'')};
    users.push(u);
    if (u.typing) typingNow++;
    if (u.language.toLowerCase()==='hindi') hindi++;
    if (u.language.toLowerCase()==='english') english++;
  }
  return json_({ok:true,liveUsers:users.length,typingNow:typingNow,hindiTyping:hindi,englishTyping:english,users:users,serverTime:new Date().toISOString()});
}

function config_() {
  const c=controlMap_();
  return json_({ok:true,appVersion:c.AppVersion||'1.0.2',information:c.Information||'',onlineThresholdMinutes:Number(c.OnlineThresholdMinutes||5),downloadCount:Number(c.DownloadCount||0)});
}

function licenses_() {
  const sh=sheet_(SHEETS.LICENSES);
  if (!sh) { setupSheets(); return licenses_(); }
  const rows=sh.getDataRange().getValues();
  const data=[];
  for(let i=1;i<rows.length;i++) if(rows[i][0]) data.push({licenseKey:String(rows[i][0]),status:String(rows[i][1]||''),userId:String(rows[i][2]||''),activatedAt:String(rows[i][3]||''),expiresAt:String(rows[i][4]||'')});
  return json_({ok:true,licenses:data,active:data.filter(x=>x.status.toLowerCase()==='active').length});
}

function downloads_() {
  const sh=sheet_(SHEETS.DOWNLOADS);
  if (!sh) { setupSheets(); return downloads_(); }
  const rows=sh.getDataRange().getValues();
  const data=[];
  for(let i=1;i<rows.length;i++) if(rows[i][0]) data.push({time:String(rows[i][0]),version:String(rows[i][1]||''),source:String(rows[i][2]||''),userId:String(rows[i][3]||'')});
  return json_({ok:true,total:data.length,downloads:data.slice(-100).reverse()});
}

function download_(b) {
  const sh=sheet_(SHEETS.DOWNLOADS); if (!sh) { setupSheets(); return download_(b); }
  sh.appendRow([new Date(),String(b.version||''),String(b.source||'unknown'),String(b.userId||'')]);
  const c=controlMap_(); setControl_('DownloadCount',String(Number(c.DownloadCount||0)+1));
  return json_({ok:true});
}

function admin_(b) {
  const token=PropertiesService.getScriptProperties().getProperty('ADMIN_TOKEN');
  if (!token || String(b.token||'') !== token) return json_({ok:false,error:'Unauthorized'});
  const op=String(b.op||'').toLowerCase();
  if(op==='announcement'){setControl_('Information',String(b.text||''));return json_({ok:true});}
  if(op==='version'){setControl_('AppVersion',String(b.version||''));return json_({ok:true});}
  if(op==='threshold'){setControl_('OnlineThresholdMinutes',String(Number(b.minutes||5)));return json_({ok:true});}
  if(op==='license'){
    const sh=sheet_(SHEETS.LICENSES); if(!sh){setupSheets();return admin_(b);}
    sh.appendRow([String(b.key||''),String(b.status||'Active'),String(b.userId||''),new Date(),String(b.expiresAt||'')]);
    return json_({ok:true});
  }
  return json_({ok:false,error:'Unknown admin operation'});
}

function controlMap_(){
  const sh=sheet_(SHEETS.CONTROL); if(!sh){setupSheets();return controlMap_();}
  const rows=sh.getDataRange().getValues(), out={};
  for(let i=1;i<rows.length;i++) if(rows[i][0]) out[String(rows[i][0])]=String(rows[i][1]||'');
  return out;
}
function setControl_(key,value){
  const sh=sheet_(SHEETS.CONTROL); if(!sh){setupSheets();return setControl_(key,value);}
  const rows=sh.getDataRange().getValues();
  for(let i=1;i<rows.length;i++) if(String(rows[i][0])===key){sh.getRange(i+1,2).setValue(value);return;}
  sh.appendRow([key,value]);
}
