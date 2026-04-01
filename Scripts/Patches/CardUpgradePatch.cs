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
            
            if (CardPlayStats.HasDamage(oldKey))
            {
                var damage = CardPlayStats.GetDamageByKey(oldKey);
                CardPlayStats.StorePendingUpgradeDamage(card, oldKey, damage);
            }
            
            if (CardPlayStats.HasBlock(oldKey))
            {
                var block = CardPlayStats.GetBlockByKey(oldKey);
                CardPlayStats.StorePendingUpgradeBlock(card, oldKey, block);
            }
            
            if (CardPlayStats.HasFatal(oldKey))
            {
                var fatal = CardPlayStats.GetFatalByKey(oldKey);
                CardPlayStats.StorePendingUpgradeFatal(card, oldKey, fatal);
            }
            
            if (CardPlayStats.HasDebuff(oldKey))
            {
                var debuff = CardPlayStats.GetDebuffByKey(oldKey);
                CardPlayStats.StorePendingUpgradeDebuff(card, oldKey, debuff);
            }
            
            if (CardPlayStats.HasDraw(oldKey))
            {
                var draw = CardPlayStats.GetDrawByKey(oldKey);
                CardPlayStats.StorePendingUpgradeDraw(card, oldKey, draw);
            }
            
            if (CardPlayStats.HasBuff(oldKey))
            {
                var buff = CardPlayStats.GetBuffByKey(oldKey);
                CardPlayStats.StorePendingUpgradeBuff(card, oldKey, buff);
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
            
            var (oldDamageKey, damage) = CardPlayStats.GetAndClearPendingUpgradeDamageForCard(card);
            if (damage > 0)
            {
                CardPlayStats.RemoveDamageByKey(oldDamageKey);
                var newKey = CardPlayStats.GetCardKey(card);
                CardPlayStats.SetDamageByKey(newKey, damage);
            }
            
            var (oldBlockKey, block) = CardPlayStats.GetAndClearPendingUpgradeBlockForCard(card);
            if (block > 0)
            {
                CardPlayStats.RemoveBlockByKey(oldBlockKey);
                var newKey = CardPlayStats.GetCardKey(card);
                CardPlayStats.SetBlockByKey(newKey, block);
            }
            
            var (oldFatalKey, fatal) = CardPlayStats.GetAndClearPendingUpgradeFatalForCard(card);
            if (fatal > 0)
            {
                CardPlayStats.RemoveFatalByKey(oldFatalKey);
                var newKey = CardPlayStats.GetCardKey(card);
                CardPlayStats.SetFatalByKey(newKey, fatal);
            }
            
            var (oldDebuffKey, debuff) = CardPlayStats.GetAndClearPendingUpgradeDebuffForCard(card);
            if (debuff > 0)
            {
                CardPlayStats.RemoveDebuffByKey(oldDebuffKey);
                var newKey = CardPlayStats.GetCardKey(card);
                CardPlayStats.SetDebuffByKey(newKey, debuff);
            }
            
            var (oldDrawKey, draw) = CardPlayStats.GetAndClearPendingUpgradeDrawForCard(card);
            if (draw > 0)
            {
                CardPlayStats.RemoveDrawByKey(oldDrawKey);
                var newKey = CardPlayStats.GetCardKey(card);
                CardPlayStats.SetDrawByKey(newKey, draw);
            }
            
            var (oldBuffKey, buff) = CardPlayStats.GetAndClearPendingUpgradeBuffForCard(card);
            if (buff > 0)
            {
                CardPlayStats.RemoveBuffByKey(oldBuffKey);
                var newKey = CardPlayStats.GetCardKey(card);
                CardPlayStats.SetBuffByKey(newKey, buff);
            }
        }
    }
}
