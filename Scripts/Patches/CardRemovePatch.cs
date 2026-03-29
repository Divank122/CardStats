using CardStats.Scripts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace CardStats.Scripts.Patches;

[HarmonyPatch(typeof(CardPileCmd), "RemoveFromDeck", typeof(IReadOnlyList<CardModel>), typeof(bool))]
public static class CardRemovePatch
{
    public static void Prefix(IReadOnlyList<CardModel> cards, bool showPreview)
    {
        CardPlayStats.PrepareForRemoval(cards);
    }

    public static void Postfix(IReadOnlyList<CardModel> cards, bool showPreview)
    {
        CardPlayStats.FinalizeRemoval();
    }
}
