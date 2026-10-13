namespace ArcCache.Core;

public sealed record ArtworkFiles(IReadOnlyList<string> Portraits,IReadOnlyList<string> Icons);
public static class SteamArtwork {
    static bool DirectoryOkay(string path) => Directory.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)==0;
    // Steam has both flat legacy assets and app-ID folders, with optional hash subfolders.
    // Bounded local reads only; no account access, image downloads, or recursive traversal.
    public static ArtworkFiles Find(string steamRoot,uint id) {
        var portraits=new List<string>();var icons=new List<string>();
        string root=Path.Combine(steamRoot,"appcache","librarycache");
        try {
            if(!DirectoryOkay(root))return new([],[]);
            string app=Path.Combine(root,id.ToString());
            var directories=new List<string>();
            if(DirectoryOkay(app)){directories.Add(app);directories.AddRange(Directory.EnumerateDirectories(app).Take(128).Where(DirectoryOkay).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase));}
            foreach(string name in new[]{"library_600x900.jpg","library_600x900.png","library_capsule.jpg","library_capsule.png","library_header.jpg","library_header.png","header.jpg","header.png"}) {
                Add(portraits,Path.Combine(root,id+"_"+name));
                foreach(string dir in directories)Add(portraits,Path.Combine(dir,name));
            }
            foreach(string name in new[]{"icon.jpg","icon.png"}){Add(icons,Path.Combine(root,id+"_"+name));foreach(string dir in directories)Add(icons,Path.Combine(dir,name));}
            if(DirectoryOkay(app))foreach(string file in Directory.EnumerateFiles(app).Take(256).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase)) {
                string name=Path.GetFileNameWithoutExtension(file);
                if(name.Length==40&&name.All(Uri.IsHexDigit))Add(icons,file);
            }
        } catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException){/* Missing/inaccessible artwork must never hide an installed title. */}
        return new(portraits.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),icons.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
    static void Add(List<string> result,string file) {
        try {
            if(!File.Exists(file)||(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)return;
            string ext=Path.GetExtension(file);if(!ext.Equals(".jpg",StringComparison.OrdinalIgnoreCase)&&!ext.Equals(".png",StringComparison.OrdinalIgnoreCase)&&!ext.Equals(".jpeg",StringComparison.OrdinalIgnoreCase))return;
            var info=new FileInfo(file);if(info.Length<8||info.Length>16*1024*1024)return;
            using var stream=File.OpenRead(file);Span<byte> header=stackalloc byte[8];if(stream.Read(header)!=8)return;
            bool png=header.SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}),jpeg=header[0]==255&&header[1]==216&&header[2]==255;
            if(png||jpeg)result.Add(Path.GetFullPath(file));
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException){ }
    }
}
