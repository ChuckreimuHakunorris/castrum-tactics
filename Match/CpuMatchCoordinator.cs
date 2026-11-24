#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using CastrumTactics.AI;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

namespace CastrumTactics.Match;

// Observe and Pump run on the session's owning thread. The worker sees only public resolved inputs.
internal sealed class CpuMatchCoordinator : IDisposable
{
    internal delegate CpuDecision? DecisionSource(GameSessionSnapshot snapshot, PlayerId player, CpuProfile profile,
        Random random, CancellationToken cancellationToken);

    private readonly IGameSession _session;
    private readonly PlayerId _player;
    private readonly CpuProfile _profile;
    private readonly Random _seedSource;
    private readonly DecisionSource _decide;
    private (MatchPhase Phase, long RoundId)? _observedRound;
    private Request? _request;
    private bool _disposed;

    internal CpuMatchCoordinator(IGameSession session, PlayerId player, CpuProfile profile, Random seedSource,
        DecisionSource? decide = null)
    {
        _session = session;
        _player = player;
        _profile = profile;
        _seedSource = seedSource;
        _decide = decide ?? new CpuDecisionEngine().Decide;
    }

    internal Exception? Failure { get; private set; }
    internal Task? PendingWork => _request?.Work;

    internal void Observe(GameSessionSnapshot snapshot)
    {
        var round = (snapshot.Phase, snapshot.RoundId);
        if (_disposed || _observedRound == round) return;
        _observedRound = round;
        Invalidate();
        Failure = null;
        if (!MatchConfiguration.IsPlayable(snapshot.Phase) || snapshot.HasSubmitted(_player)) return;

        // Each invocation owns its RNG, including while a superseded invocation unwinds.
        var random = new Random(_seedSource.Next());
        var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        DecisionSource decide = _decide;
        PlayerId player = _player;
        CpuProfile profile = _profile;
        Task<CpuDecision?> work = Task.Run(() => decide(snapshot, player, profile, random, token), token);
        _request = new(round, cancellation, work);
    }

    internal void Pump(IGameSession activeSession)
    {
        if (!ReferenceEquals(activeSession, _session))
        {
            Invalidate();
            return;
        }

        if (_request is not { } request || !request.Work.IsCompleted) return;
        _request = null;
        bool canceled = request.Cancellation.IsCancellationRequested;
        request.Cancellation.Dispose();
        GameSessionSnapshot current = _session.Current;
        if (canceled || request.Round != (current.Phase, current.RoundId) || current.HasSubmitted(_player))
        {
            _ = request.Work.Exception;
            return;
        }

        try
        {
            if (request.Work.GetAwaiter().GetResult() is not { } decision ||
                decision.Player != _player || request.Round != (decision.Phase, decision.RoundId))
            {
                throw new InvalidOperationException("CPU returned no current proposal for its player.");
            }

            var result = MatchConfiguration.Submit(_session, _player, decision.Position, decision.Phase, decision.RoundId);
            if (result != SessionCommandResult.Accepted)
                throw new InvalidOperationException($"CPU command was rejected ({result}).");
        }
        catch (Exception ex)
        {
            Failure = ex;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        Invalidate();
    }

    private void Invalidate()
    {
        if (_request is not { } request) return;
        _request = null;
        // The worker may still inspect the token. Observe discarded faults and dispose after it leaves.
        Task cleanup = Task.WhenAll(request.Work, request.Cancellation.CancelAsync());
        _ = cleanup.ContinueWith(static (work, state) =>
        {
            _ = work.Exception;
            ((CancellationTokenSource)state!).Dispose();
        }, request.Cancellation, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private sealed record Request((MatchPhase Phase, long RoundId) Round, CancellationTokenSource Cancellation,
        Task<CpuDecision?> Work);
}
