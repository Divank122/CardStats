using CardStats.Scripts;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.addons.mega_text;

namespace CardStats.Scripts.Patches;

[HarmonyPatch(typeof(NCardPileScreen), "_Ready")]
public static class CardPileScreenReadyPatch
{
    private static readonly FontVariation? CachedFont = GD.Load<FontVariation>("res://themes/kreon_bold_glyph_space_one.tres");

    public static void Postfix(NCardPileScreen __instance)
    {
        var existingContainer = __instance.GetNodeOrNull("PilePlayCountContainer");
        if (existingContainer != null) return;

        var tickboxScene = GD.Load<PackedScene>("res://scenes/ui/tickbox.tscn");
        if (tickboxScene == null) return;

        var container = new MarginContainer();
        container.Name = "PilePlayCountContainer";
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

        var clickHandler = new PilePlayCountClickHandler(tickboxScene, CachedFont);
        marginContainer.AddChild(clickHandler);

        __instance.AddChild(container);
    }
}

public partial class PilePlayCountClickHandler : HBoxContainer
{
    private Control? _tickboxVisuals;
    private Control? _tickedImage;
    private Control? _notTickedImage;
    private bool _isTicked = true;
    private bool _lastShowState = true;

    public PilePlayCountClickHandler(PackedScene tickboxScene, FontVariation? font)
    {
        Name = "PilePlayCountTickbox";
        AddThemeConstantOverride("separation", 0);
        MouseFilter = MouseFilterEnum.Stop;

        _tickboxVisuals = tickboxScene.Instantiate<Control>();
        _tickboxVisuals.Name = "TickboxVisuals";
        _tickboxVisuals.MouseFilter = MouseFilterEnum.Pass;
        AddChild(_tickboxVisuals);

        var label = new MegaLabel();
        label.Name = "PilePlayCountLabel";
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

        _isTicked = CardPlayStats.ShowPilePlayCount;
        _lastShowState = _isTicked;
        UpdateVisuals();
    }

    public override void _Process(double delta)
    {
        if (CardPlayStats.ShowPilePlayCount != _lastShowState)
        {
            _lastShowState = CardPlayStats.ShowPilePlayCount;
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
            CardPlayStats.ShowPilePlayCount = _isTicked;
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
