using System.Threading.Channels;
using CastrumTactics.AI;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using CastrumTactics.Match;
using Xunit;
using Xunit.Abstractions;

namespace CastrumTactics.Presentation.Tests;

public sealed class CpuMatchCoordinatorTests(ITestOutputHelper output)
{
    private const PlayerId Human = PlayerId.PlayerOne;
    private const PlayerId Cpu = PlayerId.PlayerTwo;
    private static readonly BoardPosition TopLeft = new(0, 0);
    private static readonly BoardPosition BottomRight = new(2, 2);
    private static readonly CpuProfile Normal = CpuDifficultyProfiles.Get(CpuDifficulty.Normal);
    private static readonly TimeSpan DeadlockLimit = TimeSpan.FromSeconds(10);

    [Fact]
    public void DefaultAndLocalConfigurationHaveTheExpectedInputOwners()
    {
        var defaults = new MatchConfiguration();
        Assert.Equal(GameMode.VersusCpu, defaults.Mode);
        Assert.Equal(CpuDifficulty.Normal, defaults.Difficulty);
        Assert.Equal(RuleSet.Classic, defaults.RuleSet);
        Assert.True(defaults.AllowsHumanInput(Human));
        Assert.False(defaults.AllowsHumanInput(Cpu));
        var swapped = new MatchConfiguration(humanPlayer: Cpu);
        Assert.Equal(Human, swapped.CpuPlayer);
        Assert.True(swapped.AllowsHumanInput(Cpu));
        var local = new MatchConfiguration(GameMode.LocalMultiplayer);
        Assert.Null(local.CpuPlayer);
        Assert.True(local.AllowsHumanInput(Human));
        Assert.True(local.AllowsHumanInput(Cpu));
        Assert.False(local.AllowsHumanInput((PlayerId)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MatchConfiguration((GameMode)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MatchConfiguration(humanPlayer: (PlayerId)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MatchConfiguration(difficulty: (CpuDifficulty)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MatchConfiguration(ruleSet: (RuleSet)99));
    }

    [Fact]
    public void LaunchOverridesComposeAndInvalidArgumentsAreRejected()
    {
        var editor = new MatchConfiguration(difficulty: CpuDifficulty.Hard);
        var launch = MatchLaunchOptions.Parse(["--map", "pocket-meadow", "--mode", "local", "--seed", "-5",
            "--ruleset", "knight-support"],
            editor, "demonstration", 1);
        Assert.Equal((GameMode.LocalMultiplayer, CpuDifficulty.Hard), (launch.Configuration.Mode, launch.Configuration.Difficulty));
        Assert.Equal(RuleSet.KnightSupport, launch.Configuration.RuleSet);
        Assert.Equal(RuleSet.KnightSupport, MatchLaunchOptions.Parse([], launch.Configuration, null, null).Configuration.RuleSet);
        Assert.Equal(RuleSet.Classic, MatchLaunchOptions.Parse(["--ruleset", "classic"], launch.Configuration, null, null)
            .Configuration.RuleSet);
        Assert.Equal("pocket-meadow", launch.SelectMap().Id);
        Assert.Equal(-5, launch.Seed);
        Assert.Equal("demonstration", MatchLaunchOptions.Parse([], editor, "demonstration", null).SelectMap().Id);
        Assert.Throws<KeyNotFoundException>(() => MatchLaunchOptions.Parse(["--map", "missing"], editor, null, null));
        string[][] invalid =
        [
            ["--mode", "unknown"], ["--mode"], ["--map", "--mode", "local"], ["--seed", "2147483648"],
            ["--map", "pocket-meadow", "--map", "demonstration"], ["--unknown"], ["--ruleset"],
            ["--ruleset", "unsupported"], ["--ruleset", "classic", "--ruleset", "knight-support"]
        ];
        Assert.All(invalid, args => Assert.Throws<ArgumentException>(() => MatchLaunchOptions.Parse(args, editor, null, null)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OpeningAndMainUseDeferredCommandsInEitherSubmissionOrder(bool cpuFirst)
    {
        var session = CreateSession();
        var source = new ControlledCpu();
        using var coordinator = Start(session, source);
        Invocation opening = await source.Next();
        Task openingWork = coordinator.PendingWork!;
        int? publishingThread = null;
        if (cpuFirst)
        {
            session.StateChanged += RecordCpuSubmission;
            opening.Choose(BottomRight);
            await openingWork.WaitAsync(DeadlockLimit);
            Assert.False(session.Current.HasSubmitted(Cpu));
            int owningThread = Environment.CurrentManagedThreadId;
            coordinator.Pump(session);
            Assert.Equal(owningThread, publishingThread);
            Assert.True(session.Current.HasSubmitted(Cpu));
            Assert.Same(opening.Snapshot.Board, session.Current.Board);
        }
        else
        {
            Submit(session, Human, BottomRight);
            Assert.Same(openingWork, coordinator.PendingWork);
            Assert.False(opening.Token.IsCancellationRequested);
            // Even a completion during StateChanged cannot issue a reentrant command.
            session.StateChanged += FinishWorkDuringRevision;
        }

        Submit(session, Human, TopLeft);
        if (!cpuFirst)
        {
            Assert.Equal(MatchPhase.Opening, session.Current.Phase);
            Assert.False(session.Current.HasSubmitted(Cpu));
            coordinator.Pump(session);
        }
        Assert.Equal(MatchPhase.Main, session.Current.Phase);
        Assert.Equal(Human, session.Current.Board.GetCell(TopLeft).Owner);
        Assert.Equal(Cpu, session.Current.Board.GetCell(BottomRight).Owner);
        Invocation main = await source.Next();
        Assert.Equal(MatchPhase.Main, main.Snapshot.Phase);
        Assert.False(main.Snapshot.HasSubmitted(Human));
        Assert.NotSame(opening.Random, main.Random);
        main.Choose(new(1, 1));
        await coordinator.PendingWork!.WaitAsync(DeadlockLimit);
        coordinator.Pump(session);
        Assert.True(session.Current.HasSubmitted(Cpu));
        Assert.Equal(PieceType.None, session.Current.Board.GetCell(new(1, 1)).Piece);
        Submit(session, Human, new(1, 1));
        Assert.Equal(1, session.Current.ResolvedMainTurns);
        Assert.Equal(PieceType.Grave, session.Current.Board.GetCell(new(1, 1)).Piece);
        if (session.Current.Phase == MatchPhase.Main)
        {
            var following = await source.Next();
            following.Choose(following.Snapshot.GetMainPhaseLegalSelections(Cpu).First());
            await coordinator.PendingWork!.WaitAsync(DeadlockLimit);
        }

        void RecordCpuSubmission(GameSessionSnapshot snapshot)
        {
            if (snapshot.HasSubmitted(Cpu)) publishingThread = Environment.CurrentManagedThreadId;
        }

        void FinishWorkDuringRevision(GameSessionSnapshot snapshot)
        {
            session.StateChanged -= FinishWorkDuringRevision;
            Assert.True(snapshot.HasSubmitted(Human));
            Assert.Same(openingWork, coordinator.PendingWork);
            opening.Choose(BottomRight);
            Assert.True(openingWork.Wait(DeadlockLimit));
        }
    }

    [Fact]
    public async Task ReplacementWithTheSameRoundAndSceneExitDiscardOutstandingWork()
    {
        var old = CreateSession();
        var replacement = CreateSession(RuleSet.KnightSupport);
        var source = new ControlledCpu();
        using var previous = Start(old, source);
        var obsolete = await source.Next();
        Task obsoleteWork = previous.PendingWork!;
        Assert.Equal(old.Current.RoundId, replacement.Current.RoundId);
        previous.Pump(replacement);
        Assert.True(obsolete.Token.IsCancellationRequested);
        Assert.Null(previous.PendingWork);
        using var current = Start(replacement, source);
        var live = await source.Next();
        Assert.Equal(RuleSet.KnightSupport, live.Snapshot.RuleSet);
        Assert.NotSame(obsolete.Random, live.Random);
        obsolete.Choose(TopLeft);
        await obsoleteWork.WaitAsync(DeadlockLimit);
        previous.Pump(replacement);
        Assert.False(replacement.Current.HasSubmitted(Cpu));
        Assert.False(old.Current.HasSubmitted(Cpu));

        Task liveWork = current.PendingWork!;
        current.Dispose();
        Assert.True(live.Token.IsCancellationRequested);
        live.Choose(BottomRight);
        await liveWork.WaitAsync(DeadlockLimit);
        current.Observe(replacement.Current);
        current.Pump(replacement);
        Assert.Null(current.PendingWork);
        Assert.False(replacement.Current.HasSubmitted(Cpu));
    }

    [Fact]
    public async Task FailedDraftAndRestartInvalidateTheOldRoundAndStartOneNewWorker()
    {
        var session = CreateSession();
        var source = new ControlledCpu();
        using var coordinator = Start(session, source);
        Invocation old = await source.Next();
        Task oldWork = coordinator.PendingWork!;
        Submit(session, Human, TopLeft);
        Submit(session, Cpu, TopLeft);
        Assert.Equal(MatchPhase.OpeningRestartRequired, session.Current.Phase);
        Assert.Equal((0, 0), (session.Current.PlayerOneScore, session.Current.PlayerTwoScore));
        Assert.Null(coordinator.PendingWork);
        Assert.True(old.Token.IsCancellationRequested);
        Assert.Equal(SessionCommandResult.Accepted, session.RestartDraft(session.Current.RoundId));
        Invocation restarted = await source.Next();
        Assert.True(restarted.Snapshot.RoundId > old.Snapshot.RoundId);
        Assert.NotSame(old.Random, restarted.Random);
        old.Choose(TopLeft);
        await oldWork.WaitAsync(DeadlockLimit);
        coordinator.Pump(session);
        Assert.False(session.Current.HasSubmitted(Cpu));
        restarted.Choose(BottomRight);
        await coordinator.PendingWork!.WaitAsync(DeadlockLimit);
        coordinator.Pump(session);
        Assert.True(session.Current.HasSubmitted(Cpu));
    }

    [Theory]
    [InlineData("exception")]
    [InlineData("null")]
    [InlineData("wrong-player")]
    [InlineData("wrong-round")]
    [InlineData("rejected")]
    public async Task FailuresAreObservedAndDoNotRetryOrSubmit(string kind)
    {
        var session = CreateSession();
        var source = new ControlledCpu();
        using var coordinator = Start(session, source);
        Invocation attempt = await source.Next();
        Task work = coordinator.PendingWork!;
        switch (kind)
        {
            case "exception":
                attempt.Completion.SetException(new InvalidOperationException("Test CPU failure"));
                await Assert.ThrowsAsync<InvalidOperationException>(() => work.WaitAsync(DeadlockLimit));
                break;
            case "null":
                attempt.Completion.SetResult(null);
                break;
            case "wrong-player":
                attempt.Completion.SetResult(new(Human, BottomRight, MatchPhase.Opening, session.Current.RoundId));
                break;
            case "wrong-round":
                attempt.Completion.SetResult(new(Cpu, BottomRight, MatchPhase.Opening, session.Current.RoundId + 1));
                break;
            default:
                attempt.Choose(new(9, 9));
                break;
        }

        if (kind != "exception") await work.WaitAsync(DeadlockLimit);
        coordinator.Pump(session);
        Assert.NotNull(coordinator.Failure);
        Assert.False(session.Current.HasSubmitted(Cpu));
        Submit(session, Human, TopLeft);
        Assert.Null(coordinator.PendingWork);
    }

    [Theory]
    [InlineData(CpuDifficulty.Easy, RuleSet.Classic)]
    [InlineData(CpuDifficulty.Normal, RuleSet.Classic)]
    [InlineData(CpuDifficulty.Hard, RuleSet.Classic)]
    [InlineData(CpuDifficulty.VeryHard, RuleSet.Classic)]
    [InlineData(CpuDifficulty.Monster, RuleSet.Classic)]
    [InlineData(CpuDifficulty.Easy, RuleSet.KnightSupport)]
    [InlineData(CpuDifficulty.Normal, RuleSet.KnightSupport)]
    [InlineData(CpuDifficulty.Hard, RuleSet.KnightSupport)]
    [InlineData(CpuDifficulty.VeryHard, RuleSet.KnightSupport)]
    [InlineData(CpuDifficulty.Monster, RuleSet.KnightSupport)]
    public async Task RealCpuCanPlayACompleteSeededMatch(CpuDifficulty difficulty, RuleSet ruleSet)
    {
        var session = CreateSession(ruleSet);
        using var coordinator = new CpuMatchCoordinator(session, Cpu, CpuDifficultyProfiles.Get(difficulty), new Random(42));
        session.StateChanged += coordinator.Observe;
        coordinator.Observe(session.Current);

        for (int round = 0; round < 64 && MatchConfiguration.IsPlayable(session.Current.Phase); round++)
        {
            var snapshot = session.Current;
            var legal = snapshot.Phase == MatchPhase.Opening
                ? snapshot.EligiblePositions
                : snapshot.GetMainPhaseLegalSelections(Human).OrderBy(p => p.Y).ThenBy(p => p.X).ToArray();
            Submit(session, Human, legal[0]);
            await coordinator.PendingWork!.WaitAsync(DeadlockLimit);
            coordinator.Pump(session);
            Assert.Null(coordinator.Failure);
            Assert.True(session.Current.RoundId > snapshot.RoundId);
            output.WriteLine($"Human {snapshot.Phase} ({legal[0].X},{legal[0].Y}) ⇒ {session.Current.PlayerOneScore}/{session.Current.PlayerTwoScore}");
        }

        Assert.Equal(MatchPhase.Finished, session.Current.Phase);
        Assert.True(session.Current.ResolvedMainTurns > 0);
        Assert.Null(coordinator.PendingWork);
        var result = session.Current.FinalResult!.Value;
        Assert.Equal((result.PlayerOneScore, result.PlayerTwoScore), (session.Current.PlayerOneScore, session.Current.PlayerTwoScore));
        output.WriteLine($"{ruleSet} {difficulty}: {session.Current.ResolvedRounds} opening round(s), {session.Current.ResolvedMainTurns} main turn(s), " +
            $"{result.PlayerOneScore}/{result.PlayerTwoScore}, {result.Winner?.ToString() ?? "draw"}");
    }

    private static LocalGameSession CreateSession(RuleSet ruleSet = RuleSet.Classic) =>
        new(PredefinedMapCatalog.BuiltIn.Get("pocket-meadow").Definition, ruleSet);

    private static CpuMatchCoordinator Start(LocalGameSession session, ControlledCpu source)
    {
        var coordinator = new CpuMatchCoordinator(session, Cpu, Normal, new Random(42), source.Decide);
        session.StateChanged += coordinator.Observe;
        coordinator.Observe(session.Current);
        return coordinator;
    }

    private static void Submit(LocalGameSession session, PlayerId player, BoardPosition position)
    {
        var snapshot = session.Current;
        Assert.Equal(SessionCommandResult.Accepted,
            MatchConfiguration.Submit(session, player, position, snapshot.Phase, snapshot.RoundId));
    }

    private sealed class ControlledCpu
    {
        private readonly Channel<Invocation> _calls = Channel.CreateUnbounded<Invocation>();

        internal CpuDecision? Decide(GameSessionSnapshot snapshot, PlayerId player, CpuProfile profile, Random random,
            CancellationToken token)
        {
            var invocation = new Invocation(snapshot, player, random, token);
            _calls.Writer.TryWrite(invocation);
            return invocation.Completion.Task.WaitAsync(DeadlockLimit).GetAwaiter().GetResult();
        }

        internal Task<Invocation> Next() => _calls.Reader.ReadAsync().AsTask().WaitAsync(DeadlockLimit);
    }

    private sealed record Invocation(GameSessionSnapshot Snapshot, PlayerId Player, Random Random, CancellationToken Token)
    {
        internal TaskCompletionSource<CpuDecision?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Choose(BoardPosition position) => Completion.SetResult(new(Player, position, Snapshot.Phase, Snapshot.RoundId));
    }
}
