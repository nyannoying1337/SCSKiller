using System.Diagnostics;
using System.Text.Json;

namespace ArcCache.Core;
public sealed record WorkerRequest(string Action, string? Executable=null, string? ReceiptPath=null, string? DriverFolder=null, int MiB=512, bool Confirmed=false, string? SourceReceipt=null, uint SteamAppId=0, string? InstallDirectory=null, bool RiskConfirmed=false, bool ReadOnly=false);
public sealed record WorkerReply(bool Ok, string Message, JsonElement Data);
public sealed class SafetyClient(string workerPath) {
    public Action<string,object>? Diagnostic {get;set;}
    void Trace(string kind,object value){try{Diagnostic?.Invoke(kind,value);}catch{}}
    public async Task<WorkerReply> RunAsync(WorkerRequest request) {
        var watch=Stopwatch.StartNew();Trace("worker-request",request);
        try {
        var info=new ProcessStartInfo(workerPath) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using var process=Process.Start(info)??throw new IOException("The safety engine could not start.");
        Task<string> output=process.StandardOutput.ReadToEndAsync(),errors=process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(request));process.StandardInput.Close();
        await process.WaitForExitAsync();string raw=await output,stderr=await errors;
        if(string.IsNullOrWhiteSpace(raw))throw new IOException("The safety engine stopped without a result. "+stderr);
        var reply=JsonSerializer.Deserialize<WorkerReply>(raw,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new IOException("Invalid safety-engine response.");
        if(process.ExitCode!=0&&reply.Ok)throw new IOException("The engine exited unexpectedly. Inspect the recovery receipt before retrying.");
        Trace("worker-result",new{Action=request.Action,ElapsedMs=watch.ElapsedMilliseconds,ExitCode=process.ExitCode,reply.Ok,reply.Message,reply.Data,Stderr=stderr});
        return reply;
        }catch(Exception e){Trace("worker-error",new{Action=request.Action,ElapsedMs=watch.ElapsedMilliseconds,Error=e.ToString()});throw;}
    }
}
public sealed record ReceiptState(int Schema,string Executable,string Folder,int MiB,string Status,Dictionary<string,string> Files);
public sealed record GameSetupState(bool ReceiptExists,bool FolderExists,bool RecordValid,bool ContentsValid,ReceiptState? Receipt,string? Error,bool CompatibilityRepairRequired=false) {
    public bool Configured => !CompatibilityRepairRequired&&RecordValid&&ContentsValid&&FolderExists&&Receipt!=null;
    public bool CanPrepare => !FolderExists&&(!ReceiptExists||(RecordValid&&ContentsValid&&Receipt?.Status=="removed"));
    public static GameSetupState FromReply(WorkerReply reply) => reply.Data.Deserialize<GameSetupState>(new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("Missing setup state.");
}
public sealed class AppStore {
    public string Root {get;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"IntelArcCacheCap");
    public string GamesDirectory=>Path.Combine(Root,"games");
    public string LogPath {get;}
    public AppStore(){Directory.CreateDirectory(GamesDirectory);Directory.CreateDirectory(Path.Combine(Root,"logs"));LogPath=Path.Combine(Root,"logs",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-winui.log");}
    public string Receipt(uint appId)=>Path.Combine(GamesDirectory,"steam-"+appId+".json");
    public ReceiptState? Read(uint appId) => File.Exists(Receipt(appId))?JsonSerializer.Deserialize<ReceiptState>(File.ReadAllText(Receipt(appId)),new JsonSerializerOptions{PropertyNameCaseInsensitive=true}):null;
    public void Log(string message)=>File.AppendAllText(LogPath,DateTime.Now.ToString("HH:mm:ss ")+message+Environment.NewLine);
}
