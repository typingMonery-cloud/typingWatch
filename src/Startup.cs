using System;
using System.IO;
using System.Reflection;
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
            Type appType=typeof(TypingWatch);
            ConstructorInfo ctor=appType.GetConstructor(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,Type.EmptyTypes,null);
            if(ctor==null) throw new MissingMethodException("TypingWatch parameterless constructor was not found.");
            Form app=(Form)ctor.Invoke(null);
            Application.Run(app);
        }
        catch(Exception ex)
        {
            Exception root=ex;
            if(ex is TargetInvocationException && ex.InnerException!=null) root=ex.InnerException;
            try
            {
                string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"TypingWatch");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder,"startup-crash.log"),DateTime.Now.ToString("o")+Environment.NewLine+root.ToString());
            }
            catch { }
            throw;
        }
    }
}
