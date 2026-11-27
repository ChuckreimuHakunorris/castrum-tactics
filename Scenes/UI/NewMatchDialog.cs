#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using CastrumTactics.AI;
using CastrumTactics.Domain;
using Godot;

internal partial class NewMatchDialog : ConfirmationDialog
{
    private readonly List<(PredefinedMap? Map, Button Button)> _entries = [];
    private Label _prompt = null!;
    private LineEdit _filter = null!;
    private BoxContainer _body = null!;
    private ScrollContainer _scroll = null!;
    private Label _caption = null!;
    private Label _previewDetail = null!;
    private MapPreview _preview = null!;
    private OptionButton _difficulty = null!;
    private CpuDifficulty _originalDifficulty;
    private Rect2 _available;

    internal PredefinedMap? MapChoice { get; private set; }
    internal CpuDifficulty SelectedDifficulty => _difficulty.Visible
        ? (CpuDifficulty)_difficulty.GetSelectedId() : _originalDifficulty;

    internal static string Describe(PredefinedMap map) => $"{map.Name} — {map.Definition.Width} × {map.Definition.Height}";

    private Func<PredefinedMap, BoardVisuals?> _loadVisuals = null!;

    internal void Initialize(bool versusCpu, Func<PredefinedMap, BoardVisuals?> loadVisuals)
    {
        _loadVisuals = loadVisuals;
        GetOkButton().ThemeTypeVariation = "PrimaryButton";

        // Bound the content independently of the window footer, including while wrapped text is remeasured.
        var content = new ScrollContainer
        {
            Name = "Content",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true
        };
        AddChild(content);
        var settings = new VBoxContainer
        {
            Name = "Settings",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        settings.AddThemeConstantOverride("separation", 6);
        content.AddChild(settings);
        _prompt = new Label { Name = "Prompt", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        settings.AddChild(_prompt);
        _body = new BoxContainer { Name = "MapSelection", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _body.AddThemeConstantOverride("separation", 8);
        settings.AddChild(_body);

        BuildMapList();
        BuildPreview(versusCpu);
    }

    private void BuildMapList()
    {
        var picker = new VBoxContainer
        {
            Name = "Picker",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        picker.AddThemeConstantOverride("separation", 4);
        _body.AddChild(picker);
        _filter = new LineEdit { Name = "Filter", PlaceholderText = "Filter maps", AccessibilityName = "Filter maps" };
        picker.AddChild(_filter);
        _scroll = new ScrollContainer
        {
            Name = "Scroll",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowAlways,
            FollowFocus = true,
            ScrollDeadzone = 8
        };
        picker.AddChild(_scroll);
        var entries = new VBoxContainer
        {
            Name = "Entries",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        entries.AddThemeConstantOverride("separation", 3);
        _scroll.AddChild(entries);
        var group = new ButtonGroup();
        AddEntry(null);
        foreach (PredefinedMap map in PredefinedMapCatalog.BuiltIn.Maps.OrderBy(map => map.Name, StringComparer.OrdinalIgnoreCase))
            AddEntry(map);
        _filter.TextChanged += UpdateFilter;

        void AddEntry(PredefinedMap? map)
        {
            // The list needs the row's press and drag events to start scrolling and cancel the pending button tap.
            var button = new Button
            {
                Name = map?.Id ?? "random",
                Text = map is null ? "Random Map" : Describe(map),
                Alignment = HorizontalAlignment.Left,
                ToggleMode = true,
                ButtonGroup = group,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Pass
            };
            button.AddThemeFontSizeOverride("font_size", 14);
            entries.AddChild(button);
            _entries.Add((map, button));
            button.Pressed += () => Select(map);
        }
    }

    private void BuildPreview(bool versusCpu)
    {
        var details = new VBoxContainer
        {
            Name = "Preview",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        details.AddThemeConstantOverride("separation", 3);
        _body.AddChild(details);
        _caption = new Label
        {
            Name = "Caption",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = "Heading"
        };
        _caption.AddThemeFontSizeOverride("font_size", 16);
        details.AddChild(_caption);
        _preview = new MapPreview
        {
            Name = "Board",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _preview.Resized += _preview.QueueRedraw;
        details.AddChild(_preview);
        _previewDetail = new Label
        {
            Name = "Detail",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = "MatchInstruction"
        };
        details.AddChild(_previewDetail);

        _difficulty = new OptionButton
        {
            Name = "DifficultyChoice",
            Visible = versusCpu,
            AccessibilityName = "Difficulty for the next match"
        };
        // Keep the CPU controls in the preview column so the landscape list can use the full height.
        details.AddChild(_difficulty);
        foreach (CpuDifficulty difficulty in Enum.GetValues<CpuDifficulty>())
            _difficulty.AddItem($"CPU difficulty · {MatchCaptions.DifficultyName(difficulty)}", (int)difficulty);
    }

    internal void Open(PredefinedMap? map, CpuDifficulty difficulty, bool unfinished)
    {
        _originalDifficulty = difficulty;
        _difficulty.Select(_difficulty.GetItemIndex((int)difficulty));
        _prompt.Text = unfinished ? "Leave this match and choose a battlefield?" : "Choose a battlefield.";
        _filter.Clear();
        UpdateFilter("");
        Select(map);
        Place();
        Popup();
        FocusSelectionAfterLayout(map);
    }

    internal void Fit(Rect2 available, Vector2 target)
    {
        bool wasVisible = Visible;
        _available = available;
        _body.Vertical = available.Size.X < 520 && available.Size.Y >= 445;
        _filter.CustomMinimumSize = target;
        _preview.CustomMinimumSize = _body.Vertical ? new Vector2(0, 88) : Vector2.Zero;
        _difficulty.CustomMinimumSize = target;
        GetOkButton().CustomMinimumSize = target;
        GetCancelButton().CustomMinimumSize = target;
        foreach (var (_, button) in _entries) button.CustomMinimumSize = target;
        if (wasVisible) Place();
    }

    private void Place()
    {
        // Reserve room above the embedded window for its title bar.
        var title = new Vector2(0, GetThemeConstant("title_height", "Window"));
        var area = new Rect2(_available.Position + title, _available.Size - title);
        Vector2 desired = _body.Vertical ? new(410, 660) : new(670, 560);
        Size = (Vector2I)desired.Min(area.Size).Max(Vector2.One);
        Position = (Vector2I)(area.GetCenter() - (Vector2)Size / 2);
    }

    private void Select(PredefinedMap? map)
    {
        MapChoice = map;
        _entries.First(entry => entry.Map == map).Button.SetPressedNoSignal(true);
        _caption.Text = map is null ? "Random Map" : Describe(map);
        _preview.ShowMap(map?.Definition, map is null ? null : _loadVisuals(map));
        _preview.Visible = map is not null;
        _previewDetail.SizeFlagsVertical = map is null ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill;
        _previewDetail.Text = map is null ? "The battlefield is chosen when the match begins."
            : $"Gold dots mark {map.Definition.StartingPositions.Count} draft positions.";
    }

    private async void FocusSelectionAfterLayout(PredefinedMap? map)
    {
        // Opening the window changes row widths and can reflow the list.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!IsInsideTree() || !Visible || MapChoice != map || _filter.HasFocus()) return;
        var button = _entries.First(entry => entry.Map == map).Button;
        if (!button.Visible) return;
        button.GrabFocus();
        _scroll.ScrollVertical = Mathf.RoundToInt(button.Position.Y);
    }

    private void UpdateFilter(string text)
    {
        string query = text.Trim();
        foreach (var (map, button) in _entries)
            button.Visible = map is null || button.Text.Contains(query, StringComparison.OrdinalIgnoreCase);
        _scroll.ScrollVertical = 0;
    }
}
