const WEBSITE_CONTENT_DEFAULTS = {
  HeroOffer:'', HeroTitle:'', HeroDescription:'', DownloadButton:'', LicenseButton:'',
  Trust1:'', Trust2:'', Trust3:'', Trust4:'', ReadyTitle:'', ReadyText:'',
  AppsTitle:'', AppsDescription:'', FeaturesTitle:'', FeaturesDescription:'',
  Feature1Title:'', Feature1Text:'', Feature2Title:'', Feature2Text:'',
  Feature3Title:'', Feature3Text:'', Feature4Title:'', Feature4Text:'',
  Feature5Title:'', Feature5Text:'', Feature6Title:'', Feature6Text:'',
  LicenseOffer:'', LicenseYear:'', LicenseTitle:'', LicenseDescription:'',
  LicenseSteps:'', LicenseSupportTitle:'', LicenseSupportText:'',
  FaqTitle:'', FaqDescription:'', Faq1Q:'', Faq1A:'', Faq2Q:'', Faq2A:'',
  Faq3Q:'', Faq3A:'', Faq4Q:'', Faq4A:'', Faq5Q:'', Faq5A:'', FooterText:''
};

const WEBSITE_CONTENT_KEYS = Object.keys(WEBSITE_CONTENT_DEFAULTS);

const LICENSE_HEADERS = ['LicenseKey','UserID','Status','PlanType','ExpiryDate','CreatedDate','Notes'];

const SHEETS = {
  LIVE_USERS: 'LiveUsers',
  CONTROL: 'Control',
  LICENSES: 'Licenses',
  DOWNLOADS: 'Downloads',
  CHAT: 'Chat',
  APPS: 'Apps'
};

function getBook_() { return SpreadsheetApp.getActiveSpreadsheet(); }
function sheet_(name) { return getBook_().getSheetByName(name); }
function json_(obj) { return ContentService.createTextOutput(JSON.stringify(obj)).setMimeType(ContentService.MimeType.JSON); }

function setupSheets() {
  const ss = getBook_();
  const defs = [
    [SHEETS.LIVE_USERS, ['UserID','LastSeen','Typing','Language','WPM','Version']],
    [SHEETS.CONTROL, ['Key','Value']],
    [SHEETS.LICENSES, LICENSE_HEADERS],
    [SHEETS.DOWNLOADS, ['Time','Version','Source','UserID']],
    [SHEETS.CHAT, ['Time','UserID','Message']],
    [SHEETS.APPS, ['Name','Platform','Description','DownloadUrl','Status','Icon','SortOrder']]
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
    OnlineThresholdMinutes: '2', AppVersion: '1.0.13', Information: 'Typing Watch is online.',
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
    else if (action === 'stats' || action === 'admin_stats') response = statsPublic_();
    else if (action === 'config' || action === 'getconfig') response = config_();
    else if (action === 'website_content' || action === 'websitecontent') response = websiteContent_();
    else if (action === 'apps') response = appsPublic_();
    else if (action === 'licenses' || action === 'downloads') response = json_({ok:false,success:false,error:'Admin authentication required'});
    else if (action === 'chat') response = chat_();
    else if (action === 'dashboard' || action === 'dashboard_data') {
      const stats = statsObject_();
      delete stats.users;
      const config = configObject_();
      response = json_({ok:true,success:true,stats:stats,config:config,liveUsers:stats.liveUsers,typingNow:stats.typingNow,downloadCount:config.downloadCount});
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
  const version=c.AppVersion || '1.0.13';
  const announcement=c.Information || '';
  return {ok:true,success:true,appVersion:version,version:version,information:announcement,announcement:announcement,
    onlineThresholdMinutes:Number(c.OnlineThresholdMinutes||2),downloadCount:Number(c.DownloadCount||0),
    maintenanceMode:String(c.MaintenanceMode||'false').toLowerCase()==='true',minimumVersion:c.MinimumVersion||'1.0.0',minVersion:c.MinimumVersion||'1.0.0',
    targetHours:Number(c.TargetHours||100),updateRequired:String(c.UpdateRequired||'false').toLowerCase()==='true'};
}
function config_() { return json_(configObject_()); }

function licenses_() {
  const meta=ensureLicenseSheet_(),sh=meta.sheet,c=meta.cols;
  const rows=sh.getDataRange().getValues(),data=[];
  for(let i=1;i<rows.length;i++){
    const r=rows[i];
    if(!r[c.licenseKey])continue;
    const created=dateString_(r[c.createdDate]);
    const expiry=dateString_(r[c.expiryDate]);
    data.push({
      licenseKey:String(r[c.licenseKey]||''),status:String(r[c.status]||''),
      userId:String(r[c.userId]||''),planType:String(r[c.planType]||'Standard'),
      activatedAt:created,createdDate:created,expiresAt:expiry,expiryDate:expiry,
      notes:String(r[c.notes]||'')
    });
  }
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
  const meta=ensureLicenseSheet_(),sh=meta.sheet,c=meta.cols;
  const rows=sh.getDataRange().getValues();
  for(let i=1;i<rows.length;i++){
    if(String(rows[i][c.licenseKey]||'').trim()!==key)continue;
    const status=String(rows[i][c.status]||'').toLowerCase();
    const bound=String(rows[i][c.userId]||'').trim();
    const expires=rows[i][c.expiryDate]?new Date(rows[i][c.expiryDate]):null;
    const expired=expires&&!isNaN(expires.getTime())&&expires.getTime()<Date.now();
    if(status!=='active'||expired)return json_({ok:true,success:true,valid:false,error:expired?'License expired':'License inactive'});
    if(bound&&userId&&bound!==userId)return json_({ok:true,success:true,valid:false,error:'License is bound to another user'});
    if(!bound&&userId)sh.getRange(i+1,c.userId+1).setValue(userId);
    if(!rows[i][c.createdDate])sh.getRange(i+1,c.createdDate+1).setValue(new Date());
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

  if(op==='stats') return json_(statsObject_());
  if(op==='list_licenses') return licenses_();
  if(op==='list_downloads') return downloads_();
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
    const email=String(b.email||b.userId||'').trim();
    if(email.indexOf('@')<1)return json_({ok:false,success:false,error:'Valid customer email is required to generate a TypingWatch license'});
    const userId=String(b.userId||email).trim();
    const planType=String(b.planType||'Standard').trim()||'Standard';
    const notes=String(b.notes||'').trim();
    let expiryInput=String(b.expiresAt||b.expiryDate||'').trim();
    if(!expiryInput&&b.days){
      const d=new Date();d.setDate(d.getDate()+Math.max(1,Number(b.days)));
      expiryInput=d.toISOString();
    }
    if(!expiryInput)return json_({ok:false,success:false,error:'Expiry date or license days are required'});
    const expiryDate=new Date(expiryInput);
    if(isNaN(expiryDate.getTime()))return json_({ok:false,success:false,error:'Invalid expiry date'});
    const expiryKey=Utilities.formatDate(expiryDate,Session.getScriptTimeZone(),'yyyyMMdd');
    const packageName='TYPINGWATCH';
    const key=makeTypingWatchLicenseKey_(email,expiryKey,packageName,planType);
    const meta=ensureLicenseSheet_(),sh=meta.sheet,c=meta.cols;
    const rows=sh.getDataRange().getValues();
    for(let i=1;i<rows.length;i++)if(String(rows[i][c.licenseKey]||'')===key)return json_({ok:false,success:false,error:'License already exists'});
    const row=new Array(sh.getLastColumn()).fill('');
    row[c.licenseKey]=key;row[c.userId]=userId;row[c.status]='Active';
    row[c.planType]=planType;row[c.expiryDate]=expiryDate;row[c.createdDate]=new Date();row[c.notes]=notes;
    sh.appendRow(row);
    let emailSent=false,emailError='';
    try {
      const subject='Your TypingWatch License Key';
      const body='Hello,\n\nYour TypingWatch license has been generated successfully.\n\nIMPORTANT NOTE: Please copy the COMPLETE License Key exactly as written below. This is the full and complete license key. Do not remove or change any part of it.\n\nFULL LICENSE KEY:\n'+key+'\n\nPlan: '+planType+'\nValid until: '+Utilities.formatDate(expiryDate,Session.getScriptTimeZone(),'dd MMM yyyy')+'\n\nPlease keep this key safe.\n\nTypingWatch Team';
      MailApp.sendEmail({to:email,subject:subject,body:body,name:'TypingWatch'});
      emailSent=true;
    } catch(mailErr) { emailError=String(mailErr && mailErr.message ? mailErr.message : mailErr); }
    return json_({ok:true,success:true,licenseKey:key,key:key,userId:userId,expiresAt:expiryDate.toISOString(),planType:planType,emailSent:emailSent,emailError:emailError});
  }
  if(op==='set_license_status'||op==='revoke_license'){
    const key=String(b.key||b.licenseKey||'');
    const meta=ensureLicenseSheet_(),sh=meta.sheet,c=meta.cols;
    const rows=sh.getDataRange().getValues();
    for(let i=1;i<rows.length;i++)if(String(rows[i][c.licenseKey]||'')===key){
      sh.getRange(i+1,c.status+1).setValue(String(b.status||'Revoked'));
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


function appsPublic_() {
  const sh=ensureSheet_(SHEETS.APPS,['Name','Platform','Description','DownloadUrl','Status','Icon','SortOrder']);
  const rows=sh.getDataRange().getValues();
  const apps=[];
  for(let i=1;i<rows.length;i++){
    const r=rows[i];
    if(!r[0])continue;
    apps.push({
      name:String(r[0]||''),platform:String(r[1]||'OTHER'),
      description:String(r[2]||''),downloadUrl:String(r[3]||''),
      status:String(r[4]||'Active'),icon:String(r[5]||'📦'),
      sortOrder:Number(r[6]||0)
    });
  }
  apps.sort((a,b)=>a.sortOrder-b.sortOrder);
  return json_({ok:true,success:true,apps:apps});
}

function appsAdmin_(b) {
  const sh=ensureSheet_(SHEETS.APPS,['Name','Platform','Description','DownloadUrl','Status','Icon','SortOrder']);
  const op=String(b.op||'apps').toLowerCase();
  if(op==='apps_delete'){
    const name=String(b.name||b.appName||'').trim();
    if(!name)return json_({ok:false,success:false,error:'App name is required'});
    const rows=sh.getDataRange().getValues();
    for(let i=1;i<rows.length;i++){
      if(String(rows[i][0]).trim()===name){sh.deleteRow(i+1);return json_({ok:true,success:true});}
    }
    return json_({ok:false,success:false,error:'App not found'});
  }
  if(op==='apps_save'){
    let apps=b.apps||[];
    if(typeof apps==='string'){try{apps=JSON.parse(apps);}catch(e){return json_({ok:false,success:false,error:'Apps JSON is invalid'});}}
    if(!Array.isArray(apps))return json_({ok:false,success:false,error:'Apps must be a JSON array'});
    if(apps.length>100)return json_({ok:false,success:false,error:'Maximum 100 apps allowed'});
    if(sh.getLastRow()>1)sh.getRange(2,1,sh.getLastRow()-1,7).clearContent();
    const values=apps.filter(a=>a&&String(a.name||a.Name||'').trim()).map(a=>[
      String(a.name||a.Name||'').trim().substring(0,100),
      String(a.platform||a.Platform||'OTHER').substring(0,40),
      String(a.description||a.Description||'').substring(0,1000),
      String(a.downloadUrl||a.DownloadUrl||'').substring(0,1000),
      String(a.status||a.Status||'Active').substring(0,30),
      String(a.icon||a.Icon||'📦').substring(0,20),
      Number(a.sortOrder||a.SortOrder||0)
    ]);
    if(values.length)sh.getRange(2,1,values.length,7).setValues(values);
    return json_({ok:true,success:true,saved:values.length});
  }
  return appsPublic_();
}

function websiteContent_() {
  const c=controlMap_(),content={};
  WEBSITE_CONTENT_KEYS.forEach(k=>{if(c[k]!==undefined&&c[k]!=='')content[k]=c[k];});
  return json_({ok:true,success:true,content:content});
}

function saveWebsiteContent_(b) {
  let content=b.content||{};
  if(typeof content==='string'){
    try{content=JSON.parse(content);}catch(e){return json_({ok:false,success:false,error:'Website content JSON is invalid'});}
  }
  if(!content||typeof content!=='object'||Array.isArray(content))
    return json_({ok:false,success:false,error:'Website content must be a JSON object'});
  let saved=0;
  WEBSITE_CONTENT_KEYS.forEach(k=>{
    if(Object.prototype.hasOwnProperty.call(content,k)){
      setControl_(k,String(content[k]===null?'':content[k]).substring(0,3000));
      saved++;
    }
  });
  return json_({ok:true,success:true,saved:saved});
}


function statsPublic_() {
  const s=statsObject_();
  return json_({
    ok:true,success:true,liveUsers:s.liveUsers,onlineUsers:s.onlineUsers,
    typingNow:s.typingNow,hindiTyping:s.hindiTyping,englishTyping:s.englishTyping,
    serverTime:s.serverTime
  });
}


function ensureLicenseSheet_() {
  const sh=ensureSheet_(SHEETS.LICENSES,LICENSE_HEADERS);
  let headers=sh.getRange(1,1,1,Math.max(1,sh.getLastColumn())).getValues()[0].map(x=>String(x||'').trim());
  const aliases={
    licenseKey:['LicenseKey','License Key'],
    userId:['UserID','User Id','User ID'],
    status:['Status'],
    planType:['PlanType','Plan Type'],
    expiryDate:['ExpiryDate','ExpiresAt','Expiry Date'],
    createdDate:['CreatedDate','ActivatedAt','Created Date'],
    notes:['Notes']
  };
  const cols={};
  Object.keys(aliases).forEach(k=>{
    let idx=-1;
    for(let i=0;i<headers.length;i++)if(aliases[k].indexOf(headers[i])>=0){idx=i;break;}
    if(idx<0){idx=headers.length;headers.push(aliases[k][0]);sh.getRange(1,idx+1).setValue(aliases[k][0]);}
    cols[k]=idx;
  });
  return {sheet:sh,cols:cols};
}

function makeTypingWatchLicenseKey_(email,expiry,packageName,planCode) {
  const salt='TW2026-OWNER-ASHISH-YADAV-CHANGE-BEFORE-RELEASE';
  const payload=String(email).toLowerCase()+'|'+String(expiry).toUpperCase()+'|'+
    String(packageName).trim()+'|'+String(planCode).toUpperCase()+'|'+salt;
  const digest=Utilities.computeDigest(Utilities.DigestAlgorithm.SHA_256,payload,Utilities.Charset.UTF_8);
  const hash=digest.slice(0,12).map(x=>('0'+((x+256)%256).toString(16)).slice(-2)).join('').toUpperCase();
  return [String(email),String(expiry),String(packageName).trim(),String(planCode).toUpperCase(),hash].join('|');
}
