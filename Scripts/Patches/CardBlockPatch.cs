using System.Reflection;
using CardStats.Scripts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CardStats.Scripts.Patches;

[HarmonyPatch]
public static class CardBlockPatch
{
    private static MethodInfo? _afterBlockGainedMethod;
    
    public static MethodInfo TargetMethod()
    {
        _afterBlockGainedMethod ??= typeof(Hook).GetMethod("AfterBlockGained", BindingFlags.Public | BindingFlags.Static);
        return _afterBlockGainedMethod!;
    }

    public static void Postfix(CombatState combatState, Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
    {
        if (cardSource != null && creature?.IsPlayer == true && amount > 0)
        {
            int block = (int)amount;
            Log.Info($"[CardStats] AfterBlockGained - cardSource: {cardSource.Id}, creature: {creature?.IsPlayer}, amount: {block}");
            CardPlayStats.RecordBlock(cardSource, block);
        }
    }
}
