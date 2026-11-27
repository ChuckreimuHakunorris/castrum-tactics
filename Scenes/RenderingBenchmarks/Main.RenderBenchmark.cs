#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using CastrumTactics.AI;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using CastrumTactics.Match;
using CastrumTactics.RenderingBenchmarks;
using Godot;

public partial class Main
{
    private RenderBenchmarkRunner? _benchmark;
    private CpuMatchCoordinator.DecisionSource? _benchmarkCpu;
    private int? _benchmarkSeed;
    private MainPhaseState? _benchmarkFixture;
    private bool _benchmarkSynthetic;
    private bool _replaceAfterRestart;

    private bool TryStartRenderingBenchmark()
    {
        RenderBenchmarkRequest? request;
        try
        {
            request = RenderBenchmarkRequest.Take();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            GD.PushError($"Invalid rendering benchmark request: {exception.Message}");
            GetTree().Quit(1);
            return true;
        }

        if (request is null) return false;
        _benchmark = new RenderBenchmarkRunner(this, request);
        _board.BenchmarkSpriteSync = started => _benchmark.Mark("sprite-sync", started);
        AddChild(_benchmark);
        return true;
    }

    internal void PrepareBenchmarkCase(string scenario, RenderBenchmarkOptions options, RenderBenchmarkCapture capture)
    {
        bool cpu = scenario is "monster" or "extended";
        _benchmarkSeed = options.Seed;
        _benchmarkCpu = cpu ? capture.CpuSource(options.CpuDiagnostics) : null;
        _benchmarkFixture = null;
        _benchmarkSynthetic = RenderBenchmarkFixtures.IsSynthetic(scenario);
        _replaceAfterRestart = false;
        _background.MotionEnabled = options.AnimateAmbient;
        _board.ApplyPreferences(new PresentationPreferences(options.ShowOpponent, !options.AnimateLegal));
        _banner.SetReducedMotion(!options.AnimateTube);

        _configuration = new MatchConfiguration(cpu ? GameMode.VersusCpu : GameMode.LocalMultiplayer,
            difficulty: cpu ? CpuDifficulty.Monster : CpuDifficulty.Normal,
            ruleSet: cpu ? RuleSet.KnightSupport : RuleSet.Classic);
        foreach (var button in new[] { _playerOne, _playerTwo })
        {
            button.Disabled = cpu;
            button.MouseFilter = cpu ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
            button.FocusMode = cpu ? FocusModeEnum.None : FocusModeEnum.All;
            if (cpu)
            {
                button.AddThemeStyleboxOverride("disabled", button.GetThemeStylebox("normal"));
                button.AddThemeColorOverride("font_disabled_color", button.GetThemeColor("font_color"));
            }
            else
            {
                button.RemoveThemeStyleboxOverride("disabled");
                button.RemoveThemeColorOverride("font_disabled_color");
            }
        }

        _playerButtonMinimum = MeasurePlayerButtonMinimum(_playerOne, PlayerId.PlayerOne)
            .Max(MeasurePlayerButtonMinimum(_playerTwo, PlayerId.PlayerTwo));
        string mapId = scenario is "small" or "turns" or "lifecycle" ? "demonstration" : "great-divide";
        _mapChoice = PredefinedMapCatalog.BuiltIn.Get(mapId);
        StartMatch(_configuration);

        if (scenario == "damage" || _benchmarkSynthetic)
        {
            var state = _benchmarkFixture = _benchmarkSynthetic ? RenderBenchmarkFixtures.Create(scenario) : RenderBenchmarkFixtures.ReplayHeavyDamage();
            if (_benchmarkSynthetic) _board.SetVisuals(null);
            _board.SetMatchBoard(state.Board, state.GetLegalSelections(PlayerId.PlayerOne), state.GetLegalSelections(PlayerId.PlayerTwo),
                state.Territory, PlayerId.PlayerOne);
            _banner.ShowTerritory(state.Board, state.Territory);
            if (_benchmarkSynthetic) _activeMap.Text = "Synthetic benchmark — 12 × 12";
            _phase.Text = "Main · Benchmark";
            return;
        }

        if (scenario is not ("small" or "large")) return;
        while (_displayed.Phase == MatchPhase.Opening)
        {
            var starts = _displayed.EligiblePositions;
            BenchmarkSubmit(PlayerId.PlayerOne, starts[0]);
            BenchmarkSubmit(PlayerId.PlayerTwo, starts[1]);
        }
    }

    internal void AdvanceBenchmarkMatch(string scenario, int step)
    {
        if (_match.Cpu?.Failure is { } failure) throw new InvalidOperationException("Benchmark CPU failed.", failure);
        if (_replaceAfterRestart)
        {
            _replaceAfterRestart = false;
            _mapChoice = PredefinedMapCatalog.BuiltIn.Get(_mapChoice?.Id == "demonstration" ? "open-meadow" : "demonstration");
            long started = Stopwatch.GetTimestamp();
            StartMatch(_configuration);
            _benchmark?.Mark("replacement", started, _mapChoice.Id);
            return;
        }

        if (!MatchConfiguration.IsPlayable(_displayed.Phase))
        {
            long started = Stopwatch.GetTimestamp();
            var result = _match.Session!.RestartDraft(_displayed.RoundId);
            if (result != SessionCommandResult.Accepted) throw new InvalidOperationException($"Benchmark restart was rejected: {result}.");
            _benchmark?.Mark("restart", started);
            _replaceAfterRestart = scenario == "lifecycle";
            return;
        }

        if (_configuration.CpuPlayer.HasValue)
        {
            if (!_displayed.HasSubmitted(PlayerId.PlayerOne))
            {
                // Waiting for a slower CPU should change elapsed time, not the scripted choice for a resolved state.
                int round = _displayed.ResolvedRounds + _displayed.ResolvedMainTurns;
                BenchmarkSubmit(PlayerId.PlayerOne, BenchmarkChoice(PlayerId.PlayerOne, round));
            }
            return;
        }

        bool collide = scenario == "lifecycle" && _displayed.Phase == MatchPhase.Opening;
        var first = BenchmarkChoice(PlayerId.PlayerOne, step);
        var second = collide ? first : BenchmarkChoice(PlayerId.PlayerTwo, step + 1);
        BenchmarkSubmit(PlayerId.PlayerOne, first);
        BenchmarkSubmit(PlayerId.PlayerTwo, second);
    }

    private BoardPosition BenchmarkChoice(PlayerId player, int step)
    {
        var targets = _displayed.Phase == MatchPhase.Opening
            ? _displayed.EligiblePositions
            : _displayed.GetMainPhaseLegalSelections(player).OrderBy(p => p.Y).ThenBy(p => p.X).ToArray();
        return targets[step % targets.Count];
    }

    private void BenchmarkSubmit(PlayerId player, BoardPosition position)
    {
        long started = Stopwatch.GetTimestamp();
        var result = MatchConfiguration.Submit(_match.Session!, player, position, _displayed.Phase, _displayed.RoundId);
        if (result != SessionCommandResult.Accepted) throw new InvalidOperationException($"Benchmark selection was rejected: {result}.");
        _benchmark?.Mark("scripted-submit", started, $"{player}:{position.X},{position.Y}");
    }

    internal object BenchmarkBoardDigest()
    {
        var board = _benchmarkFixture?.Board ?? _displayed.Board;
        var one = _benchmarkFixture?.GetLegalSelections(PlayerId.PlayerOne) ?? _displayed.GetMainPhaseLegalSelections(PlayerId.PlayerOne);
        var two = _benchmarkFixture?.GetLegalSelections(PlayerId.PlayerTwo) ?? _displayed.GetMainPhaseLegalSelections(PlayerId.PlayerTwo);
        var cells = new byte[board.Width * board.Height * 4];
        int offset = 0, pieces = 0, damageMarkers = 0;
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
            {
                var cell = board.GetCell(new BoardPosition(x, y));
                cells[offset++] = (byte)cell.Terrain;
                cells[offset++] = (byte)cell.Piece;
                cells[offset++] = (byte)(cell.Owner ?? 0);
                cells[offset++] = (byte)cell.GroundHealth;
                if (cell.Piece != PieceType.None) pieces++;
                damageMarkers += TerrainCatalog.DamageMarkerCount(cell);
            }

        return new
        {
            map = _benchmarkSynthetic ? "synthetic" : _mapChoice?.Id,
            board.Width,
            board.Height,
            pieces,
            damageMarkers,
            phase = _benchmarkFixture is null ? _displayed.Phase.ToString() : "Main",
            rules = _configuration.RuleSet.ToString(),
            mode = _configuration.Mode.ToString(),
            difficulty = _configuration.CpuPlayer.HasValue ? _configuration.Difficulty.ToString() : null,
            round = _benchmarkFixture is null ? (long?)_displayed.RoundId : null,
            oneLegal = one.Count,
            twoLegal = two.Count,
            sharedLegal = one.Count(two.Contains),
            digest = Convert.ToHexString(SHA256.HashData(cells)),
        };
    }
}
