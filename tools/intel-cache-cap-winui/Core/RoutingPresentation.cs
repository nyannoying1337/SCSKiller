namespace ArcCache.Core;

public static class RoutingPresentation {
    public static string Status(bool enabled, bool restartRequired) => restartRequired
        ? "Restart required — Windows has not finished applying the routing change."
        : enabled
            ? "On — required for games to use their private driver and selected cache limit."
            : "Off — enable this and restart Windows before using a private game setup.";
}
