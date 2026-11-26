#nullable enable

using System;
using CastrumTactics.AI;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

namespace CastrumTactics.Match;

// The scene attaches its observer after updating the captions for the newly selected map.
internal sealed class MatchLifetime
{
    internal IGameSession? Session { get; private set; }
    internal CpuMatchCoordinator? Cpu { get; private set; }

    internal IGameSession Create(MapDefinition map, MatchConfiguration configuration, int? seed)
    {
        var session = new LocalGameSession(map, configuration.RuleSet);
        Session = session;
        if (configuration.CpuPlayer is { } player)
        {
            var seeds = seed is int value ? new Random(value) : new Random();
            Cpu = new CpuMatchCoordinator(session, player, CpuDifficultyProfiles.Get(configuration.Difficulty), seeds);
        }
        return session;
    }

    internal void Detach(Action<GameSessionSnapshot> observer)
    {
        if (Session is not null) Session.StateChanged -= observer;
        Cpu?.Dispose();
        Cpu = null;
        Session = null;
    }
}
