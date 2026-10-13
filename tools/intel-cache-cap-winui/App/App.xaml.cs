using Microsoft.UI.Xaml;
namespace ArcCache;
public partial class App : Application {
    Window? window;
    public App(){InitializeComponent();UnhandledException+=(_,e)=>File.AppendAllText(Path.Combine(Path.GetTempPath(),"ArcCache-crash.log"),DateTime.Now+" "+e.Exception+Environment.NewLine);}
    protected override void OnLaunched(LaunchActivatedEventArgs args){window=new MainWindow();string[] command=Environment.GetCommandLineArgs();int at=Array.IndexOf(command,"--preview");if(at>=0&&at+1<command.Length){uint id=0;if(at+2<command.Length)uint.TryParse(command[at+2],out id);int watch=0;if(at+3<command.Length)int.TryParse(command[at+3],out watch);((MainWindow)window).StartPreview(command[at+1],id,watch,command.Contains("--preview-driver-update"));}else window.Activate();}
}
