using CardStats.Scripts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

namespace CardStats.Scripts.Patches;

[HarmonyPatch(typeof(CardPileCmd), "RemoveFromDeck", typeof(IReadOnlyList<CardModel>), typeof(bool))]
public static class CardRemovePatch
{
    public static void Prefix(IReadOnlyList<CardModel> cards, bool showPreview)
    {
        CardPlayStats.PrepareForRemoval(cards);
        CardPlayStats.PrepareDamageForRemoval(cards);
        CardPlayStats.PrepareBlockForRemoval(cards);
        CardPlayStats.PrepareFatalForRemoval(cards);
        CardPlayStats.PrepareDebuffForRemoval(cards);
        CardPlayStats.PrepareDrawForRemoval(cards);
        CardPlayStats.PrepareBuffForRemoval(cards);
    }

    public static void Postfix(IReadOnlyList<CardModel> cards, bool showPreview)
    {
        CardPlayStats.FinalizeRemoval();
        CardPlayStats.FinalizeDamageRemoval();
        CardPlayStats.FinalizeBlockRemoval();
        CardPlayStats.FinalizeFatalRemoval();
        CardPlayStats.FinalizeDebuffRemoval();
        CardPlayStats.FinalizeDrawRemoval();
        CardPlayStats.FinalizeBuffRemoval();
    }
}
