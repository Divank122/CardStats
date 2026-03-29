using CardStats.Scripts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace CardStats;

[ModInitializer(nameof(Init))]
public static class Entry
{
    public static void Init()
    {
        MegaCrit.Sts2.Core.Logging.Log.Info("[CardStats] Mod initializing...");

        var harmony = new Harmony("CardStats");
        harmony.PatchAll();

        CardPlayStats.Initialize();

        MegaCrit.Sts2.Core.Logging.Log.Info("[CardStats] Mod initialized successfully! Patches applied.");
    }
}
