using System.Reflection;
using CardStats.Scripts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace CardStats.Scripts.Patches;

[HarmonyPatch]
public static class CardBuffPatch
{
    private static MethodInfo? _afterPowerAmountChangedMethod;
    
    public static MethodInfo TargetMethod()
    {
        _afterPowerAmountChangedMethod ??= typeof(Hook).GetMethod("AfterPowerAmountChanged", BindingFlags.Public | BindingFlags.Static);
        return _afterPowerAmountChangedMethod!;
    }

    public static void Postfix(CombatState combatState, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (cardSource == null || applier == null) return;
        if (!applier.IsPlayer) return;
        if (power.Type != PowerType.Buff) return;
        if (!power.Owner.IsPlayer) return;
        if (amount <= 0) return;
        
        int stacks = (int)amount;
        Log.Info($"[CardStats] Buff applied - cardSource: {cardSource.Id}, power: {power.Id}, amount: {stacks}");
        CardPlayStats.RecordBuff(cardSource, stacks);
    }
}
