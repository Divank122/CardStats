using CardStats.Scripts;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.addons.mega_text;

namespace CardStats.Scripts.Patches;

public static class HistoryStartTimeHolder
{
    public static long CurrentHistoryStartTime { get; private set; }
    
    public static void SetStartTime(long startTime)
    {
        CurrentHistoryStartTime = startTime;
    }
}

[HarmonyPatch(typeof(NRunHistory), "DisplayRun")]
public static class RunHistoryDisplayPatch
{
    public static void Prefix(RunHistory history)
    {
        Log.Info($"[CardStats] DisplayRun: StartTime={history.StartTime}");
        HistoryStartTimeHolder.SetStartTime(history.StartTime);
    }
}

[HarmonyPatch(typeof(NDeckHistoryEntry), "Reload")]
public static class DeckHistoryEntryReloadPatch
{
    private static readonly FontVariation? CachedFont = GD.Load<FontVariation>("res://themes/kreon_bold_glyph_space_one.tres");

    public static void Postfix(NDeckHistoryEntry __instance)
    {
        Log.Info("[CardStats] DeckHistoryEntryReloadPatch called");
        try
        {
            var card = __instance.Card;
            if (card == null)
            {
                Log.Info("[CardStats] DeckHistoryEntryReloadPatch: card is null");
                return;
            }
            
            var startTime = HistoryStartTimeHolder.CurrentHistoryStartTime;
            if (startTime == 0)
            {
                Log.Info("[CardStats] DeckHistoryEntryReloadPatch: startTime is 0");
                return;
            }
            
            Log.Info($"[CardStats] DeckHistoryEntryReloadPatch: card={card.Id}, startTime={startTime}");
            
            var stats = CardPlayStats.GetHistoryStats(startTime);
            var playCount = CardPlayStats.GetHistoryPlayCount(card, stats);
            
            var existingLabel = __instance.GetNodeOrNull<MegaLabel>("PlayCountLabel");
            if (existingLabel != null)
            {
                existingLabel.Text = $"打出:{playCount}";
                existingLabel.Visible = CardPlayStats.ShowHistoryPlayCount;
                return;
            }
            
            var label = new MegaLabel();
            label.Name = "PlayCountLabel";
            label.Text = $"打出:{playCount}";
            label.AddThemeColorOverride("font_color", new Color(0.937255f, 0.784314f, 0.317647f));
            label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.501961f));
            label.AddThemeConstantOverride("outline_size", 12);
            label.AddThemeFontSizeOverride("font_size", 16);
            if (CachedFont != null)
            {
                label.AddThemeFontOverride("font", CachedFont);
            }
            label.Position = new Vector2(133, 8);
            label.Visible = CardPlayStats.ShowHistoryPlayCount;
            label.AddToGroup("history_play_count_label");
            label.AutoSizeEnabled = false;
            label.MinFontSize = 16;
            label.MaxFontSize = 16;
            
            __instance.AddChild(label);
        }
        catch (System.Exception ex)
        {
            Log.Error($"[CardStats] Error in DeckHistoryEntryReloadPatch: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NRunHistory), "_Ready")]
public static class RunHistoryReadyPatch
{
    private static readonly FontVariation? CachedFont = GD.Load<FontVariation>("res://themes/kreon_bold_glyph_space_one.tres");

    public static void Postfix(NRunHistory __instance)
    {
        try
        {
            AddHistoryPlayCountCheckbox(__instance);
        }
        catch (System.Exception ex)
        {
            Log.Error($"[CardStats] Error in RunHistoryReadyPatch: {ex.Message}");
        }
    }

    private static void AddHistoryPlayCountCheckbox(NRunHistory instance)
    {
        var existingContainer = instance.GetNodeOrNull("HistoryPlayCountContainer");
        if (existingContainer != null) return;

        var tickboxScene = GD.Load<PackedScene>("res://scenes/ui/tickbox.tscn");
        if (tickboxScene == null) return;

        var container = new MarginContainer();
        container.Name = "HistoryPlayCountContainer";
        container.AnchorTop = 1.0f;
        container.AnchorBottom = 1.0f;
        container.OffsetLeft = 16;
        container.OffsetTop = -100;
        container.OffsetRight = 278;
        container.OffsetBottom = -36;
        container.Scale = new Vector2(0.75f, 0.75f);

        var marginContainer = new MarginContainer();
        marginContainer.AddThemeConstantOverride("margin_left", 6);
        marginContainer.AddThemeConstantOverride("margin_right", 6);
        container.AddChild(marginContainer);

        var clickHandler = new HistoryPlayCountClickHandler(tickboxScene, CachedFont);
        marginContainer.AddChild(clickHandler);

        instance.AddChild(container);
    }
}

public partial class HistoryPlayCountClickHandler : HBoxContainer
{
    private Control? _tickboxVisuals;
    private Control? _tickedImage;
    private Control? _notTickedImage;
    private bool _isTicked = true;
    private bool _lastShowState = true;

    public HistoryPlayCountClickHandler(PackedScene tickboxScene, FontVariation? font)
    {
        Name = "HistoryPlayCountTickbox";
        AddThemeConstantOverride("separation", 0);
        MouseFilter = MouseFilterEnum.Stop;

        _tickboxVisuals = tickboxScene.Instantiate<Control>();
        _tickboxVisuals.Name = "TickboxVisuals";
        _tickboxVisuals.MouseFilter = MouseFilterEnum.Pass;
        AddChild(_tickboxVisuals);

        var label = new MegaLabel();
        label.Name = "HistoryPlayCountLabel";
        label.Text = "查看打出次数";
        label.AddThemeColorOverride("font_color", new Color(0.937255f, 0.784314f, 0.317647f));
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.501961f));
        label.AddThemeConstantOverride("outline_size", 12);
        label.AddThemeFontSizeOverride("font_size", 27);
        if (font != null)
        {
            label.AddThemeFontOverride("font", font);
        }
        label.VerticalAlignment = VerticalAlignment.Center;
        label.MouseFilter = MouseFilterEnum.Pass;
        label.AutoSizeEnabled = false;
        label.MinFontSize = 28;
        label.MaxFontSize = 28;
        AddChild(label);
    }

    public override void _Ready()
    {
        if (_tickboxVisuals != null)
        {
            _tickedImage = _tickboxVisuals.GetNodeOrNull<Control>("Ticked");
            _notTickedImage = _tickboxVisuals.GetNodeOrNull<Control>("NotTicked");
        }

        _isTicked = CardPlayStats.ShowHistoryPlayCount;
        _lastShowState = _isTicked;
        UpdateVisuals();
    }

    public override void _Process(double delta)
    {
        if (CardPlayStats.ShowHistoryPlayCount != _lastShowState)
        {
            _lastShowState = CardPlayStats.ShowHistoryPlayCount;
            _isTicked = _lastShowState;
            UpdateVisuals();
            UpdateAllLabels();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
        {
            _isTicked = !_isTicked;
            _lastShowState = _isTicked;
            CardPlayStats.ShowHistoryPlayCount = _isTicked;
            UpdateVisuals();
            UpdateAllLabels();
            AcceptEvent();
        }
    }

    private void UpdateVisuals()
    {
        if (_tickedImage != null)
            _tickedImage.Visible = _isTicked;
        if (_notTickedImage != null)
            _notTickedImage.Visible = !_isTicked;
    }

    private static void UpdateAllLabels()
    {
        var sceneTree = Engine.GetMainLoop() as SceneTree;
        if (sceneTree == null) return;
        
        var labels = sceneTree.GetNodesInGroup("history_play_count_label");
        foreach (var node in labels)
        {
            if (node is MegaLabel label)
            {
                label.Visible = CardPlayStats.ShowHistoryPlayCount;
            }
        }
    }
}
