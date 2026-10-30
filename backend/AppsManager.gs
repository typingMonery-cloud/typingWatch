/*
 TYPING WATCH - WEBSITE APPS MANAGER

 Add these helpers to the same Apps Script project as Code.gs.
 Then add the two small routing lines shown in README section below.

 Google Sheet created automatically:
 Apps
 Columns:
 ID | Name | Platform | Description | DownloadUrl | Status | Icon | SortOrder | UpdatedAt
*/

const APPS_SHEET_NAME = 'Apps';
const APPS_HEADERS = ['ID','Name','Platform','Description','DownloadUrl','Status','Icon','SortOrder','UpdatedAt'];

function ensureAppsSheet_(){
  const ss = SpreadsheetApp.getActiveSpreadsheet();
  let sh = ss.getSheetByName(APPS_SHEET_NAME);
  if(!sh) sh = ss.insertSheet(APPS_SHEET_NAME);
  if(sh.getLastRow()===0) sh.getRange(1,1,1,APPS_HEADERS.length).setValues([APPS_HEADERS]);
  return sh;
}

function appsList_(){
  const sh=ensureAppsSheet_();
  const rows=sh.getDataRange().getValues();
  const out=[];
  for(let i=1;i<rows.length;i++){
    const r=rows[i];
    if(!r[0] && !r[1]) continue;
    const status=String(r[5]||'Active');
    if(status.toLowerCase()==='hidden') continue;
    out.push({
      id:String(r[0]||''),
      name:String(r[1]||''),
      platform:String(r[2]||'OTHER'),
      description:String(r[3]||''),
      downloadUrl:String(r[4]||''),
      status:status,
      icon:String(r[6]||'📦'),
      sortOrder:Number(r[7]||0),
      updatedAt:dateString_(r[8])
    });
  }
  out.sort((a,b)=>a.sortOrder-b.sortOrder || a.name.localeCompare(b.name));
  return out;
}

function appsPublic_(){
  return json_({ok:true,success:true,apps:appsList_()});
}

function appsAdmin_(b){
  const op=String(b.op||'').toLowerCase();
  if(op==='apps' && String(b.mode||'list').toLowerCase()==='list') return json_({ok:true,success:true,apps:appsList_()});
  if(op==='apps_save'){
    const sh=ensureAppsSheet_();
    const id=String(b.id||('APP-'+Utilities.getUuid().replace(/-/g,'').substring(0,12).toUpperCase()));
    const values=[
      id,
      String(b.name||'').trim(),
      String(b.platform||'OTHER').trim().toUpperCase(),
      String(b.description||'').trim(),
      String(b.downloadUrl||'').trim(),
      String(b.status||'Active').trim(),
      String(b.icon||'📦').trim(),
      Number(b.sortOrder||0),
      new Date()
    ];
    if(!values[1]) return json_({ok:false,success:false,error:'App name is required'});
    const rows=sh.getDataRange().getValues();
    let row=-1;
    for(let i=1;i<rows.length;i++) if(String(rows[i][0])===id){row=i+1;break;}
    if(row<0) sh.appendRow(values); else sh.getRange(row,1,1,values.length).setValues([values]);
    return json_({ok:true,success:true,id:id,apps:appsList_()});
  }
  if(op==='apps_delete'){
    const id=String(b.id||'').trim();
    if(!id) return json_({ok:false,success:false,error:'Missing app id'});
    const sh=ensureAppsSheet_();
    const rows=sh.getDataRange().getValues();
    for(let i=1;i<rows.length;i++){
      if(String(rows[i][0])===id){
        sh.deleteRow(i+1);
        return json_({ok:true,success:true,apps:appsList_()});
      }
    }
    return json_({ok:false,success:false,error:'App not found'});
  }
  return json_({ok:false,success:false,error:'Unknown apps operation'});
}

/*
 ROUTING TO ADD TO Code.gs

 1) Inside doGet(e), before the final Unknown action response:
    if (action === 'apps') return appsPublic_();
    if (action === 'admin') return adminGet_(e.parameter || {});

 2) Inside doPost(e), before the final Unknown action response:
    if (action === 'admin_apps') return admin_(Object.assign({},body,{action:'admin',op:body.op||'apps'}));

 3) Add this helper once to Code.gs. It reuses the existing admin_() authentication:
*/
function adminGet_(p){
  const b=Object.assign({},p,{action:'admin'});
  return admin_(b);
}

/*
 4) Inside existing admin_(), BEFORE its final unknown-operation response, add:
    if(op==='apps' || op==='apps_save' || op==='apps_delete') return appsAdmin_(b);

 The Apps Manager page calls:
    action=admin&op=apps
    action=admin&op=apps_save
    action=admin&op=apps_delete

 Public customer website calls:
    action=apps
*/
