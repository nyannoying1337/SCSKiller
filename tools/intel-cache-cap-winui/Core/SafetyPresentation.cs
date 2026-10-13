using System.Text.Json;
namespace ArcCache.Core;
public sealed record SafetyPresentation(string Title,string Message,bool Warn) {
    public static SafetyPresentation From(JsonElement assessment) {
        if(assessment.GetProperty("Allowed").GetBoolean())return new("","",false);
        string reason=assessment.GetProperty("Message").GetString()??"";
        bool antiCheat=assessment.GetProperty("Marker").ValueKind==JsonValueKind.String||reason.Contains("Valve Anti-Cheat",StringComparison.OrdinalIgnoreCase);
        bool multiplayer=(assessment.GetProperty("Multiplayer").GetString()??"").Contains("multiplayer/co-op",StringComparison.OrdinalIgnoreCase);
        return new(antiCheat?"Anti-cheat detected":multiplayer?"Multiplayer game: extra risk":"Game safety could not be confirmed",
            "A modified driver may stop the game from starting or cause account penalties. Use the original driver for online play.",true);
    }
}
