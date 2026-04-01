using System.Reflection;
using System.Threading.Tasks;
using CardStats.Scripts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace CardStats.Scripts.Patches;

[HarmonyPatch]
public static class CardDrawPatch
{
    private static MethodInfo? _afterCardDrawnMethod;
    
    public static MethodInfo TargetMethod()
    {
        _afterCardDrawnMethod ??= typeof(Hook).GetMethod("AfterCardDrawn", BindingFlags.Public | BindingFlags.Static);
        return _afterCardDrawnMethod!;
    }

    public static void Postfix(CombatState combatState, PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (fromHandDraw) return;
        
        var sourceModel = choiceContext.LastInvolvedModel;
        if (sourceModel is CardModel sourceCard && CardPlayStats.IsCardInDeck(sourceCard))
        {
            Log.Info($"[CardStats] Recording draw for card {sourceCard.Id}");
            CardPlayStats.RecordDraw(sourceCard, 1);
        }
    }
}
