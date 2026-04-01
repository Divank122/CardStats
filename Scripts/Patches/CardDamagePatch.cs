using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CardStats.Scripts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace CardStats.Scripts.Patches;

[HarmonyPatch]
public static class CardDamagePatch
{
    private static MethodInfo? _afterDamageGivenMethod;
    
    public static MethodInfo TargetMethod()
    {
        _afterDamageGivenMethod ??= typeof(Hook).GetMethod("AfterDamageGiven", BindingFlags.Public | BindingFlags.Static);
        return _afterDamageGivenMethod!;
    }

    public static void Postfix(PlayerChoiceContext choiceContext, CombatState combatState, Creature? dealer, DamageResult results, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (cardSource == null || dealer == null || target == null) return;
        if (!dealer.IsPlayer || target.IsPlayer) return;
        
        int totalDamage = results.TotalDamage;
        Log.Info($"[CardStats] AfterDamageGiven - cardSource: {cardSource.Id}, dealer: {dealer.IsPlayer}, target: {target.IsPlayer}, totalDamage: {totalDamage}, unblocked: {results.UnblockedDamage}, blocked: {results.BlockedDamage}");
        
        if (totalDamage > 0)
        {
            Log.Info($"[CardStats] Recording damage: {totalDamage} for card {cardSource.Id}");
            CardPlayStats.RecordDamage(cardSource, totalDamage);
        }
        
        if (results.WasTargetKilled && HasFatalEffect(cardSource))
        {
            var powers = target.Powers;
            if (powers != null)
            {
                bool shouldTriggerFatal = powers.All(p => p.ShouldOwnerDeathTriggerFatal());
                if (shouldTriggerFatal)
                {
                    Log.Info($"[CardStats] Recording fatal for card {cardSource.Id}");
                    CardPlayStats.RecordFatal(cardSource);
                }
            }
        }
    }
    
    private static bool HasFatalEffect(CardModel card)
    {
        var tips = card.HoverTips;
        if (tips == null) return false;
        
        foreach (var tip in tips)
        {
            if (tip.Id != null && tip.Id.Contains("Fatal"))
            {
                return true;
            }
        }
        return false;
    }
}
