using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using CardStats.Scripts;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace CardStats.Scripts.Patches;

[HarmonyPatch]
public static class CardHoverTipPatch
{
    private static MethodInfo? _hoverTipsGetterMethod;
    private static FieldInfo? _titleField;
    private static FieldInfo? _descriptionField;
    private static FieldInfo? _iconField;
    private static FieldInfo? _idField;
    private static FieldInfo? _canonicalModelField;
    
    public static MethodInfo TargetMethod()
    {
        _hoverTipsGetterMethod ??= typeof(CardModel).GetProperty("HoverTips")?.GetGetMethod(nonPublic: false);
        return _hoverTipsGetterMethod!;
    }

    public static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (!CardPlayStats.IsCardInDeck(__instance))
        {
            return;
        }
        
        bool shouldShow = IsInPileScreen() ? CardPlayStats.ShowPilePlayCount : CardPlayStats.ShowPlayCount;
        if (!shouldShow)
        {
            return;
        }
        
        var list = new List<IHoverTip>(__result);
        
        var playCount = CardPlayStats.GetPlayCount(__instance);
        var damage = CardPlayStats.GetDamage(__instance);
        var block = CardPlayStats.GetBlock(__instance);
        var fatal = CardPlayStats.GetFatal(__instance);
        var debuff = CardPlayStats.GetDebuff(__instance);
        var draw = CardPlayStats.GetDraw(__instance);
        var buff = CardPlayStats.GetBuff(__instance);
        
        bool hasFatalEffect = HasFatalTip(list);
        
        Log.Info($"[CardStats] HoverTips for {__instance.Id}, playCount: {playCount}, damage: {damage}, block: {block}, fatal: {fatal}, debuff: {debuff}, draw: {draw}, buff: {buff}, hasFatalEffect: {hasFatalEffect}");
        
        var description = $"打出次数：{playCount}";
        if (damage > 0)
        {
            description += $"\n造成伤害：{damage}";
        }
        if (block > 0)
        {
            description += $"\n获得格挡：{block}";
        }
        if (hasFatalEffect && fatal > 0)
        {
            description += $"\n斩杀次数：{fatal}";
        }
        if (debuff > 0)
        {
            description += $"\n施加效果层数：{debuff}";
        }
        if (draw > 0)
        {
            description += $"\n抽牌数量：{draw}";
        }
        if (buff > 0)
        {
            description += $"\n获得效果层数：{buff}";
        }
        
        var tip = CreateHoverTip("卡牌统计", description, $"cardstats_{playCount}_{damage}_{block}_{fatal}_{debuff}_{draw}_{buff}");
        list.Add(tip);
        
        __result = list;
        Log.Info($"[CardStats] Added tooltip for {__instance.Id}");
    }
    
    private static bool HasFatalTip(List<IHoverTip> tips)
    {
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
    
    private static bool IsInPileScreen()
    {
        var tree = Engine.GetMainLoop();
        if (tree is SceneTree sceneTree)
        {
            var root = sceneTree.Root;
            if (root != null)
            {
                return CheckForPileScreen(root);
            }
        }
        return false;
    }
    
    private static bool CheckForPileScreen(Node node)
    {
        if (node.GetType().Name == "NCardPileScreen")
            return true;
        
        foreach (var child in node.GetChildren())
        {
            if (CheckForPileScreen(child))
                return true;
        }
        
        return false;
    }
    
    private static HoverTip CreateHoverTip(string title, string description, string id)
    {
        _titleField ??= typeof(HoverTip).GetField("<Title>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        _descriptionField ??= typeof(HoverTip).GetField("<Description>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        _iconField ??= typeof(HoverTip).GetField("<Icon>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        _idField ??= typeof(HoverTip).GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        _canonicalModelField ??= typeof(HoverTip).GetField("<CanonicalModel>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        
        object tip = FormatterServices.GetUninitializedObject(typeof(HoverTip));
        
        _titleField?.SetValue(tip, title);
        _descriptionField?.SetValue(tip, description);
        _iconField?.SetValue(tip, null);
        _idField?.SetValue(tip, id);
        _canonicalModelField?.SetValue(tip, null);
        
        return (HoverTip)tip;
    }
}
