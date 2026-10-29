using System;
using System.IO;
using System.Windows.Forms;

class Startup
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            Application.Run(new TypingWatch());
        }
        catch (Exception ex)
        {
            try
            {
                string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"TypingWatch");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder,"startup-crash.log"),DateTime.Now.ToString("o")+Environment.NewLine+ex.ToString());
            }
            catch { }
            throw;
        }
    }
}
