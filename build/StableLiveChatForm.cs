class LiveChatForm : Form
{
    string serverUrl,userId,displayName;
    Action<string> saveName;
    TextBox input=new TextBox();
    ListBox messages=new ListBox();
    Button send=new Button(),changeName=new Button();
    FormsTimer refreshTimer=new FormsTimer();
    bool loadBusy=false;
    bool sendBusy=false;

    public LiveChatForm(string url,string id,string name,Action<string> save)
    {
        serverUrl=url; userId=id; displayName=name; saveName=save;
        Text="TYPING WATCH - Live Chat"; Width=430; Height=520;
        StartPosition=FormStartPosition.CenterParent; FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=false; MinimizeBox=false; TopMost=true; BackColor=Color.White;

        Label title=new Label(); title.Text="Live Chat - Name: "+displayName;
        title.SetBounds(15,12,285,28); title.Font=new Font("Segoe UI",11,FontStyle.Bold);
        changeName.Text="Change Name"; changeName.SetBounds(305,12,95,27);
        changeName.Click+=(s,e)=>
        {
            try
            {
                using(ChatNamePromptForm prompt=new ChatNamePromptForm(displayName))
                {
                    if(prompt.ShowDialog(this)==DialogResult.OK&&!String.IsNullOrWhiteSpace(prompt.DisplayName))
                    {
                        displayName=prompt.DisplayName;
                        if(displayName.Length>30)displayName=displayName.Substring(0,30);
                        if(saveName!=null)saveName(displayName);
                        title.Text="Live Chat - Name: "+displayName;
                    }
                }
            }
            catch { }
        };

        messages.SetBounds(15,48,385,330);
        messages.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
        input.SetBounds(15,390,300,28); input.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;
        send.Text="Send"; send.SetBounds(322,390,78,28); send.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;
        send.Click+=(s,e)=>SendMessage();
        Label note=new Label(); note.Text="Aapke messages turant dikhte hain; chat sabhi users ke saath shared hai.";
        note.SetBounds(15,425,385,35); note.ForeColor=Color.DimGray;
        Controls.AddRange(new Control[]{title,changeName,messages,input,send,note});
        AcceptButton=send;

        refreshTimer.Interval=1000;
        refreshTimer.Tick+=(s,e)=>LoadMessages();
        refreshTimer.Start();
        FormClosed+=(s,e)=>
        {
            try { refreshTimer.Stop(); } catch { }
        };
        LoadMessages();
    }

    bool UiAlive()
    {
        return !IsDisposed && !Disposing && IsHandleCreated;
    }

    void SafeUi(Action action)
    {
        try
        {
            if(!UiAlive())return;
            BeginInvoke((MethodInvoker)delegate
            {
                try
                {
                    if(UiAlive()) action();
                }
                catch { }
            });
        }
        catch { }
    }

    void LoadMessages()
    {
        if(loadBusy || !UiAlive())return;
        loadBusy=true;
        ThreadPool.QueueUserWorkItem(delegate
        {
            string text="";
            try
            {
                ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
                using(WebClient wc=new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent]="TypingWatch/1.0.13";
                    text=wc.DownloadString(serverUrl+"?action=chat&_="+DateTime.UtcNow.Ticks);
                }
            }
            catch { }

            SafeUi(delegate
            {
                loadBusy=false;
                if(String.IsNullOrEmpty(text))return;
                string data=ExtractChatValue(text,"messages");
                if(String.IsNullOrEmpty(data))return;
                messages.Items.Clear();
                string[] rows=data.Split(new char[]{'\n'},StringSplitOptions.RemoveEmptyEntries);
                foreach(string row in rows)
                {
                    string[] p=row.Split(new string[]{"|||"},3,StringSplitOptions.None);
                    if(p.Length==3)messages.Items.Add(p[0]+"  "+p[1]+": "+p[2]);
                }
                if(messages.Items.Count>0)messages.TopIndex=messages.Items.Count-1;
                Text="TYPING WATCH - Live Chat";
            });
        });
    }

    void SendMessage()
    {
        if(sendBusy)return;
        string msg=input.Text.Trim();
        if(msg.Length==0)return;
        if(msg.Length>300)msg=msg.Substring(0,300);

        input.Clear();
        messages.Items.Add(DateTime.Now.ToString("HH:mm:ss")+"  "+displayName+": "+msg);
        if(messages.Items.Count>0)messages.TopIndex=messages.Items.Count-1;

        string safe=msg.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r"," ").Replace("\n"," ");
        string safeName=displayName.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r"," ").Replace("\n"," ");
        string payload="{\"action\":\"chatSend\",\"userId\":\""+safeName+"\",\"message\":\""+safe+"\"}";

        sendBusy=true;
        send.Enabled=false;
        ThreadPool.QueueUserWorkItem(delegate
        {
            bool sent=false;
            try
            {
                ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
                using(WebClient wc=new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent]="TypingWatch/1.0.13";
                    wc.Headers[HttpRequestHeader.ContentType]="application/json; charset=utf-8";
                    string response=wc.UploadString(serverUrl,"POST",payload);
                    sent=response.IndexOf("\"ok\":true",StringComparison.OrdinalIgnoreCase)>=0||
                         response.IndexOf("\"success\":true",StringComparison.OrdinalIgnoreCase)>=0;
                }
            }
            catch { }

            SafeUi(delegate
            {
                sendBusy=false;
                send.Enabled=true;
                if(sent)
                    Text="TYPING WATCH - Live Chat";
                else
                    Text="TYPING WATCH - Live Chat (pending sync)";
            });
        });
    }

    string ExtractChatValue(string json,string key)
    {
        string token="\""+key+"\"";
        int i=json.IndexOf(token,StringComparison.OrdinalIgnoreCase); if(i<0)return "";
        i=json.IndexOf(':',i+token.Length); if(i<0)return ""; i++;
        while(i<json.Length&&Char.IsWhiteSpace(json[i]))i++;
        if(i>=json.Length||json[i]!='\"')return ""; i++;
        StringBuilder sb=new StringBuilder(); bool esc=false;
        for(;i<json.Length;i++)
        {
            char c=json[i];
            if(esc){if(c=='n')sb.Append('\n');else sb.Append(c);esc=false;continue;}
            if(c=='\\'){esc=true;continue;}
            if(c=='\"')break;
            sb.Append(c);
        }
        return sb.ToString();
    }
}


// ================================================================

