using CardStats.Scripts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace CardStats.Scripts.Patches;

[HarmonyPatch(typeof(CardCmd), "Upgrade", typeof(IEnumerable<CardModel>), typeof(CardPreviewStyle))]
public static class CardUpgradePatch
{
    public static void Prefix(IEnumerable<CardModel> cards)
    {
        foreach (var card in cards)
        {
            if (card == null || !card.IsUpgradable) continue;
            
            var oldKey = CardPlayStats.GetCardKey(card);
            if (CardPlayStats.HasPlayCount(oldKey))
            {
                var count = CardPlayStats.GetPlayCountByKey(oldKey);
                CardPlayStats.StorePendingUpgrade(card, oldKey, count);
            }
        }
    }

    public static void Postfix(IEnumerable<CardModel> cards)
    {
        foreach (var card in cards)
        {
            if (card == null) continue;
            
            var (oldKey, count) = CardPlayStats.GetAndClearPendingUpgradeForCard(card);
            if (count > 0)
            {
                CardPlayStats.RemovePlayCountByKey(oldKey);
                var newKey = CardPlayStats.GetCardKey(card);
                CardPlayStats.SetPlayCountByKey(newKey, count);
            }
        }
    }
}
