using CardStats.Scripts;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.addons.mega_text;

namespace CardStats.Scripts.Patches;

[HarmonyPatch(typeof(NGridCardHolder), "Create")]
public static class GridCardHolderCreatePatch
{
    private static readonly FontVariation? CachedFont = GD.Load<FontVariation>("res://themes/kreon_bold_glyph_space_one.tres");

    public static void Postfix(NCard cardNode, NGridCardHolder __result)
    {
        if (__result == null || cardNode?.Model == null) return;

        var cardModel = cardNode.Model;
        
        var existingLabel = __result.GetNodeOrNull<CardStatsLabel>("CardStatsLabel");
        
        if (!cardModel.FloorAddedToDeck.HasValue)
        {
            if (existingLabel != null)
            {
                existingLabel.QueueFree();
            }
            return;
        }

        var playCount = CardPlayStats.GetPlayCount(cardModel);

        if (existingLabel == null)
        {
            var statsLabel = CardStatsLabel.Create(playCount, CachedFont);
            __result.AddChild(statsLabel);
        }
        else
        {
            existingLabel.UpdateStats(playCount);
        }
    }
}

[HarmonyPatch(typeof(NCardsViewScreen), "ConnectSignals")]
public static class CardsViewScreenConnectPatch
{
    private static readonly FontVariation? CachedFont = GD.Load<FontVariation>("res://themes/kreon_bold_glyph_space_one.tres");

    public static void Postfix(NCardsViewScreen __instance)
    {
        var viewUpgradesContainer = __instance.GetNodeOrNull<MarginContainer>("ViewUpgrades");
        if (viewUpgradesContainer == null) return;

        var existingPlayCountContainer = __instance.GetNodeOrNull("ViewPlayCount");
        if (existingPlayCountContainer != null) return;

        var tickboxScene = GD.Load<PackedScene>("res://scenes/ui/tickbox.tscn");
        if (tickboxScene == null) return;

        var playCountContainer = new MarginContainer();
        playCountContainer.Name = "ViewPlayCount";
        playCountContainer.AnchorTop = 1.0f;
        playCountContainer.AnchorBottom = 1.0f;
        playCountContainer.OffsetLeft = 16;
        playCountContainer.OffsetTop = -140;
        playCountContainer.OffsetRight = 278;
        playCountContainer.OffsetBottom = -76;
        playCountContainer.Scale = new Vector2(0.75f, 0.75f);

        var marginContainer = new MarginContainer();
        marginContainer.AddThemeConstantOverride("margin_left", 6);
        marginContainer.AddThemeConstantOverride("margin_right", 6);
        playCountContainer.AddChild(marginContainer);

        var clickHandler = new CardStatsClickHandler(tickboxScene, CachedFont);
        marginContainer.AddChild(clickHandler);

        __instance.AddChild(playCountContainer);
    }
}

public partial class CardStatsClickHandler : HBoxContainer
{
    private Control? _tickboxVisuals;
    private Control? _tickedImage;
    private Control? _notTickedImage;
    private bool _isTicked = true;
    private bool _lastShowState = true;

    public CardStatsClickHandler(PackedScene tickboxScene, FontVariation? font)
    {
        Name = "PlayCountTickbox";
        AddThemeConstantOverride("separation", 0);
        MouseFilter = MouseFilterEnum.Stop;

        _tickboxVisuals = tickboxScene.Instantiate<Control>();
        _tickboxVisuals.Name = "TickboxVisuals";
        _tickboxVisuals.MouseFilter = MouseFilterEnum.Pass;
        AddChild(_tickboxVisuals);

        var label = new MegaLabel();
        label.Name = "PlayCountLabel";
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

        _isTicked = CardPlayStats.ShowPlayCount;
        _lastShowState = _isTicked;
        UpdateVisuals();
    }

    public override void _Process(double delta)
    {
        if (CardPlayStats.ShowPlayCount != _lastShowState)
        {
            _lastShowState = CardPlayStats.ShowPlayCount;
            _isTicked = _lastShowState;
            UpdateVisuals();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
        {
            _isTicked = !_isTicked;
            _lastShowState = _isTicked;
            CardPlayStats.ShowPlayCount = _isTicked;
            UpdateVisuals();
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
}

public partial class CardStatsLabel : Control
{
    private MegaLabel? _label;
    private int _pendingCount = 0;
    private bool _initialized = false;
    private readonly FontVariation? _cachedFont;
    private bool _lastVisibleState = true;

    private CardStatsLabel(FontVariation? font)
    {
        _cachedFont = font;
    }

    public override void _Ready()
    {
        _label = new MegaLabel();
        _label.Name = "StatsLabel";
        _label.Text = $"打出:{_pendingCount}";
        _label.AddThemeColorOverride("font_color", new Color(0.937255f, 0.784314f, 0.317647f));
        _label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.501961f));
        _label.AddThemeConstantOverride("outline_size", 12);
        _label.AddThemeFontSizeOverride("font_size", 20);
        if (_cachedFont != null)
        {
            _label.AddThemeFontOverride("font", _cachedFont);
        }
        _label.Position = new Vector2(-40, -245);
        _label.AutoSizeEnabled = false;
        _label.MinFontSize = 20;
        _label.MaxFontSize = 20;
        AddChild(_label);
        
        _initialized = true;
        _lastVisibleState = GetCurrentVisibility();
        Visible = _lastVisibleState;
    }

    public override void _Process(double delta)
    {
        if (!_initialized) return;
        
        var currentVisibility = GetCurrentVisibility();
        if (currentVisibility != _lastVisibleState)
        {
            _lastVisibleState = currentVisibility;
            Visible = _lastVisibleState;
        }
    }

    private bool GetCurrentVisibility()
    {
        return IsInPileScreen() ? CardPlayStats.ShowPilePlayCount : CardPlayStats.ShowPlayCount;
    }

    private bool IsInPileScreen()
    {
        var parent = GetParent();
        while (parent != null)
        {
            if (parent.GetType().Name == "NCardPileScreen")
                return true;
            parent = parent.GetParent();
        }
        return false;
    }

    public void UpdateStats(int playCount)
    {
        _pendingCount = playCount;
        if (_label != null)
        {
            _label.Text = $"打出:{playCount}";
        }
    }

    public static CardStatsLabel Create(int initialCount, FontVariation? font)
    {
        var label = new CardStatsLabel(font);
        label.Name = "CardStatsLabel";
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        label._pendingCount = initialCount;
        return label;
    }
}
