extern alias frozen;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;
using Frozen = frozen::CastrumTactics.AI;

// Dot's archived Probe and helpers, with the Normal engine bound to the isolated 8B.1 assembly.
internal static class ArchivedProbe
{
    private static readonly Frozen.CpuDecisionEngine engine = new();
    private static readonly Frozen.CpuProfile normal = Frozen.CpuDifficultyProfiles.Get(Frozen.CpuDifficulty.Normal);

    internal static BoardPosition Probe(GameSessionSnapshot s, PlayerId side, Random rng, out int pairs) {
        pairs = 0;
        var foe = Opp(side);
        var rules = MainPhaseState.Evaluate(s.Board,s.RuleSet);
        var allOwn = Sorted(rules.GetLegalSelections(side));
        var allFoe = Sorted(rules.GetLegalSelections(foe));
        var own = allOwn.Length <= 10 ? allOwn : engine.DecideWithDiagnostics(s,side,normal,new Random(0)).Candidates.Take(10).Select(c=>c.Position).ToArray();
        var responses = allFoe.Length <= 8 ? allFoe : engine.DecideWithDiagnostics(s,foe,normal,new Random(0)).Candidates.Take(6).Select(c=>c.Position).ToArray();
        double best=double.NegativeInfinity;
        var choices=new List<BoardPosition>();
        foreach (var a in own) {
            var replies = responses.Contains(a) || !rules.GetLegalSelections(foe).Contains(a) ? responses : responses.Append(a).ToArray();
            pairs += replies.Length;
            var vals = replies.Select(b=>Value(MainPhaseState.Evaluate(rules.ResolveTurn(new(side,a),new(foe,b)),s.RuleSet),side)).ToArray();
            double v = .65*vals.Average()+.35*vals.Min();
            if (v>best+1e-9){best=v;choices.Clear();}
            if (v>=best-1e-9) choices.Add(a);
        }
        return choices[rng.Next(choices.Count)];
    }
    static double Value(MainPhaseState s, PlayerId p) {
        double td=TD(s,p);
        if (s.Result is not null) return Math.Sign(td)*150+td;
        var e=Opp(p);
        var own=s.GetLegalSelections(p); var foe=s.GetLegalSelections(e);
        double pieces=0, anchors=0;
        for (int y=0;y<s.Board.Height;y++) for (int x=0;x<s.Board.Width;x++) {
            var q=new BoardPosition(x,y); var cell=s.Board.GetCell(q);
            if (cell.Owner is null) continue;
            int sign=cell.Owner==p?1:-1;
            if(cell.Piece==PieceType.Knight) {
                pieces += sign;
                int supports=0; for(int yy=Math.Max(0,y-1); yy<Math.Min(s.Board.Height,y+2); yy++) for(int xx=Math.Max(0,x-1); xx<Math.Min(s.Board.Width,x+2); xx++) if((xx!=x || yy!=y) && s.Board.GetCell(new BoardPosition(xx,yy)).Owner==cell.Owner) supports++;
                anchors += sign * (supports>1?0.5:0);
            } else if (cell.Piece==PieceType.Castle) anchors += sign;
        }
        return td*.95 + pieces*.65 + anchors*.5 + (own.Count-foe.Count)*.25;
    }
    static PlayerId Opp(PlayerId p) => p == PlayerId.PlayerOne ? PlayerId.PlayerTwo : PlayerId.PlayerOne;
    static BoardPosition[] Sorted(IEnumerable<BoardPosition> pos)=>pos.OrderBy(q=>q.Y).ThenBy(q=>q.X).ToArray();
    static int TD(MainPhaseState s,PlayerId p)=>(s.Territory.PlayerOneScore-s.Territory.PlayerTwoScore)*(p==PlayerId.PlayerOne?1:-1);
}
