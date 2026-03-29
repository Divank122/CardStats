using CardStats.Scripts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.PauseMenu;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace CardStats.Scripts.Patches;

[HarmonyPatch(typeof(CombatHistory), "CardPlayFinished")]
public static class CardPlayPatch
{
    public static void Postfix(CombatState combatState, CardPlay cardPlay)
    {
        Log.Info($"[CardStats] CardPlayFinished Postfix called! cardPlay={(cardPlay != null ? "not null" : "null")}");
        if (cardPlay?.Card != null)
        {
            Log.Info($"[CardStats] Card played: {cardPlay.Card.Id}");
            CardPlayStats.RecordPlay(cardPlay.Card);
        }
        else
        {
            Log.Warn("[CardStats] CardPlay.Card is null!");
        }
    }
}

[HarmonyPatch(typeof(CombatManager), "SetUpCombat")]
public static class CombatSetUpPatch
{
    public static void Postfix(CombatState state)
    {
        Log.Info("[CardStats] CombatSetUp Postfix");
        CardPlayStats.OnCombatSetUp();
        
        var instance = CombatManager.Instance;
        if (instance != null)
        {
            instance.CombatEnded += OnCombatEnded;
        }
    }

    private static void OnCombatEnded(MegaCrit.Sts2.Core.Rooms.CombatRoom room)
    {
        Log.Info("[CardStats] CombatEnded event received");
        CardPlayStats.OnCombatEnded();
        
        var instance = CombatManager.Instance;
        if (instance != null)
        {
            instance.CombatEnded -= OnCombatEnded;
        }
    }
}

[HarmonyPatch(typeof(Hook), "AfterActEntered")]
public static class ActEnterPatch
{
    public static void Postfix(IRunState runState)
    {
        Log.Info($"[CardStats] Act entered, actIndex: {runState.CurrentActIndex}");
        if (runState.CurrentActIndex == 0)
        {
            CardPlayStats.Reset();
        }
    }
}

[HarmonyPatch(typeof(NPauseMenu), "CloseToMenu")]
public static class PauseMenuPatch
{
    public static bool Prefix()
    {
        Log.Info("[CardStats] Save and Quit - restoring from backup");
        CardPlayStats.RestoreFromBackup();
        return true;
    }
}

[HarmonyPatch(typeof(RunHistoryUtilities), "CreateRunHistoryEntry")]
public static class RunHistoryPatch
{
    public static void Postfix(SerializableRun run)
    {
        Log.Info($"[CardStats] Run history entry created, start time: {run.StartTime}");
        CardPlayStats.SetRunStartTime(run.StartTime);
        CardPlayStats.SaveToHistory();
    }
}
