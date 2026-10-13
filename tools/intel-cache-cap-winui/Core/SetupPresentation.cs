namespace ArcCache.Core;

// Disk installation and runtime observation are independent states.
public sealed record SetupPresentation(string Installed, string Title, string Next, bool WaitingForControl) {
    public static string Limit(int mib) => mib >= 1024 ? $"{mib / 1024} GiB" : $"{mib} MiB";
    public static SetupPresentation From(GameSetupState? state, bool detected,bool routingReady=true,bool driverCompatible=true) {
        if(state?.CompatibilityRepairRequired==true)
            return new($"{Limit(state.Receipt!.MiB)} installed · compatibility update required", "Update the existing private setup", "Close the game. Choose Use system driver again, then prepare a fresh original 512 MiB copy. Changing the limit alone does not install the compatibility fix.", false);
        if (state?.Error != null || state?.FolderExists == true && !state.Configured)
            return new("Setup needs recovery", "Recover existing setup", "Open recovery options below. Import the original receipt before changing this setup.", false);
        if (state?.Configured == true && state.Receipt is { } receipt) {
            string installed = $"{Limit(receipt.MiB)} installed · private driver files verified";
            if(!driverCompatible)return new($"{Limit(receipt.MiB)} saved · driver review required","Review the updated driver","Check new DLLs above, or use the system driver again. Your saved limit is retained.",false);
            if(!routingReady)return new($"{Limit(receipt.MiB)} saved · routing inactive","Enable private drivers","Open Settings, enable Use private drivers and restart Windows. Your saved files remain; their presence does not prove the game loaded them.",false);
            if (!detected) return new(installed, "Game update needs attention", "Return to the system driver before detecting this game's updated executable.", false);
            bool control = receipt.MiB == 512 && receipt.Status != "original-control-loading-verified";
            if (control) return new(installed, "Step 2 of 3 · Test the original copy", "Play through Steam in DX12. Keep this app open; it checks loading automatically. Close the game when the check completes.", true);
            if (receipt.MiB == 512) return new(installed, "Step 3 of 3 · Choose and apply your limit", "Original loading checked. Close the game, choose a limit below, and apply it.", false);
            if(receipt.MiB>=8192)return new(installed,"Ready for experimental test","Launch in DX12 and enable the live overlay. Loading a large cache may use equally large RAM buffers. Use this experimental app to return to 4 GiB or the system driver.",false);
            return new(installed, "Ready to play", "Play through Steam in DX12. You can check the running game’s driver below.", false);
        }
        return detected
            ? new("System driver · no private setup", "Step 1 of 3 · Prepare this game", "Prepare an unchanged private copy first (about 608 MiB). Your selected limit is applied after the automatic loading check.", false)
            : new("System driver · no private setup", "Step 1 of 3 · Detect this game", "Play through Steam in DX12 and leave it at the menu. This app finds its rendering executable automatically.", false);
    }
}
