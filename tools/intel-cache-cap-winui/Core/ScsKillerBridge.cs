using System.Diagnostics;
using System.Text.Json;

namespace ArcCache.Core;

public sealed class ScsKillerBridge(string settingsPath) {
    public string? CliPath {get;private set;}
    public ScsKillerBridge Load() {
        if(File.Exists(settingsPath))CliPath=JsonSerializer.Deserialize<BridgeSettings>(File.ReadAllText(settingsPath))?.CliPath;
        return this;
    }
    public void SelectCli(string path) {
        path=Path.GetFullPath(path);
        if(!File.Exists(path)||!Path.GetFileName(path).Equals("scskiller.exe",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Select the SCSKiller CLI scskiller.exe, not the desktop app or a proxy DLL.");
        RequireConsole(path);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath,JsonSerializer.Serialize(new BridgeSettings(path)));CliPath=path;
    }
    public static ProcessStartInfo Command(string cli,string action,uint appId) {
        if(action is not ("status" or "compile")||appId==0)throw new ArgumentException("Only a selected Steam game's status or compile command is supported.");
        var info=new ProcessStartInfo(Path.GetFullPath(cli)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true,WorkingDirectory=Path.GetDirectoryName(Path.GetFullPath(cli))!};
        info.ArgumentList.Add(action);info.ArgumentList.Add("steam:"+appId);info.ArgumentList.Add("--community");
        // Explicit experimental Intel consent is obtained by the UI for each invocation.
        // Child-only; never changes persistent environment, settings, credentials or safety policy.
        info.Environment["SCSKILLER_EXPERIMENTAL_INTEL"]="1";
        return info;
    }
    public async Task<int> RunAsync(string action,uint appId,Action<string> progress,CancellationToken cancel) {
        if(CliPath==null||!File.Exists(CliPath))throw new FileNotFoundException("Choose the installed SCSKiller CLI in Settings first.");
        RequireConsole(CliPath);
        using var process=Process.Start(Command(CliPath,action,appId))??throw new IOException("SCSKiller could not start.");
        process.StandardInput.Close(); // No implicit affirmative answers to backend prompts.
        using var registration=cancel.Register(()=>{try{if(!process.HasExited)process.Kill(entireProcessTree:true);}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}});
        async Task Read(StreamReader reader,string prefix) {
            while(await reader.ReadLineAsync() is {} line)progress(prefix+(line.Length>8192?line[..8192]+" [truncated]":line));
        }
        await Task.WhenAll(Read(process.StandardOutput,"SCSKiller: "),Read(process.StandardError,"SCSKiller log: "),process.WaitForExitAsync());
        cancel.ThrowIfCancellationRequested();return process.ExitCode;
    }
    static void RequireConsole(string path) {
        using var input=new BinaryReader(File.OpenRead(path));
        if(input.BaseStream.Length<256||input.ReadUInt16()!=0x5A4D)throw new InvalidDataException("Selected file is not a Windows CLI executable.");
        input.BaseStream.Position=0x3C;uint header=input.ReadUInt32();
        if(header>input.BaseStream.Length-96)throw new InvalidDataException("Invalid executable header.");
        input.BaseStream.Position=header;if(input.ReadUInt32()!=0x4550)throw new InvalidDataException("Invalid PE executable.");
        input.BaseStream.Position=header+24;ushort magic=input.ReadUInt16();
        input.BaseStream.Position=header+24+68;
        if((magic!=0x10B&&magic!=0x20B)||input.ReadUInt16()!=3)throw new InvalidDataException("This is the desktop app. Select SCSKiller's console CLI build instead.");
    }
    sealed record BridgeSettings(string CliPath);
}
