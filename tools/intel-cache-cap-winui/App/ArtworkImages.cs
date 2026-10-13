using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
namespace ArcCache;

public sealed class ArtworkConverter : IValueConverter {
    public object? Convert(object value,Type targetType,object parameter,string language) => value is IEnumerable<string> paths?ArtworkImages.Get(paths,48):null;
    public object ConvertBack(object value,Type targetType,object parameter,string language) => throw new NotSupportedException();
}
internal static class ArtworkImages {
    static readonly Dictionary<string,BitmapImage> cache=new(StringComparer.OrdinalIgnoreCase);
    public static BitmapImage? Get(IEnumerable<string> candidates,int width) {
        string[] paths=candidates.Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).Take(128).ToArray();
        if(paths.Length==0)return null;
        string key=width+"|"+string.Join("|",paths);
        if(cache.TryGetValue(key,out var existing))return existing;
        var bitmap=new BitmapImage{DecodePixelWidth=width};
        if(cache.Count>=256)cache.Clear();cache[key]=bitmap;_ = Load(bitmap,paths);return bitmap;
    }
    static async Task Load(BitmapImage bitmap,string[] paths) {
        foreach(string path in paths)try {
            var file=new FileInfo(path);if(!file.Exists||file.Length>16*1024*1024||(file.Attributes&FileAttributes.ReparsePoint)!=0)continue;
            using var stream=File.OpenRead(path);await bitmap.SetSourceAsync(stream.AsRandomAccessStream());return;
        }catch(Exception){/* Keep the placeholder and try the next cached asset. */}
    }
}
