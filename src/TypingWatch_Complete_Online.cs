using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.Threading;
using FormsTimer = System.Windows.Forms.Timer;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

class TypingWatch : Form
{
    [DllImport("user32.dll", SetLastError=true)] static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet=CharSet.Auto)] static extern IntPtr GetModuleHandle(string lpModuleName);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint idThread);
    delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    const int WH_KEYBOARD_LL=13, WM_KEYDOWN=0x0100, WM_SYSKEYDOWN=0x0104;
    IntPtr hook=IntPtr.Zero; LowLevelKeyboardProc proc;
    DateTime lastHookAttempt=DateTime.MinValue;
    int lastHookError=0;
    DateTime lastKey=DateTime.MinValue, lastTick=DateTime.Now;
    DateTime wpmStart=DateTime.Now;
    TimeSpan total=TimeSpan.Zero, today=TimeSpan.Zero;
    TimeSpan hindiTotal=TimeSpan.Zero, englishTotal=TimeSpan.Zero;
    TimeSpan hindiToday=TimeSpan.Zero, englishToday=TimeSpan.Zero;
    enum TypingLanguage { Unknown, Hindi, English }
    TypingLanguage lastTypingLanguage=TypingLanguage.Unknown;
    int keys=0;
    Queue<DateTime> recentKeys=new Queue<DateTime>();
    double displayedWpm=0;
    string folder,totalFile,dayFile,date;
    string hindiTotalFile,englishTotalFile,hindiDayFile,englishDayFile;
    FormsTimer timer=new FormsTimer(); Label label=new Label(); Label licenseStatusLabel=new Label(); Label ownerLabel=new Label(); Button editButton=new Button(); Button warningButton=new Button(); Button dashboardButton=new Button(); Button chatButton=new Button(); SpeedMeter meter=new SpeedMeter();
    string settingsFile;
    string trialFile, licenseFile;
    const string LicenseSalt = "TW2026-OWNER-ASHISH-YADAV-CHANGE-BEFORE-RELEASE";

    // ===== ONLINE FEATURES =====
    // Existing deployed Google Apps Script Web App for this project.
    const string OnlineServerUrl = "https://script.google.com/macros/s/AKfycbxqCie36dJeZXBTRDsFMDhFYE0vZHnhXFdH1laGqTLScO-IPTlqZJ9IOotb91oJfR2QUA/exec";
    const string AppVersion = "1.0.3";
    const string UpdateOwner = "typingMonery-cloud";
    const string UpdateRepository = "typingWatch";
    bool updateCheckRunning=false;
    string onlineUserId="";
    int onlineUsers=0;
    string announcement="Internet se information load ho rahi hai...";
    DateTime nextOnlineSync=DateTime.MinValue;
    Button updateButton=new Button();
    Label announcementLabel=new Label();
    Label onlineUsersLabel=new Label();
    int minWpm=15;
    bool beepEnabled=true;
    bool trialExpired=false;
    DateTime belowLimitSince=DateTime.MinValue;
    DateTime nextWarningBeepAt=DateTime.MinValue;

    TypingWatch()
    {
        folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"TypingWatch");
        Directory.CreateDirectory(folder);
        ConfigureAutoStart();
        totalFile=Path.Combine(folder,"total.dat");
        dayFile=Path.Combine(folder,"today.dat");
        settingsFile=Path.Combine(folder,"wpm-settings.dat");
        trialFile=Path.Combine(folder,"trial-start.dat");
        licenseFile=Path.Combine(folder,"license.dat");
        date=DateTime.Now.ToString("yyyy-MM-dd");
        hindiTotalFile=Path.Combine(folder,"hindi-total.dat");
        englishTotalFile=Path.Combine(folder,"english-total.dat");
        hindiDayFile=Path.Combine(folder,"hindi-today.dat");
        englishDayFile=Path.Combine(folder,"english-today.dat");

        Text="Typing Watch"; ClientSize=new Size(340, 585);
        FormBorderStyle=FormBorderStyle.FixedToolWindow;
        StartPosition=FormStartPosition.Manual; TopMost=true; BackColor=Color.White;
        Rectangle r=Screen.PrimaryScreen.WorkingArea;
        Left=r.Right-Width-15; Top=r.Bottom-Height-15;

        // Use explicit, non-overlapping bounds: Dock=Fill previously let the
        // label cover the meter/settings controls on some WinForms layouts.
        meter.SetBounds(7, 5, ClientSize.Width-14, 98);
        meter.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
        Controls.Add(meter);

        licenseStatusLabel.SetBounds(7, 103, ClientSize.Width-14, 18);
        licenseStatusLabel.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
        licenseStatusLabel.Font=new Font("Segoe UI",8,FontStyle.Bold);
        licenseStatusLabel.TextAlign=ContentAlignment.MiddleCenter;
        Controls.Add(licenseStatusLabel);

        ownerLabel.SetBounds(7, 121, ClientSize.Width-14, 16);
        ownerLabel.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
        ownerLabel.Font=new Font("Segoe UI",7,FontStyle.Bold);
        ownerLabel.ForeColor=Color.DimGray;
        ownerLabel.TextAlign=ContentAlignment.MiddleRight;
        ownerLabel.Text="OWNER: ASHISH FROM FARRUKHABAD";
        Controls.Add(ownerLabel);

        label.SetBounds(7, 139, ClientSize.Width-14, 205);
        label.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
        label.Font=new Font("Segoe UI",9,FontStyle.Bold);
        label.TextAlign=ContentAlignment.MiddleCenter;
        label.Cursor=Cursors.Hand;
        Controls.Add(label);

        warningButton.Text="WPM Warning Settings";
        warningButton.SetBounds(7, 350, ClientSize.Width-14, 27);
        warningButton.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
        warningButton.Click+=EditWarningSettings;
        Controls.Add(warningButton);

        editButton.Text="Edit Total Typing Time";
        editButton.SetBounds(7, 381, ClientSize.Width-14, 27);
        editButton.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
        editButton.Click+=EditTotal;
        Controls.Add(editButton);

        dashboardButton.Text="⚙ Settings Dashboard";
        dashboardButton.SetBounds(7, 412, ClientSize.Width-14, 27);
        dashboardButton.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
        dashboardButton.Click+=OpenDashboard;
        Controls.Add(dashboardButton);

        chatButton.Text="💬 Live Chat";
        chatButton.SetBounds(7, 443, ClientSize.Width-14, 27);
        chatButton.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
        chatButton.Click+=delegate { OpenChat(); };
        Controls.Add(chatButton);

        updateButton.Text="↻ Check for Updates";
        updateButton.SetBounds(7, 474, ClientSize.Width-14, 27);
        updateButton.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
        updateButton.Click+=delegate { CheckForUpdates(true); };
        Controls.Add(updateButton);

        announcementLabel.SetBounds(7, 505, ClientSize.Width-14, 45);
        announcementLabel.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
        announcementLabel.BorderStyle=BorderStyle.FixedSingle;
        announcementLabel.Font=new Font("Segoe UI",8,FontStyle.Bold);
        announcementLabel.TextAlign=ContentAlignment.MiddleCenter;
        announcementLabel.Text="📢 "+announcement;
        announcementLabel.AutoEllipsis=true;
        Controls.Add(announcementLabel);

        onlineUsersLabel.SetBounds(ClientSize.Width-98, 3, 90, 17);
        onlineUsersLabel.Anchor=AnchorStyles.Top|AnchorStyles.Right;
        onlineUsersLabel.Font=new Font("Segoe UI",7,FontStyle.Bold);
        onlineUsersLabel.TextAlign=ContentAlignment.MiddleRight;
        onlineUsersLabel.Text="Online: --";
        Controls.Add(onlineUsersLabel);

        LoadData();
        LoadWarningSettings();
        ContextMenu licenseMenu=new ContextMenu();
        licenseMenu.MenuItems.Add("About Typing Watch",(s,e)=>ShowAbout());
        licenseMenu.MenuItems.Add("WPM Warning Settings",EditWarningSettings);
        licenseMenu.MenuItems.Add("Edit Total Typing Time",EditTotal);
        licenseMenu.MenuItems.Add("Exit",(s,e)=>Close());
        ContextMenu=licenseMenu;
        label.ContextMenu=licenseMenu;
        editButton.ContextMenu=licenseMenu;
        warningButton.ContextMenu=licenseMenu;
        dashboardButton.ContextMenu=licenseMenu;
        updateButton.ContextMenu=licenseMenu;
        chatButton.ContextMenu=licenseMenu;
        announcementLabel.ContextMenu=licenseMenu;
        onlineUsersLabel.ContextMenu=licenseMenu;
        proc=HookCallback;
        // Install after the form/message loop is ready. A low-level keyboard hook
        // receives key events system-wide, even when another app (e.g. Notepad) has focus.
        Shown += (s,e) => InstallKeyboardHook();

        MouseDoubleClick+=OpenDashboard;
        label.DoubleClick+=OpenDashboard;
        meter.DoubleClick+=OpenDashboard;

        timer.Interval=250;
        timer.Tick+=(s,e)=>
        {
            // Retry global hook installation if Windows rejected the first attempt.
            if(hook==IntPtr.Zero && (DateTime.Now-lastHookAttempt).TotalSeconds>=3) InstallKeyboardHook();
            UpdateWatch();
            if(DateTime.Now>=nextOnlineSync) { nextOnlineSync=DateTime.Now.AddSeconds(5); SyncOnlineNow(); }
        };
        timer.Start();
        FormClosing+=(s,e)=>{SaveData();if(hook!=IntPtr.Zero)UnhookWindowsHookEx(hook);};
        BeginInvoke((MethodInvoker)delegate { StartOnlineSync(); CheckForUpdates(false); });
        UpdateWatch();
    }

    void ConfigureAutoStart()
    {
        // Per-user startup entry: no administrator rights required; compatible with Windows 7+.
        try
        {
            string exe=Application.ExecutablePath;
            using(RegistryKey key=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run",true))
            {
                if(key!=null) key.SetValue("TypingWatch", "\\\""+exe+"\\\"");
            }
        }
        catch
        {
            // Keep the tracker usable even if the system blocks startup registration.
        }
    }

    void OpenChat()
    {
        if(!OnlineConfigured() || String.IsNullOrEmpty(onlineUserId))
        {
            MessageBox.Show("Live Chat ke liye online connection zaroori hai.","Typing Watch",MessageBoxButtons.OK,MessageBoxIcon.Information);
            return;
        }
        using(LiveChatForm f=new LiveChatForm(OnlineServerUrl,onlineUserId)) f.ShowDialog(this);
    }

    void OpenDashboard(object sender,EventArgs e)
    {
        using(SettingsDashboardForm f=new SettingsDashboardForm(
            GetLicenseSummary(),
            delegate { EditWarningSettings(this,EventArgs.Empty); },
            delegate { EditTotal(this,EventArgs.Empty); },
            delegate { ActivateLicense(); UpdateWatch(); },
            delegate { ShowPayment(); }))
        {
            f.ShowDialog(this);
            UpdateWatch();
        }
    }

    string GetLicenseSummary()
    {
        string saved=ReadLicense();
        if(IsLicenseValid(saved))
        {
            string[] parts=saved.Split('|');
            if(parts.Length>=2 && parts[1].Trim().Equals("LIFETIME",StringComparison.OrdinalIgnoreCase)) return "License Status: ACTIVE (LIFETIME)";
            DateTime expiry;
            if(parts.Length>=2 && DateTime.TryParseExact(parts[1].Trim(),"yyyyMMdd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out expiry))
                return "License Status: ACTIVE\r\nExpiry: "+expiry.ToString("dd-MMM-yyyy");
            return "License Status: ACTIVE";
        }
        DateTime start;
        try
        {
            if(File.Exists(trialFile) && DateTime.TryParse(File.ReadAllText(trialFile),null,System.Globalization.DateTimeStyles.RoundtripKind,out start))
            {
                int left=Math.Max(0,(int)Math.Ceiling(5-(DateTime.Now-start).TotalDays));
                if(left>0) return "License Status: FREE TRIAL\r\nTrial days remaining: "+left+" of 5 days";
                return "License Status: TRIAL EXPIRED\r\nActivate a license to unlock tracking.";
            }
        }
        catch {}
        return "License Status: FREE TRIAL\r\nTrial starts when TypingWatch is launched.";
    }

    void InstallKeyboardHook()
    {
        if(hook!=IntPtr.Zero) return;
        lastHookAttempt=DateTime.Now;
        // WH_KEYBOARD_LL is global when thread id is zero. Keep the delegate in
        // the proc field for the entire lifetime of the hook. Try the documented
        // null module handle first, then the current executable module as fallback.
        hook=SetWindowsHookEx(WH_KEYBOARD_LL,proc,IntPtr.Zero,0);
        if(hook==IntPtr.Zero)
        {
            lastHookError=Marshal.GetLastWin32Error();
            try
            {
                IntPtr module=GetModuleHandle(null);
                hook=SetWindowsHookEx(WH_KEYBOARD_LL,proc,module,0);
                if(hook==IntPtr.Zero) lastHookError=Marshal.GetLastWin32Error();
            }
            catch { hook=IntPtr.Zero; lastHookError=Marshal.GetLastWin32Error(); }
        }
        else lastHookError=0;
    }

    void LoadData()
    {
        double x;
        if(File.Exists(hindiTotalFile)&&double.TryParse(File.ReadAllText(hindiTotalFile),out x))
            hindiTotal=TimeSpan.FromSeconds(x);
        if(File.Exists(englishTotalFile)&&double.TryParse(File.ReadAllText(englishTotalFile),out x))
            englishTotal=TimeSpan.FromSeconds(x);

        if(File.Exists(hindiDayFile))
        {
            string[] a=File.ReadAllLines(hindiDayFile);
            if(a.Length>=2 && a[0]==date && double.TryParse(a[1],out x)) hindiToday=TimeSpan.FromSeconds(x);
        }
        if(File.Exists(englishDayFile))
        {
            string[] a=File.ReadAllLines(englishDayFile);
            if(a.Length>=2 && a[0]==date && double.TryParse(a[1],out x)) englishToday=TimeSpan.FromSeconds(x);
        }
        total=hindiTotal+englishTotal;
        today=hindiToday+englishToday;
    }

    void SaveData()
    {
        total=hindiTotal+englishTotal;
        today=hindiToday+englishToday;
        File.WriteAllText(hindiTotalFile,hindiTotal.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.WriteAllText(englishTotalFile,englishTotal.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.WriteAllLines(hindiDayFile,new string[]{date,hindiToday.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)});
        File.WriteAllLines(englishDayFile,new string[]{date,englishToday.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)});
    }

    void EditTotal(object sender,EventArgs e)
    {
        using(EditForm f=new EditForm(total))
        {
            if(f.ShowDialog()==DialogResult.OK)
            {
                TimeSpan newOverall=f.Value;
                TimeSpan oldOverall=hindiTotal+englishTotal;
                TimeSpan delta=newOverall-oldOverall;
                // Preserve the two language counters. Apply manual overall correction
                // to the currently selected language; if none is selected, Hindi is used.
                if(lastTypingLanguage==TypingLanguage.English) englishTotal+=delta;
                else hindiTotal+=delta;
                if(englishTotal<TimeSpan.Zero) englishTotal=TimeSpan.Zero;
                if(hindiTotal<TimeSpan.Zero) hindiTotal=TimeSpan.Zero;
                SaveData();
                UpdateWatch();
            }
        }
    }

    void LoadWarningSettings()
    {
        try
        {
            if(File.Exists(settingsFile))
            {
                string[] a=File.ReadAllLines(settingsFile);
                int v;
                if(a.Length>0 && int.TryParse(a[0],out v) && v>=0 && v<=300) minWpm=v;
                if(a.Length>1) beepEnabled=(a[1]=="1");
            }
        } catch { }
    }

    void SaveWarningSettings()
    {
        File.WriteAllLines(settingsFile,new string[]{minWpm.ToString(),beepEnabled?"1":"0"});
    }

    void EditWarningSettings(object sender,EventArgs e)
    {
        using(WarningSettingsForm f=new WarningSettingsForm(minWpm,beepEnabled))
        {
            f.StartPosition=FormStartPosition.CenterParent;
            if(f.ShowDialog(this)==DialogResult.OK)
            {
                minWpm=f.MinimumWpm;
                beepEnabled=f.BeepEnabled;
                SaveWarningSettings();
                UpdateWatch();
            }
        }
    }

    bool IsLicenseValid(string key)
    {
        if(String.IsNullOrWhiteSpace(key)) return false;
        string[] p=key.Trim().Split('|');
        if(p.Length<2) return false;
        string email=p[0].Trim(); string expiry=p[1].Trim().ToUpperInvariant();
        if(email.IndexOf('@')<1) return false;
        if(expiry=="LIFETIME")
        {
            if(p.Length<3) return false;
            string expected=MakePackageKeyHash(email,expiry,p[2].Trim(),p.Length>=4?p[3].Trim():"LIFETIME");
            return p.Length>=5 && String.Equals(p[4].Trim(),expected,StringComparison.OrdinalIgnoreCase);
        }
        DateTime d;
        if(!DateTime.TryParseExact(expiry,"yyyyMMdd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out d)) return false;
        if(d.Date<DateTime.Now.Date) return false;
        if(p.Length<5) return false;
        string expected2=MakePackageKeyHash(email,expiry,p[2].Trim(),p[3].Trim());
        return String.Equals(p[4].Trim(),expected2,StringComparison.OrdinalIgnoreCase);
    }

    string ReadLicense()
    {
        try { return File.Exists(licenseFile)?File.ReadAllText(licenseFile).Trim():""; } catch { return ""; }
    }

    static string MakeLicenseHash(string email,string expiry)
    {
        using(SHA256 sha=SHA256.Create())
        {
            byte[] b=sha.ComputeHash(Encoding.UTF8.GetBytes(email.ToLowerInvariant()+"|"+expiry.ToUpperInvariant()+"|"+LicenseSalt));
            StringBuilder sb=new StringBuilder();
            for(int i=0;i<12;i++) sb.Append(b[i].ToString("X2"));
            return sb.ToString();
        }
    }

    static string MakePackageKeyHash(string email,string expiry,string package,string planCode)
    {
        using(SHA256 sha=SHA256.Create())
        {
            string payload=email.ToLowerInvariant()+"|"+expiry.ToUpperInvariant()+"|"+package.Trim()+"|"+planCode.ToUpperInvariant()+"|"+LicenseSalt;
            byte[] b=sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
            StringBuilder sb=new StringBuilder();
            for(int i=0;i<12;i++) sb.Append(b[i].ToString("X2"));
            return sb.ToString();
        }
    }

    bool ActivateLicense()
    {
        using(LicenseForm f=new LicenseForm())
        {
            if(f.ShowDialog(this)!=DialogResult.OK) return IsLicenseValid(ReadLicense());
            if(!IsLicenseValid(f.LicenseKey))
            {
                MessageBox.Show("License key sahi nahi hai ya subscription expire ho gayi hai.","License Error",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return false;
            }
            try { File.WriteAllText(licenseFile,f.LicenseKey.Trim()); }
            catch { MessageBox.Show("License save nahi ho paya."); return false; }
            MessageBox.Show("License activate ho gaya.","TypingWatch",MessageBoxButtons.OK,MessageBoxIcon.Information);
            return true;
        }
    }

    void ShowPayment()
    {
        using(PaymentForm f=new PaymentForm()) f.ShowDialog(this);
    }

    void ShowAbout()
    {
        MessageBox.Show("Typing Watch\r\n\r\nTyping practice time tracker\r\nHindi + English activity\r\n\r\nFree-use build. Online features require the configured server.","About Typing Watch",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }

    void StartOnlineSync()
    {
        onlineUserId=GetOrCreateOnlineUserId();
        SyncOnlineNow();
        nextOnlineSync=DateTime.Now.AddSeconds(5);
    }

    string GetOrCreateOnlineUserId()
    {
        string f=Path.Combine(folder,"online-user-id.dat");
        try
        {
            if(File.Exists(f))
            {
                string s=File.ReadAllText(f).Trim();
                Guid g;
                if(Guid.TryParse(s,out g)) return s;
            }
            string id=Guid.NewGuid().ToString("N");
            File.WriteAllText(f,id);
            return id;
        }
        catch { return Guid.NewGuid().ToString("N"); }
    }

    bool OnlineConfigured()
    {
        return !String.IsNullOrWhiteSpace(OnlineServerUrl) && OnlineServerUrl.IndexOf("YOUR-TYPINGWATCH-WORKER",StringComparison.OrdinalIgnoreCase)<0;
    }

    void SyncOnlineNow()
    {
        if(!OnlineConfigured() || String.IsNullOrEmpty(onlineUserId)) return;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
                using(WebClient wc=new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent]="TypingWatch/"+AppVersion;
                    wc.Headers[HttpRequestHeader.ContentType]="application/json; charset=utf-8";
                    bool typing=(DateTime.Now-lastKey).TotalSeconds<5;
                    string language=lastTypingLanguage==TypingLanguage.Hindi?"Hindi":(lastTypingLanguage==TypingLanguage.English?"English":"Unknown");
                    string payload="{\"action\":\"heartbeat\",\"userId\":\""+onlineUserId+"\",\"typing\":\""+(typing?"true":"false")+"\",\"language\":\""+language+"\",\"wpm\":"+((int)Math.Round(displayedWpm))+",\"version\":\""+AppVersion+"\"}";
                    try { wc.UploadString(OnlineServerUrl,"POST",payload); } catch { }
                    try
                    {
                        string stats=wc.DownloadString(OnlineServerUrl+"?action=stats");
                        int n; string live=ExtractJsonValue(stats,"liveUsers");
                        if(int.TryParse(live,out n))
                            BeginInvoke((MethodInvoker)delegate { onlineUsers=Math.Max(0,n); onlineUsersLabel.Text="Online: "+onlineUsers; });
                    }
                    catch { }
                    try
                    {
                        string config=wc.DownloadString(OnlineServerUrl+"?action=config");
                        string msg=ExtractJsonValue(config,"information");
                        if(!String.IsNullOrEmpty(msg))
                            BeginInvoke((MethodInvoker)delegate { announcement=msg; announcementLabel.Text="📢 "+announcement; });
                    }
                    catch { }
                }
            }
            catch { }
        });
    }

    bool IsNewerVersion(string latest,string current)
    {
        Version a,b;
        if(Version.TryParse(latest.Trim().TrimStart('v','V'),out a) && Version.TryParse(current.Trim().TrimStart('v','V'),out b)) return a>b;
        return false;
    }

    void CheckForUpdates(bool manual)
    {
        if(updateCheckRunning) return;
        if(UpdateOwner=="YOUR-GITHUB-USERNAME")
        {
            if(manual) MessageBox.Show("Auto-update check is ready. GitHub repository details are not connected yet.\r\n\r\nStep 2 mein repository URL set karna hoga.","Typing Watch",MessageBoxButtons.OK,MessageBoxIcon.Information);
            return;
        }
        updateCheckRunning=true; updateButton.Enabled=false;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
                string api="https://raw.githubusercontent.com/"+UpdateOwner+"/"+UpdateRepository+"/main/version.json";
                string json;
                using(WebClient wc=new WebClient()) { wc.Headers[HttpRequestHeader.UserAgent]="TypingWatch/"+AppVersion; json=wc.DownloadString(api); }
                string tag=ExtractJsonValue(json,"tag_name");
                if(String.IsNullOrEmpty(tag)) tag=ExtractJsonValue(json,"version");
                string url="https://github.com/"+UpdateOwner+"/"+UpdateRepository+"/releases";
                bool newer=IsNewerVersion(tag,AppVersion);
                BeginInvoke((MethodInvoker)delegate
                {
                    updateCheckRunning=false; updateButton.Enabled=true;
                    if(newer)
                    {
                        DialogResult dr=MessageBox.Show("New Typing Watch version available: "+tag+"\r\nCurrent: "+AppVersion+"\r\n\r\nGitHub release page open karein?","Update Available",MessageBoxButtons.YesNo,MessageBoxIcon.Information);
                        if(dr==DialogResult.Yes && !String.IsNullOrWhiteSpace(url)) Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
                    }
                    else if(manual) MessageBox.Show("Latest version installed hai ("+AppVersion+").","Typing Watch",MessageBoxButtons.OK,MessageBoxIcon.Information);
                });
            }
            catch(Exception ex)
            {
                BeginInvoke((MethodInvoker)delegate { updateCheckRunning=false; updateButton.Enabled=true; if(manual) MessageBox.Show("Update check failed.\r\n\r\n"+ex.Message,"Typing Watch",MessageBoxButtons.OK,MessageBoxIcon.Warning); });
            }
        });
    }

    string ExtractJsonValue(string json,string key)
    {
        string token="\""+key+"\""; int i=json.IndexOf(token,StringComparison.OrdinalIgnoreCase); if(i<0) return "";
        i=json.IndexOf(':',i+token.Length); if(i<0) return ""; i++; while(i<json.Length && Char.IsWhiteSpace(json[i])) i++;
        if(i>=json.Length) return "";
        if(json[i]!='"')
        {
            int start=i;
            while(i<json.Length && json[i]!=',' && json[i]!='}' && !Char.IsWhiteSpace(json[i])) i++;
            return json.Substring(start,i-start).Trim();
        }
        i++; StringBuilder sb=new StringBuilder(); bool esc=false;
        for(;i<json.Length;i++){ char c=json[i]; if(esc){sb.Append(c);esc=false;continue;} if(c=='\\'){esc=true;continue;} if(c=='"') break; sb.Append(c); }
        return sb.ToString();
    }

    void UpdateWatch()
    {
        DateTime now=DateTime.Now;
        if(trialExpired && !IsLicenseValid(ReadLicense()))
        {
            meter.Wpm=0;
            meter.MinWpm=minWpm;
            meter.Warning=false;
            meter.Invalidate();
            licenseStatusLabel.Text="TRIAL EXPIRED - LICENSE REQUIRED";
            licenseStatusLabel.ForeColor=Color.Red;
            label.Text="⌨ TYPING WATCH\r\n\r\nTRIAL EXPIRED\r\n\r\nWATCH: ON BUT INACTIVE\r\nWPM Meter: LOCKED\r\nTyping Time: LOCKED\r\n\r\nBuy License to activate\r\nRight-click for Payment / License";
            lastTick=now;
            return;
        }
        if(trialExpired && IsLicenseValid(ReadLicense())) trialExpired=false;
        TimeSpan elapsed=now-lastTick;

        if(elapsed.TotalSeconds>0 && elapsed.TotalSeconds<10 && (now-lastKey).TotalSeconds<5)
        {
            if(lastTypingLanguage==TypingLanguage.Hindi)
            {
                hindiTotal+=elapsed;
                hindiToday+=elapsed;
            }
            else if(lastTypingLanguage==TypingLanguage.English)
            {
                englishTotal+=elapsed;
                englishToday+=elapsed;
            }
        }
        total=hindiTotal+englishTotal;
        today=hindiToday+englishToday;
        lastTick=now;

        // Use a rolling 3-second sample so the meter responds quickly instead of waiting
        // for a long 10-second window. WPM = characters per minute / 5.
        while(recentKeys.Count>0 && (now-recentKeys.Peek()).TotalSeconds>3.0)
            recentKeys.Dequeue();

        bool active=(now-lastKey).TotalSeconds<5;

        // Estimate WPM from the actual time span covered by recent character-producing
        // key events. A single key is treated as one character.
        if(active && recentKeys.Count>0)
        {
            double seconds=Math.Max(0.25,(now-recentKeys.Peek()).TotalSeconds);
            displayedWpm=(recentKeys.Count/5.0)*(60.0/seconds);
        }
        else if(!active) displayedWpm=0;
        displayedWpm=Math.Max(0,Math.Min(120,displayedWpm));

        meter.Wpm=(int)Math.Round(displayedWpm);
        meter.MinWpm=minWpm;
        meter.Warning=active && minWpm>0 && displayedWpm<minWpm;
        meter.Invalidate();

        bool licensed=IsLicenseValid(ReadLicense());
        if(!licensed)
        {
            if(!File.Exists(trialFile))
            {
                try { File.WriteAllText(trialFile,DateTime.Now.ToString("o")); } catch { }
            }
            DateTime start;
            if(DateTime.TryParse(File.Exists(trialFile)?File.ReadAllText(trialFile):"",null,System.Globalization.DateTimeStyles.RoundtripKind,out start))
                trialExpired=(DateTime.Now-start).TotalDays>=5;
        }
        else trialExpired=false;

        licenseStatusLabel.Text=licensed?"LICENSE: ACTIVE":(trialExpired?"TRIAL EXPIRED":"FREE TRIAL");
        licenseStatusLabel.ForeColor=licensed?Color.DarkGreen:(trialExpired?Color.Red:Color.DarkOrange);

        TimeSpan remaining=TimeSpan.FromHours(50)-total;
        if(remaining<TimeSpan.Zero) remaining=TimeSpan.Zero;
        label.Text="⌨ TYPING WATCH\r\n\r\n"+
            "Total: "+Format(total)+"\r\n"+
            "Today: "+Format(today)+"\r\n"+
            "Hindi: "+Format(hindiTotal)+"\r\n"+
            "English: "+Format(englishTotal)+"\r\n"+
            "Remaining (50h): "+Format(remaining)+"\r\n\r\n"+
            "WPM: "+((int)Math.Round(displayedWpm))+"\r\n"+
            "Keyboard: "+(active?"ACTIVE":"PAUSED (5s)")+"\r\n"+
            "Hook: "+(hook!=IntPtr.Zero?"OK":"RETRYING")+"\r\n"+
            "Online: "+onlineUsers;

        if(active && beepEnabled && minWpm>0 && displayedWpm<minWpm)
        {
            if(belowLimitSince==DateTime.MinValue) belowLimitSince=now;
            if(now>=nextWarningBeepAt && (now-belowLimitSince).TotalSeconds>=2)
            {
                try { System.Media.SystemSounds.Beep.Play(); } catch { }
                nextWarningBeepAt=now.AddSeconds(1);
            }
        }
        else
        {
            belowLimitSince=DateTime.MinValue;
            nextWarningBeepAt=DateTime.MinValue;
        }

        if(now.Second%5==0)
        {
            try { SaveData(); } catch { }
        }
    }

    string Format(TimeSpan t)
    {
        return string.Format("{0:00}:{1:00}:{2:00}",(int)t.TotalHours,t.Minutes,t.Seconds);
    }

    IntPtr HookCallback(int nCode,IntPtr wParam,IntPtr lParam)
    {
        if(nCode>=0 && (wParam==new IntPtr(WM_KEYDOWN) || wParam==new IntPtr(WM_SYSKEYDOWN)))
        {
            DateTime now=DateTime.Now;
            lastKey=now;
            keys++;
            recentKeys.Enqueue(now);
            wpmStart=now;

            try
            {
                IntPtr hwnd=GetForegroundWindow();
                uint pid;
                uint tid=GetWindowThreadProcessId(hwnd,out pid);
                IntPtr layout=GetKeyboardLayout(tid);
                long value=layout.ToInt64();
                int lang=(int)(value & 0xFFFF);
                // Hindi/Devanagari layouts commonly use 0x0439; otherwise treat as English.
                lastTypingLanguage=(lang==0x0439 || lang==0x0440 || lang==0x0461)?TypingLanguage.Hindi:TypingLanguage.English;
            }
            catch { if(lastTypingLanguage==TypingLanguage.Unknown) lastTypingLanguage=TypingLanguage.English; }
        }
        return CallNextHookEx(hook,nCode,wParam,lParam);
    }

    [STAThread] static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
        {
            try
            {
                string d=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"TypingWatch");
                Directory.CreateDirectory(d);
                File.AppendAllText(Path.Combine(d,"errors.log"),DateTime.Now.ToString("o")+" | UI | "+e.Exception+Environment.NewLine);
            }
            catch { }
        };
        Application.Run(new TypingWatch());
    }
}

class LicenseForm:Form
{
    public string LicenseKey="";
    TextBox box=new TextBox();
    public LicenseForm()
    {
        Text="Typing Watch — License"; Width=500; Height=190; FormBorderStyle=FormBorderStyle.FixedDialog; StartPosition=FormStartPosition.CenterParent; MaximizeBox=false; MinimizeBox=false;
        Label info=new Label(){Text="License key paste karein:",Left=15,Top=18,Width=450};
        box.SetBounds(15,48,450,28);
        Button ok=new Button(){Text="Activate",Left=285,Top=92,Width=85};
        Button cancel=new Button(){Text="Cancel",Left=380,Top=92,Width=85};
        ok.Click+=(s,e)=>{LicenseKey=box.Text.Trim(); if(LicenseKey.Length==0){MessageBox.Show("License key required hai.");return;} DialogResult=DialogResult.OK;Close();};
        cancel.Click+=(s,e)=>{DialogResult=DialogResult.Cancel;Close();};
        Controls.AddRange(new Control[]{info,box,ok,cancel}); AcceptButton=ok; CancelButton=cancel;
    }
}

class PaymentForm:Form
{
    const string OwnerEmail="bossbabysweetheart@gmail.com";
    ComboBox plan=new ComboBox(); TextBox customerName=new TextBox(); TextBox customerEmail=new TextBox(); TextBox phone=new TextBox(); PictureBox qr=new PictureBox();
    public PaymentForm()
    {
        Text="Typing Watch — Payment / Buy License"; Width=650; Height=610; StartPosition=FormStartPosition.CenterParent; FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false; TopMost=true;
        Label title=new Label(){Text="Typing Watch License Plans",Left=18,Top=15,Width=590,Height=30,Font=new Font("Segoe UI",14,FontStyle.Bold)};
        Label choose=new Label(){Text="Plan chunein:",Left=18,Top=49,Width=140};
        plan.SetBounds(160,46,300,28); plan.DropDownStyle=ComboBoxStyle.DropDownList;
        plan.Items.AddRange(new object[]{"Basic — 4 Months — ₹50","Standard — 6 Months — ₹60","Premium — 12 Months — ₹70"}); plan.SelectedIndex=1;
        Label nameLabel=new Label(){Text="Customer name:",Left=18,Top=83,Width=140}; customerName.SetBounds(160,80,300,24);
        Label emailLabel=new Label(){Text="Customer email:",Left=18,Top=113,Width=140}; customerEmail.SetBounds(160,110,300,24);
        Label phoneLabel=new Label(){Text="Mobile number:",Left=18,Top=143,Width=140}; phone.SetBounds(160,140,300,24);
        Label pay=new Label(){Text="QR scan karke selected plan ka payment karein.",Left=18,Top=172,Width=590,Height=24};
        qr.SetBounds(220,198,190,190); qr.SizeMode=PictureBoxSizeMode.Zoom; qr.BorderStyle=BorderStyle.FixedSingle;
        string q=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PhonePe_QR.png");
        if(File.Exists(q)) { try { using(var im=Image.FromFile(q)) qr.Image=new Bitmap(im); } catch { pay.Text="QR image load nahi hui. PhonePe_QR.png ko EXE ke saath same folder mein rakhein."; } }
        else pay.Text="QR nahi mila: PhonePe_QR.png ko EXE ke saath same folder mein rakhein.";
        Label instructions=new Label(){Left=18,Top=393,Width=600,Height=58,Text="Payment ke baad screenshot lekar email button dabayein. Email app mein screenshot khud attach karke Send karein. Payment manually verify hoga; phir license key email se di jayegi.",AutoSize=false};
        Button email=new Button(){Text="Payment Screenshot / License Request Email Karein",Left=100,Top=457,Width=420,Height=32};
        email.Click+=(s,e)=>{
            if(customerName.Text.Trim()=="" || customerEmail.Text.Trim()=="" || phone.Text.Trim()=="") { MessageBox.Show("Name, email aur mobile number bharein."); return; }
            string selected=plan.SelectedItem==null?"Standard — 6 Months — ₹60":plan.SelectedItem.ToString();
            string body="TypingWatch license request\r\nCustomer name: "+customerName.Text.Trim()+"\r\nCustomer email: "+customerEmail.Text.Trim()+"\r\nMobile: "+phone.Text.Trim()+"\r\nPlan: "+selected+"\r\nPayment status: Please verify manually.\r\nI will attach the payment screenshot before sending.";
            string url="mailto:"+OwnerEmail+"?subject="+Uri.EscapeDataString("TypingWatch Payment Screenshot / License Request")+"&body="+Uri.EscapeDataString(body);
            try { Process.Start(new ProcessStartInfo(url){UseShellExecute=true}); }
            catch { MessageBox.Show("Email app nahi khuli. Is address par manually email bhejein: "+OwnerEmail); }
        };
        Label mail=new Label(){Text="Payment email: "+OwnerEmail,Left=18,Top=499,Width=600,Height=24,ForeColor=Color.DarkBlue};
        Button close=new Button(){Text="Close",Left=265,Top=535,Width=100,Height=30}; close.Click+=(s,e)=>Close();
        Controls.AddRange(new Control[]{title,choose,plan,nameLabel,customerName,emailLabel,customerEmail,phoneLabel,phone,pay,qr,instructions,email,mail,close});
    }
}

class LiveChatForm : Form
{
    string serverUrl,userId;
    TextBox input=new TextBox();
    ListBox messages=new ListBox();
    Button send=new Button();
    FormsTimer refreshTimer=new FormsTimer();
    bool busy=false;

    public LiveChatForm(string url,string id)
    {
        serverUrl=url; userId=id;
        Text="Typing Watch — Live Chat"; Width=430; Height=520;
        StartPosition=FormStartPosition.CenterParent; FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=false; MinimizeBox=false; TopMost=true; BackColor=Color.White;
        Label title=new Label(){Text="💬 Live Chat — सभी Live Users",Left=15,Top=12,Width=380,Height=28,Font=new Font("Segoe UI",12,FontStyle.Bold)};
        messages.SetBounds(15,48,385,330); messages.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
        input.SetBounds(15,390,300,28); input.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
        send.Text="Send"; send.SetBounds(322,390,78,28); send.Anchor=AnchorStyles.Bottom|AnchorStyles.Right; send.Click+=(s,e)=>SendMessage();
        Label note=new Label(){Text="Anonymous user ID • Chat messages are shared with live users",Left=15,Top=425,Width=385,Height=35,ForeColor=Color.DimGray};
        Controls.AddRange(new Control[]{title,messages,input,send,note});
        AcceptButton=send;
        refreshTimer.Interval=5000; refreshTimer.Tick+=(s,e)=>LoadMessages(); refreshTimer.Start();
        FormClosed+=(s,e)=>refreshTimer.Stop();
        LoadMessages();
    }

    void LoadMessages()
    {
        if(busy) return;
        busy=true;
        ThreadPool.QueueUserWorkItem(delegate
        {
            string text="";
            try
            {
                ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
                using(WebClient wc=new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent]="TypingWatch/1.0.0";
                    text=wc.DownloadString(serverUrl+"?action=chat");
                }
            }
            catch { }
            BeginInvoke((MethodInvoker)delegate
            {
                busy=false;
                if(!String.IsNullOrEmpty(text))
                {
                    string data=ExtractChatValue(text,"messages");
                    messages.Items.Clear();
                    if(!String.IsNullOrEmpty(data))
                    {
                        string[] rows=data.Split(new char[]{'\n'},StringSplitOptions.RemoveEmptyEntries);
                        foreach(string row in rows)
                        {
                            string[] p=row.Split(new string[]{"|||"},3,StringSplitOptions.None);
                            if(p.Length==3) messages.Items.Add(p[0]+"  "+p[1]+": "+p[2]);
                        }
                    }
                    if(messages.Items.Count>0) messages.TopIndex=messages.Items.Count-1;
                }
            });
        });
    }

    void SendMessage()
    {
        string msg=input.Text.Trim();
        if(msg.Length==0 || busy) return;
        if(msg.Length>300) msg=msg.Substring(0,300);
        input.Clear(); busy=true;
        string safe=msg.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r"," ").Replace("\n"," ");
        string payload="{\"action\":\"chatSend\",\"userId\":\""+userId+"\",\"message\":\""+safe+"\"}";
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
                using(WebClient wc=new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent]="TypingWatch/1.0.0";
                    wc.Headers[HttpRequestHeader.ContentType]="application/json; charset=utf-8";
                    wc.UploadString(serverUrl,"POST",payload);
                }
            }
            catch { }
            BeginInvoke((MethodInvoker)delegate { busy=false; LoadMessages(); });
        });
    }

    string ExtractChatValue(string json,string key)
    {
        string token="\""+key+"\""; int i=json.IndexOf(token,StringComparison.OrdinalIgnoreCase); if(i<0) return "";
        i=json.IndexOf(':',i+token.Length); if(i<0) return ""; i++; while(i<json.Length && Char.IsWhiteSpace(json[i])) i++;
        if(i>=json.Length || json[i]!='\"') return ""; i++; StringBuilder sb=new StringBuilder(); bool esc=false;
        for(;i<json.Length;i++){ char c=json[i]; if(esc){ if(c=='n') sb.Append('\n'); else sb.Append(c); esc=false; continue; } if(c=='\\'){esc=true;continue;} if(c=='\"') break; sb.Append(c); }
        return sb.ToString();
    }
}

class SettingsDashboardForm : Form
{
    public SettingsDashboardForm(string licenseSummary, Action warning, Action editTime, Action activate, Action payment)
    {
        Text="TypingWatch — Settings Dashboard"; Size=new Size(520,510); StartPosition=FormStartPosition.CenterParent;
        FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false; BackColor=Color.White;
        Label title=new Label(){Text="TypingWatch Settings Dashboard",Left=20,Top=16,Width=460,Height=32,Font=new Font("Segoe UI",15,FontStyle.Bold)};
        Label intro=new Label(){Text="Manage your floating watch and preferences",Left=22,Top=52,Width=450,Height=24,ForeColor=Color.DimGray};
        GroupBox wpm=new GroupBox(){Text="WPM & Beep",Left=20,Top=88,Width=460,Height=75};
        Label wpmInfo=new Label(){Text="Set minimum WPM, warning color and Beep ON/OFF. Current beep interval: 1 second.",Left=12,Top=25,Width=430,Height=34};
        Button wpmButton=new Button(){Text="WPM / Beep Settings",Left=12,Top=45,Width=190,Height=25};
        wpmButton.Click+=(s,e)=>warning(); wpm.Controls.Add(wpmInfo); wpm.Controls.Add(wpmButton);
        GroupBox time=new GroupBox(){Text="Typing Time",Left=20,Top=172,Width=460,Height=70};
        Label timeInfo=new Label(){Text="Edit Total Typing Time. Today is calculated automatically.",Left=12,Top=22,Width=430,Height=25};
        Button timeButton=new Button(){Text="Edit Total Typing Time",Left=12,Top=42,Width=190,Height=25};
        timeButton.Click+=(s,e)=>editTime(); time.Controls.Add(timeInfo); time.Controls.Add(timeButton);
        GroupBox lic=new GroupBox(){Text="License & Free Trial",Left=20,Top=251,Width=460,Height=112};
        Label licInfo=new Label(){Text=licenseSummary,Left=12,Top=22,Width=430,Height=42,Font=new Font("Segoe UI",10,FontStyle.Bold)};
        Button activateButton=new Button(){Text="Enter License Key",Left=12,Top=70,Width=140,Height=28}; activateButton.Click+=(s,e)=>{activate();licInfo.Text="License Status refreshed. Reopen Dashboard to see current details.";};
        Button paymentButton=new Button(){Text="Payment / Buy License",Left=162,Top=70,Width=170,Height=28}; paymentButton.Click+=(s,e)=>payment();
        lic.Controls.Add(licInfo);lic.Controls.Add(activateButton);lic.Controls.Add(paymentButton);
        Label note=new Label(){Text="Settings are saved by TypingWatch. Double-click the floating watch or use its Settings button to reopen this dashboard.",Left=22,Top=374,Width=450,Height=42,ForeColor=Color.DimGray};
        Button close=new Button(){Text="Back to Floating Watch",Left=310,Top=425,Width=170,Height=30}; close.Click+=(s,e)=>Close();
        Controls.AddRange(new Control[]{title,intro,wpm,time,lic,note,close}); AcceptButton=close;
    }
}

class SpeedMeter : Panel
{
    public int Wpm { get; set; }
    public int MinWpm { get; set; }
    public bool Warning { get; set; }
    public SpeedMeter() { DoubleBuffered=true; BackColor=Color.White; ResizeRedraw=true; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g=e.Graphics;
        g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        // Fit the entire upper half of the gauge inside the short floating panel.
        float cx=Width/2f;
        float radius=Math.Min((Width/2f)-18f, Height-24f);
        if(radius<20) return;
        float cy=Height-10f;
        RectangleF arc=new RectangleF(cx-radius,cy-radius,radius*2f,radius*2f);
        Color accent=Warning ? Color.Red : Color.FromArgb(0,160,150);

        using(Pen track=new Pen(Color.FromArgb(225,230,232),8f))
        using(Pen progress=new Pen(accent,8f))
        {
            track.StartCap=System.Drawing.Drawing2D.LineCap.Round;
            track.EndCap=System.Drawing.Drawing2D.LineCap.Round;
            progress.StartCap=System.Drawing.Drawing2D.LineCap.Round;
            progress.EndCap=System.Drawing.Drawing2D.LineCap.Round;
            g.DrawArc(track,arc,180,180);
            float pct=Math.Max(0f,Math.Min(1f,Wpm/120f));
            if(pct>0.001f) g.DrawArc(progress,arc,180,180*pct);
        }

        // Tick marks and labels on the same 0-120 WPM scale.
        using(Pen tickPen=new Pen(Color.Gray,1.4f))
        using(Font tickFont=new Font("Segoe UI",7f,FontStyle.Regular))
        using(SolidBrush tickBrush=new SolidBrush(Color.DimGray))
        {
            int[] vals={0,30,60,90,120};
            foreach(int val in vals)
            {
                double a=(180.0-val/120.0*180.0)*Math.PI/180.0;
                float x1=cx+(float)((radius-5)*Math.Cos(a));
                float y1=cy-(float)((radius-5)*Math.Sin(a));
                float x2=cx+(float)((radius-12)*Math.Cos(a));
                float y2=cy-(float)((radius-12)*Math.Sin(a));
                g.DrawLine(tickPen,x1,y1,x2,y2);
                string t=val.ToString();
                SizeF z=g.MeasureString(t,tickFont);
                float tx=cx+(float)((radius-22)*Math.Cos(a))-z.Width/2f;
                float ty=cy-(float)((radius-22)*Math.Sin(a))-z.Height/2f;
                // Keep labels away from the panel edges.
                tx=Math.Max(1f,Math.Min(Width-z.Width-1f,tx));
                ty=Math.Max(11f,Math.Min(Height-z.Height-1f,ty));
                g.DrawString(t,tickFont,tickBrush,tx,ty);
            }
        }

        double angle=(180.0-Math.Max(0,Math.Min(120,Wpm))/120.0*180.0)*Math.PI/180.0;
        float nx=cx+(float)((radius-18)*Math.Cos(angle));
        float ny=cy-(float)((radius-18)*Math.Sin(angle));
        using(Pen needle=new Pen(Warning?Color.Red:Color.FromArgb(0,80,90),2.5f))
        using(SolidBrush hub=new SolidBrush(accent))
        {
            needle.StartCap=System.Drawing.Drawing2D.LineCap.Round;
            needle.EndCap=System.Drawing.Drawing2D.LineCap.Round;
            g.DrawLine(needle,cx,cy,nx,ny);
            g.FillEllipse(hub,cx-4f,cy-4f,8f,8f);
        }

        using(Font valueFont=new Font("Segoe UI",11f,FontStyle.Bold))
        using(SolidBrush valueBrush=new SolidBrush(Warning?Color.Red:Color.FromArgb(0,80,90)))
        using(Font titleFont=new Font("Segoe UI",7f,FontStyle.Bold))
        using(SolidBrush titleBrush=new SolidBrush(Color.DimGray))
        {
            string value=Math.Max(0,Wpm)+" WPM";
            SizeF z=g.MeasureString(value,valueFont);
            // The numeric readout sits below the arc's crown and above the pivot.
            float vy=Math.Max(17f,cy-radius*0.42f-z.Height/2f);
            g.DrawString(value,valueFont,valueBrush,cx-z.Width/2f,vy);
            g.DrawString("TYPING SPEED",titleFont,titleBrush,5f,2f);
        }
    }
}

class EditForm:Form
{
    public TimeSpan Value;
    TextBox box=new TextBox();

    public EditForm(TimeSpan current)
    {
        Text="Edit Total Typing Time";
        Width=340; Height=155;
        FormBorderStyle=FormBorderStyle.FixedDialog;
        StartPosition=FormStartPosition.CenterScreen;

        Label l=new Label(){Text="Total Typing (HH:MM:SS):",Left=15,Top=18,Width=170};
        box.SetBounds(185,15,115,25);
        box.Text=string.Format("{0:00}:{1:00}:{2:00}",(int)current.TotalHours,current.Minutes,current.Seconds);

        Button save=new Button(){Text="Save",Left=120,Top=60,Width=80};
        save.Click+=(s,e)=>{
            string[] parts=box.Text.Trim().Split(':');
            int hours, minutes, seconds;
            if(parts.Length!=3 ||
               !int.TryParse(parts[0],out hours) ||
               !int.TryParse(parts[1],out minutes) ||
               !int.TryParse(parts[2],out seconds) ||
               hours<0 || minutes<0 || minutes>59 || seconds<0 || seconds>59)
            {
                MessageBox.Show("HH:MM:SS format use karein. Example: 25:00:00");
                return;
            }
            // Explicitly interpret HH:MM:SS as hours, minutes, seconds.
            Value=new TimeSpan(hours,minutes,seconds);
            DialogResult=DialogResult.OK; Close();
        };

        Button cancel=new Button(){Text="Cancel",Left=210,Top=60,Width=80};
        cancel.Click+=(s,e)=>{DialogResult=DialogResult.Cancel;Close();};

        Controls.AddRange(new Control[]{l,box,save,cancel});
        AcceptButton=save; CancelButton=cancel;
    }
}

class WarningSettingsForm:Form
{
    public int MinimumWpm;
    public bool BeepEnabled;
    NumericUpDown limit=new NumericUpDown();
    CheckBox beep=new CheckBox();

    public WarningSettingsForm(int currentLimit,bool currentBeep)
    {
        Text="WPM Warning Settings";
        Width=360; Height=205;
        FormBorderStyle=FormBorderStyle.FixedDialog;
        StartPosition=FormStartPosition.CenterParent;
        TopMost=true; MaximizeBox=false; MinimizeBox=false;
        Label l=new Label(){Text="Minimum WPM limit (0 = off):",Left=15,Top=20,Width=205};
        limit.SetBounds(225,17,90,25); limit.Minimum=0; limit.Maximum=300; limit.Value=currentLimit;
        beep.Text="Beep sound ON/OFF (uncheck to turn OFF)";
        beep.SetBounds(18,58,300,25); beep.Checked=currentBeep;
        Label note=new Label(){Text="Warning works only while keyboard activity is detected.",Left=15,Top=88,Width=320};
        Button save=new Button(){Text="Save",Left=130,Top=120,Width=80};
        Button cancel=new Button(){Text="Cancel",Left=220,Top=120,Width=80};
        save.Click+=(s,e)=>{MinimumWpm=(int)limit.Value;BeepEnabled=beep.Checked;DialogResult=DialogResult.OK;Close();};
        cancel.Click+=(s,e)=>{DialogResult=DialogResult.Cancel;Close();};
        Controls.AddRange(new Control[]{l,limit,beep,note,save,cancel});
        AcceptButton=save; CancelButton=cancel;
    }
}
