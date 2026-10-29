void SyncOnlineNow()
{
    if (!OnlineConfigured() ||
        String.IsNullOrEmpty(onlineUserId))
    {
        return;
    }

    ThreadPool.QueueUserWorkItem(
        delegate
        {
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12;

                using (WebClient wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] =
                        "TypingWatch/" + AppVersion;

                    wc.Headers[HttpRequestHeader.ContentType] =
                        "application/json; charset=utf-8";

                    bool typing =
                        (DateTime.Now - lastKey).TotalSeconds < 5;

                    string language =
                        lastTypingLanguage == TypingLanguage.Hindi
                        ? "Hindi"
                        :
                        (
                            lastTypingLanguage == TypingLanguage.English
                            ? "English"
                            : "Unknown"
                        );

                    string payload =
                        "{\"action\":\"heartbeat\"," +
                        "\"userId\":\"" +
                        onlineUserId + "\"," +
                        "\"typing\":\"" +
                        (typing ? "true" : "false") +
                        "\"," +
                        "\"language\":\"" +
                        language +
                        "\"," +
                        "\"wpm\":" +
                        ((int)Math.Round(displayedWpm)) +
                        "," +
                        "\"version\":\"" +
                        AppVersion +
                        "\"}";

                    // ==========================================
                    // HEARTBEAT
                    // ==========================================
                    try
                    {
                        string result =
                            wc.UploadString(
                                OnlineServerUrl,
                                "POST",
                                payload);

                        File.AppendAllText(
                            Path.Combine(
                                folder,
                                "online.log"),
                            DateTime.Now.ToString("o") +
                            " | HEARTBEAT OK | " +
                            result +
                            Environment.NewLine);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            File.AppendAllText(
                                Path.Combine(
                                    folder,
                                    "online.log"),
                                DateTime.Now.ToString("o") +
                                " | HEARTBEAT ERROR | " +
                                ex.ToString() +
                                Environment.NewLine);
                        }
                        catch
                        {
                        }
                    }

                    // ==========================================
                    // LIVE USERS / STATS
                    // ==========================================
                    try
                    {
                        string stats =
                            wc.DownloadString(
                                OnlineServerUrl +
                                "?action=stats");

                        string live =
                            ExtractJsonValue(
                                stats,
                                "liveUsers");

                        int n;

                        if (int.TryParse(live, out n))
                        {
                            BeginInvoke(
                                (MethodInvoker)delegate
                                {
                                    onlineUsers =
                                        Math.Max(0, n);

                                    onlineUsersLabel.Text =
                                        "Online: " +
                                        onlineUsers;
                                });
                        }

                        try
                        {
                            File.AppendAllText(
                                Path.Combine(
                                    folder,
                                    "online.log"),
                                DateTime.Now.ToString("o") +
                                " | STATS OK | " +
                                stats +
                                Environment.NewLine);
                        }
                        catch
                        {
                        }
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            File.AppendAllText(
                                Path.Combine(
                                    folder,
                                    "online.log"),
                                DateTime.Now.ToString("o") +
                                " | STATS ERROR | " +
                                ex.ToString() +
                                Environment.NewLine);
                        }
                        catch
                        {
                        }
                    }

                    // ==========================================
                    // CONFIG / ANNOUNCEMENT
                    // ==========================================
                    try
                    {
                        string config =
                            wc.DownloadString(
                                OnlineServerUrl +
                                "?action=config");

                        string msg =
                            ExtractJsonValue(
                                config,
                                "information");

                        if (!String.IsNullOrEmpty(msg))
                        {
                            BeginInvoke(
                                (MethodInvoker)delegate
                                {
                                    announcement = msg;

                                    announcementLabel.Text =
                                        "📢 " +
                                        announcement;
                                });
                        }

                        try
                        {
                            File.AppendAllText(
                                Path.Combine(
                                    folder,
                                    "online.log"),
                                DateTime.Now.ToString("o") +
                                " | CONFIG OK | " +
                                config +
                                Environment.NewLine);
                        }
                        catch
                        {
                        }
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            File.AppendAllText(
                                Path.Combine(
                                    folder,
                                    "online.log"),
                                DateTime.Now.ToString("o") +
                                " | CONFIG ERROR | " +
                                ex.ToString() +
                                Environment.NewLine);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(
                        Path.Combine(
                            folder,
                            "online.log"),
                        DateTime.Now.ToString("o") +
                        " | ONLINE ERROR | " +
                        ex.ToString() +
                        Environment.NewLine);
                }
                catch
                {
                }
            }
        });
}
