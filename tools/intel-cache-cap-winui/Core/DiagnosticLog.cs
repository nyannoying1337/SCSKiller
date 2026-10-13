using System.Text.Json;

namespace ArcCache.Core;

// Local, opt-in diagnostics. Logging failure must never change a worker result.
public sealed class DiagnosticLog {
    readonly string preference;
    public string Path {get;}
    public bool Enabled {get;private set;}
    public DiagnosticLog(string root,bool sessionDebug=false) {
        preference=System.IO.Path.Combine(root,"diagnostics.json");
        Path=System.IO.Path.Combine(root,"logs","diagnostics.jsonl");
        try {Enabled=File.Exists(preference)&&JsonSerializer.Deserialize<bool>(File.ReadAllText(preference));}catch{Enabled=false;}
        Enabled|=sessionDebug;
    }
    public void SetEnabled(bool enabled) {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(preference)!);
        File.WriteAllText(preference,JsonSerializer.Serialize(enabled));Enabled=enabled;
        Write("diagnostics-enabled",new{OS=Environment.OSVersion.ToString(),Runtime=Environment.Version.ToString()});
    }
    public void Write(string kind,object details) {
        if(!Enabled)return;
        try {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            // Two bounded generations. No network or access to cache contents.
            if(File.Exists(Path)&&new FileInfo(Path).Length>=2*1024*1024)File.Move(Path,Path+".previous",true);
            string data=JsonSerializer.Serialize(details);
            if(data.Length>16384)data=JsonSerializer.Serialize(new{Truncated=true,Summary=data[..16384]});
            File.AppendAllText(Path,"{\"utc\":"+JsonSerializer.Serialize(DateTime.UtcNow)+",\"event\":"+JsonSerializer.Serialize(kind)+",\"data\":"+data+"}"+Environment.NewLine);
        }catch(IOException){}catch(UnauthorizedAccessException){}catch(System.Security.SecurityException){}
    }
}
