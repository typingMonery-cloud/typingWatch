const SHEETS = {
  LIVE_USERS: 'LiveUsers',
  CONTROL: 'Control',
  LICENSES: 'Licenses',
  DOWNLOADS: 'Downloads',
  CHAT: 'Chat'
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
    [SHEETS.DOWNLOADS, ['Time','Version','Source','UserID']],
    [SHEETS.CHAT, ['Time','UserID','Message']]
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
    OnlineThresholdMinutes: '2', AppVersion: '1.0.3', Information: 'Typing Watch is online.',
    DownloadCount: '0', MaintenanceMode: 'false', MinimumVersion: '1.0.0', TargetHours: '100'
  };
  Object.keys(defaults).forEach(k => { if (keys.indexOf(k) < 0) control.appendRow([k, defaults[k]]); });
  if (typeof WEBSITE_CONTENT_DEFAULTS !== 'undefined') {
    Object.keys(WEBSITE_CONTENT_DEFAULTS).forEach(k => { if (keys.indexOf(k) < 0) control.appendRow([k, WEBSITE_CONTENT_DEFAULTS[k]]); });
  }
  const props = PropertiesService.getScriptProperties();
  if (!props.getProperty('ADMIN_TOKEN') && !props.getProperty('ADMIN_KEY')) {
    const token = Utilities.getUuid().replace(/-/g,'') + Utilities.getUuid().replace(/-/g,'');
    props.setProperty('ADMIN_TOKEN', token);
    Logger.log('ADMIN_TOKEN created. Store it securely; do not publish it.');
  }
  return json_({ok:true,success:true,message:'Typing Watch sheets ready.'});
}

function doPost(e) {
  try {
    const body = readBody_(e);
    const action = String(body.action || '').toLowerCase();
    if (action === 'heartbeat') return heartbeat_(body);
    if (action === 'download' || action === 'download_event') return download_(body);
    if (action === 'chatsend' || action === 'chat_send') return chatSend_(body);
    if (action === 'check_license' || action === 'checklicense' || action === 'verifylicense') return checkLicense_(body);
    if (action === 'admin') return admin_(body);
    if (action === 'admin_apps') return admin_(Object.assign({},body,{action:'admin',op:body.op||'apps'}));
    if (action === 'update_announcement') return admin_(Object.assign({},body,{action:'admin',op:'announcement',text:body.announcement || body.text}));
    if (action === 'update_config' || action === 'save_settings') return admin_(Object.assign({},body,{action:'admin',op:'settings',settings:body.settings || body}));
    if (action === 'toggle_maintenance') return admin_(Object.assign({},body,{action:'admin',op:'maintenance',enabled:body.enabled}));
    if (action === 'generate_license' || action === 'manage_license' || action === 'create_license') return admin_(Object.assign({},body,{action:'admin',op:'generate_license'}));
    if (action === 'search_customer') return admin_(Object.assign({},body,{action:'admin',op:'search_customer'}));
    return json_({ok:false,success:false,error:'Unknown action: '+action});
  } catch (err) { return json_({ok:false,success:false,error:String(err)}); }
}

function doGet(e) {
  let response;
  try {
    const params = (e && e.parameter) || {};
    const action = String(params.action || 'stats').toLowerCase();
    if (action === 'health') response = json_({ok:true,success:true,service:'Typing Watch',serverTime:new Date().toISOString()});
    else if (action === 'stats' || action === 'admin_stats') response = stats_();
    else if (action === 'config' || action === 'getconfig') response = config_();
    else if (action === 'website_content' || action === 'websitecontent') response = websiteContent_();
    else if (action === 'apps') response = appsPublic_();
    else if (action === 'licenses') response = licenses_();
    else if (action === 'downloads') response = downloads_();
    else if (action === 'chat') response = chat_();
    else if (action === 'dashboard' || action === 'dashboard_data') {
      const stats = statsObject_();
      const config = configObject_();
      response = json_({ok:true,success:true,stats:stats,config:config,liveUsers:stats.liveUsers,typingNow:stats.typingNow,downloadCount:config.downloadCount,activeLicenses:licenseCount_()});
    } else if (action === 'check_license' || action === 'checklicense' || action === 'verifylicense') response = checkLicense_(params);
    else if (action === 'admin') response = admin_(params);
    else response = json_({ok:false,success:false,error:'Unknown action: '+action});
  } catch (err) {
    response = json_({ok:false,success:false,error:String(err)});
  }

  const callback = String((e && e.parameter && e.parameter.callback) || '');
  if (callback && /^[A-Za-z_$][0-9A-Za-z_$.]*$/.test(callback)) {
    return ContentService.createTextOutput(callback + '(' + response.getContent() + ');')
      .setMimeType(ContentService.MimeType.JAVASCRIPT);
  }
  return response;
}

function readBody_(e) {
  if (e && e.postData && e.postData.contents) {
    try { return JSON.parse(e.postData.contents); } catch (_) {}
  }
  return (e && e.parameter) || {};
}

function heartbeat_(b) {
  const sh = ensureSheet_(SHEETS.LIVE_USERS, ['UserID','LastSeen','Typing','Language','WPM','Version']);
  const id = String(b.userId || b.UserID || '').trim();
  if (!id) return json_({ok:false,success:false,error:'Missing userId'});
  const lock = LockService.getScriptLock();
  lock.waitLock(10000);
  try {
    const rows = sh.getDataRange().getValues();
    let row = -1;
    for (let i=1;i<rows.length;i++) if (String(rows[i][0]) === id) { row=i+1; break; }
    const values = [id,new Date(),String(b.typing||'false'),String(b.language||''),Number(b.wpm||0),String(b.version||'')];
    if (row < 0) sh.appendRow(values); else sh.getRange(row,1,1,values.length).setValues([values]);
  } finally { lock.releaseLock(); }
  return json_({ok:true,success:true,received:true});
}

function statsObject_() {
  const sh = ensureSheet_(SHEETS.LIVE_USERS, ['UserID','LastSeen','Typing','Language','WPM','Version']);
  const rows = sh.getDataRange().getValues();
  const control = controlMap_();
  const mins = Math.max(1,Number(control.OnlineThresholdMinutes || 2));
  const cutoff = new Date(Date.now() - mins*60000);
  const users = [];
  let typingNow = 0, hindi = 0, english = 0;
  for (let i=1;i<rows.length;i++) {
    const r=rows[i];
    if (!r[0] || !(r[1] instanceof Date) || r[1] < cutoff) continue;
    const u={userId:String(r[0]),lastSeen:r[1].toISOString(),typing:String(r[2]).toLowerCase()==='true',language:String(r[3]||''),wpm:Number(r[4]||0),version:String(r[5]||'')};
    users.push(u);
    if (u.typing) typingNow++;
    if (u.language.toLowerCase()==='hindi') hindi++;
    if (u.language.toLowerCase()==='english') english++;
  }
  return {ok:true,success:true,liveUsers:users.length,onlineUsers:users.length,typingNow:typingNow,hindiTyping:hindi,englishTyping:english,users:users,serverTime:new Date().toISOString()};
}
function stats_() { return json_(statsObject_()); }

function configObject_() {
  const c=controlMap_();
  const version=c.AppVersion || '1.0.3';
  const announcement=c.Information || '';
  return {ok:true,success:true,appVersion:version,version:version,information:announcement,announcement:announcement,
    onlineThresholdMinutes:Number(c.OnlineThresholdMinutes||2),downloadCount:Number(c.DownloadCount||0),
    maintenanceMode:String(c.MaintenanceMode||'false').toLowerCase()==='true',minimumVersion:c.MinimumVersion||'1.0.0',minVersion:c.MinimumVersion||'1.0.0',
    targetHours:Number(c.TargetHours||100),updateRequired:String(c.UpdateRequired||'false').toLowerCase()==='true'};
}
function config_() { return json_(configObject_()); }

function licenses_() {
  const sh=ensureSheet_(SHEETS.LICENSES, ['LicenseKey','Status','UserID','ActivatedAt','ExpiresAt']);
  const rows=sh.getDataRange().getValues();
  const data=[];
  for(let i=1;i<rows.length;i++) if(rows[i][0]) data.push({licenseKey:String(rows[i][0]),status:String(rows[i][1]||''),userId:String(rows[i][2]||''),activatedAt:dateString_(rows[i][3]),expiresAt:dateString_(rows[i][4])});
  return json_({ok:true,success:true,licenses:data,active:data.filter(x=>x.status.toLowerCase()==='active').length,total:data.length});
}

function downloads_() {
  const sh=ensureSheet_(SHEETS.DOWNLOADS, ['Time','Version','Source','UserID']);
  const rows=sh.getDataRange().getValues();
  const data=[];
  for(let i=1;i<rows.length;i++) if(rows[i][0]) data.push({time:dateString_(rows[i][0]),version:String(rows[i][1]||''),source:String(rows[i][2]||''),userId:String(rows[i][3]||'')});
  return json_({ok:true,success:true,total:data.length,downloads:data.slice(-100).reverse()});
}

function download_(b) {
  const sh=ensureSheet_(SHEETS.DOWNLOADS, ['Time','Version','Source','UserID']);
  sh.appendRow([new Date(),String(b.version||''),String(b.source||'unknown'),String(b.userId||'')]);
  const c=controlMap_();
  setControl_('DownloadCount',String(Number(c.DownloadCount||0)+1));
  return json_({ok:true,success:true});
}

function chat_() {
  const sh=ensureSheet_(SHEETS.CHAT, ['Time','UserID','Message']);
  const rows=sh.getDataRange().getValues();
  const messages=[];
  for(let i=Math.max(1,rows.length-100);i<rows.length;i++){
    const r=rows[i];
    if(!r[0]||!r[1]||!r[2])continue;
    messages.push(dateString_(r[0])+'|||'+String(r[1])+'|||'+String(r[2]).replace(/[\r\n]+/g,' '));
  }
  return json_({ok:true,success:true,messages:messages.join('\n')});
}
function chatSend_(b) {
  const id=String(b.userId||'').trim();
  const msg=String(b.message||'').trim().replace(/[\r\n]+/g,' ').substring(0,300);
  if(!id||!msg)return json_({ok:false,success:false,error:'userId and message are required'});
  ensureSheet_(SHEETS.CHAT,['Time','UserID','Message']).appendRow([new Date(),id,msg]);
  return json_({ok:true,success:true});
}

function checkLicense_(b) {
  const key=String(b.licenseKey||b.key||'').trim();
  const userId=String(b.userId||'').trim();
  if(!key)return json_({ok:false,success:false,valid:false,error:'Missing licenseKey'});
  const sh=ensureSheet_(SHEETS.LICENSES,['LicenseKey','Status','UserID','ActivatedAt','ExpiresAt']);
  const rows=sh.getDataRange().getValues();
  for(let i=1;i<rows.length;i++){
    if(String(rows[i][0]).trim()!==key)continue;
    const status=String(rows[i][1]||'').toLowerCase();
    const bound=String(rows[i][2]||'').trim();
    const expires=rows[i][4]?new Date(rows[i][4]):null;
    const expired=expires&&!isNaN(expires.getTime())&&expires.getTime()<Date.now();
    if(status!=='active'||expired)return json_({ok:true,success:true,valid:false,error:expired?'License expired':'License inactive'});
    if(bound&&userId&&bound!==userId)return json_({ok:true,success:true,valid:false,error:'License is bound to another user'});
    if(!bound&&userId)sh.getRange(i+1,3).setValue(userId);
    if(!rows[i][3])sh.getRange(i+1,4).setValue(new Date());
    return json_({ok:true,success:true,valid:true,licenseKey:key,userId:userId});
  }
  return json_({ok:true,success:true,valid:false,error:'License not found'});
}

function admin_(b) {
  const props=PropertiesService.getScriptProperties();
  const token=props.getProperty('ADMIN_TOKEN')||props.getProperty('ADMIN_KEY');
  const supplied=String(b.token||b.adminToken||b.adminKey||b.AdminKey||'');
  if(!token||supplied!==token)return json_({ok:false,success:false,error:'Unauthorized'});
  const op=String(b.op||b.operation||'').toLowerCase();

  if(op==='apps'||op==='apps_save'||op==='apps_delete') return appsAdmin_(b);
  if(op==='announcement'||op==='update_announcement'){
    setControl_('Information',String(b.text!==undefined?b.text:(b.announcement||'')));
    return json_({ok:true,success:true});
  }
  if(op==='version'){
    setControl_('AppVersion',String(b.version||''));
    return json_({ok:true,success:true});
  }
  if(op==='threshold'){
    setControl_('OnlineThresholdMinutes',String(Math.max(1,Number(b.minutes||5))));
    return json_({ok:true,success:true});
  }
  if(op==='maintenance'||op==='toggle_maintenance'){
    setControl_('MaintenanceMode',String(!!b.enabled));
    return json_({ok:true,success:true,maintenanceMode:!!b.enabled});
  }
  if(op==='settings'||op==='update_config'||op==='save_settings'){
    const s=b.settings||b;
    if(s.version||s.Version||s.AppVersion)setControl_('AppVersion',String(s.version||s.Version||s.AppVersion));
    if(s.minVersion||s.MinimumVersion||s.MinVersion)setControl_('MinimumVersion',String(s.minVersion||s.MinimumVersion||s.MinVersion));
    if(s.targetHours!==undefined||s.TargetHours!==undefined)setControl_('TargetHours',String(s.targetHours!==undefined?s.targetHours:s.TargetHours));
    if(s.onlineThresholdMinutes!==undefined||s.OnlineThresholdMinutes!==undefined)setControl_('OnlineThresholdMinutes',String(Math.max(1,Number(s.onlineThresholdMinutes!==undefined?s.onlineThresholdMinutes:s.OnlineThresholdMinutes))));
    return json_({ok:true,success:true,config:configObject_()});
  }
  if(op==='website_content'||op==='save_website_content'||op==='websitecontent')return saveWebsiteContent_(b);
  if(op==='license'||op==='generate_license'||op==='create_license'||op==='manage_license'){
    const key=String(b.key||b.licenseKey||('TW-'+Utilities.getUuid().replace(/-/g,'').toUpperCase().substring(0,16)));
    const status=String(b.status||'Active');
    const userId=String(b.userId||'');
    let expires=String(b.expiresAt||b.expiryDate||'');
    if(!expires&&b.days){const d=new Date();d.setDate(d.getDate()+Math.max(1,Number(b.days)));expires=d.toISOString();}
    const sh=ensureSheet_(SHEETS.LICENSES,['LicenseKey','Status','UserID','ActivatedAt','ExpiresAt']);
    const rows=sh.getDataRange().getValues();
    for(let i=1;i<rows.length;i++) if(String(rows[i][0])===key) return json_({ok:false,success:false,error:'License already exists'});
    sh.appendRow([key,status,userId,new Date(),expires]);
    return json_({ok:true,success:true,licenseKey:key,key:key,expiresAt:expires});
  }
  if(op==='set_license_status'||op==='revoke_license'){
    const key=String(b.key||b.licenseKey||'');
    const sh=ensureSheet_(SHEETS.LICENSES,['LicenseKey','Status','UserID','ActivatedAt','ExpiresAt']);
    const rows=sh.getDataRange().getValues();
    for(let i=1;i<rows.length;i++) if(String(rows[i][0])===key){
      sh.getRange(i+1,2).setValue(String(b.status||'Revoked'));
      return json_({ok:true,success:true});
    }
    return json_({ok:false,success:false,error:'License not found'});
  }
  if(op==='search_customer'||op==='search'){
    const q=String(b.query||b.customerId||b.licenseKey||'').toLowerCase().trim();
    const licenses=JSON.parse(licenses_().getContent()).licenses;
    const results=q?licenses.filter(x=>[x.licenseKey,x.userId,x.status].join(' ').toLowerCase().indexOf(q)>=0):licenses;
    return json_({ok:true,success:true,results:results});
  }
  return json_({ok:false,success:false,error:'Unknown admin operation: '+op});
}

function licenseCount_(){const d=JSON.parse(licenses_().getContent());return Number(d.active||0);}
function ensureSheet_(name,headers){
  const ss=getBook_();
  if(!ss)throw new Error('No active spreadsheet. Open Apps Script from the target Google Sheet before running setupSheets().');
  let sh=ss.getSheetByName(name);
  if(!sh)sh=ss.insertSheet(name);
  if(sh.getLastRow()===0)sh.getRange(1,1,1,headers.length).setValues([headers]);
  return sh;
}
function controlMap_(){
  const sh=ensureSheet_(SHEETS.CONTROL,['Key','Value']);
  const rows=sh.getDataRange().getValues(),out={};
  for(let i=1;i<rows.length;i++)if(rows[i][0])out[String(rows[i][0])]=String(rows[i][1]||'');
  return out;
}
function setControl_(key,value){
  const sh=ensureSheet_(SHEETS.CONTROL,['Key','Value']);
  const rows=sh.getDataRange().getValues();
  for(let i=1;i<rows.length;i++)if(String(rows[i][0])===key){sh.getRange(i+1,2).setValue(value);return;}
  sh.appendRow([key,value]);
}
function dateString_(v){if(v instanceof Date)return v.toISOString();return String(v==null?'':v);}
