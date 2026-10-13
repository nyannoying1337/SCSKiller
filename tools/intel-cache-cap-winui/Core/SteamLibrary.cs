using Microsoft.Win32;
using System.Text;

namespace ArcCache.Core;

public sealed record SteamGame(uint AppId, string Name, string InstallDirectory, string? Artwork, string? DetectedExecutable=null, bool Detected=false, string? Icon=null, IReadOnlyList<string>? ArtworkFallbacks=null) {
    public bool Supported => Detected;
    public string? SupportedExecutable => DetectedExecutable;
    public string SupportLabel => Detected?"Ready to set up":"Launch in DX12 to detect";
    public IReadOnlyList<string> ArtworkSources => (Artwork is null?Array.Empty<string>():new[]{Artwork}).Concat(ArtworkFallbacks??Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public IReadOnlyList<string> IconSources => (Icon is null?Array.Empty<string>():new[]{Icon}).Concat(ArtworkSources).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
public sealed record LibraryResult(IReadOnlyList<SteamGame> Games, IReadOnlyList<string> Warnings, string? SteamRoot);

// Valve text KeyValues: comments, escaped strings and nesting, without regex path extraction.
public static class KeyValues {
    readonly record struct Token(string Value,bool Brace);
    public static IReadOnlyList<string> Values(string input, string key) {
        if(input.Length > 4 * 1024 * 1024) throw new InvalidDataException("Steam metadata is unexpectedly large.");
        var result = new List<string>(); var tokens = Tokens(input).ToArray();
        int position = 0;
        void Parse(int depth, bool nested) {
            if(depth > 64) throw new InvalidDataException("Steam metadata nesting is too deep.");
            while(position < tokens.Length) {
                Token name = tokens[position++];
                if(name.Brace&&name.Value == "}") { if(!nested) throw new InvalidDataException("Unexpected closing brace."); return; }
                if(name.Brace || position == tokens.Length) throw new InvalidDataException("Invalid Steam metadata pair.");
                Token value = tokens[position++];
                if(value.Brace&&value.Value == "{") Parse(depth+1, true);
                else if(value.Brace) throw new InvalidDataException("Missing metadata value.");
                else if(name.Value.Equals(key, StringComparison.OrdinalIgnoreCase)) result.Add(value.Value);
            }
            if(nested) throw new InvalidDataException("Unclosed metadata object.");
        }
        Parse(0, false); return result;
    }
    static IEnumerable<Token> Tokens(string input) {
        for(int i=0;i<input.Length;) {
            char c=input[i]; if(char.IsWhiteSpace(c)){i++;continue;}
            if(c=='/'&&i+1<input.Length&&input[i+1]=='/'){while(i<input.Length&&input[i]!='\n')i++;continue;}
            if(c=='{'||c=='}'){i++;yield return new(c.ToString(),true);continue;}
            var value=new StringBuilder();
            if(c=='"') {
                i++;bool closed=false;
                while(i<input.Length) {
                    c=input[i++]; if(c=='"'){closed=true;break;}
                    if(c=='\\'&&i<input.Length) {
                        char next=input[i++];
                        if(next=='\\'||next=='"')value.Append(next);
                        else {value.Append('\\');value.Append(next);}
                    } else value.Append(c);
                }
                if(!closed)throw new InvalidDataException("Unclosed Steam metadata string.");
            } else {while(i<input.Length&&!char.IsWhiteSpace(input[i])&&input[i]!='{'&&input[i]!='}')value.Append(input[i++]);}
            yield return new(value.ToString(),false);
        }
    }
}

public static class SteamLibrary {
    public static LibraryResult Discover(string? root = null) {
        root ??= Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
        if(string.IsNullOrEmpty(root)||!Directory.Exists(root)) return new([], ["Steam was not found. Choose its folder in Settings."], root);
        root=Path.GetFullPath(root); var warnings=new List<string>(); var libraries=new HashSet<string>(StringComparer.OrdinalIgnoreCase){root};
        string vdf=Path.Combine(root,"steamapps","libraryfolders.vdf");
        try{if(File.Exists(vdf))foreach(string value in KeyValues.Values(File.ReadAllText(vdf),"path"))libraries.Add(Path.GetFullPath(value));}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException){warnings.Add("Some Steam libraries could not be read: "+e.Message);}
        var games=new Dictionary<uint,SteamGame>();
        foreach(string library in libraries) {
            string apps=Path.Combine(library,"steamapps"); if(!Directory.Exists(apps))continue;
            string[] manifests;
            try{manifests=Directory.GetFiles(apps,"appmanifest_*.acf");}catch(Exception e)when(e is IOException or UnauthorizedAccessException){warnings.Add(e.Message);continue;}
            foreach(string manifest in manifests) {
                try {
                    string text=File.ReadAllText(manifest);string? Field(string key)=>KeyValues.Values(text,key).FirstOrDefault();
                    if(!uint.TryParse(Field("appid"),out uint id)||!uint.TryParse(Field("StateFlags"),out uint state)||(state&4)==0)continue;
                    string? relative=Field("installdir");if(string.IsNullOrEmpty(relative)||Path.IsPathRooted(relative))continue;
                    string common=Path.GetFullPath(Path.Combine(apps,"common"))+Path.DirectorySeparatorChar;
                    string install=Path.GetFullPath(Path.Combine(common,relative));
                    if(!install.StartsWith(common,StringComparison.OrdinalIgnoreCase)||!Directory.Exists(install))continue;
                    var art=SteamArtwork.Find(root,id);
                    games[id]=new(id,Field("name")??relative,install,art.Portraits.FirstOrDefault(),Icon:art.Icons.FirstOrDefault(),ArtworkFallbacks:art.Portraits.Skip(1).ToArray());
                }catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException){warnings.Add(Path.GetFileName(manifest)+": "+e.Message);}
            }
        }
        return new(games.Values.OrderByDescending(g=>g.Supported).ThenBy(g=>g.Name,StringComparer.OrdinalIgnoreCase).ToArray(),warnings,root);
    }
}
