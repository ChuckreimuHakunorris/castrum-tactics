using System.Numerics;
using System.IO.Compression;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using CastrumTactics.Domain;
using CastrumTactics.Domain.Session;

namespace CastrumTactics.Development.Solver;

internal sealed class SolvedTablebase
{
    internal const int Version = 3;
    private const string Precision = "certified exact rational";
    private const string IntervalPrecision = "certified outward rational intervals";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string DomainDigest =
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(BoardState).Assembly.Location)));

    private readonly Dictionary<string, Equilibrium[]> _entries;
    internal string Digest { get; }
    internal Metadata Info { get; }

    private SolvedTablebase(Metadata info, Dictionary<string, Equilibrium[]> entries, string digest)
    {
        Info = info;
        _symmetry = new(info.Transforms);
        _entries = entries;
        Digest = digest;
    }

    internal sealed record Metadata(int SolverVersion, int StateVersion, string DomainFingerprint, string Map,
        string MapFingerprint, string Starts, string[] Path, string Rules, string Precision, string[] Utilities, bool Complete, BoardTransform[] Transforms, bool IgnoresLockedHealth, int IntervalScale);
    private sealed record WireEntry(string Key, string[] Values, string[][] Red, string[][] Blue,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        string[][]? Bounds = null);
    private const string Footer = "sha256:";
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    private readonly BoardSymmetry _symmetry;

    internal static string Save(string path, string map, string mapFingerprint, string starts, string[] history,
        SmallBoardGraph graph, bool complete, CancellationToken token)
    {
        var metadata = new Metadata(Version, PositionIdentity.Version, DomainDigest, map, mapFingerprint, starts,
            history, nameof(RuleSet.KnightSupport), graph.IntervalScale == 0 ? Precision : IntervalPrecision, ["Outcome", "Margin"],
            complete, graph.Symmetry.Transforms, graph.IgnoresLockedHealth, graph.IntervalScale);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            string digest;
            using (var file = File.Create(temporary))
            using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
            using (var writer = new StreamWriter(gzip, Utf8) { NewLine = "\n" })
            using (var checksum = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                WriteLine(JsonSerializer.Serialize(metadata, Json), writer, checksum);
                foreach (var (key, node) in graph.Positions)
                {
                    token.ThrowIfCancellationRequested();
                    if (node.Margin is null) continue;
                    var games = new[] { node.Game(SolverUtility.Outcome), node.Game(SolverUtility.Margin) }
                        .Select(game => node.ToCanonical.PolicyToCanonical(game, node.Red, node.Blue,
                            node.Width, node.Height)).ToArray();
                    var entry = new WireEntry(EncodeKey(key), games.Select(g => g.Value.ToString()).ToArray(),
                        games.Select(g => g.Red.Select(p => p.ToString()).ToArray()).ToArray(),
                        games.Select(g => g.Blue.Select(p => p.ToString()).ToArray()).ToArray(),
                        games.Any(g => !g.IsExact) ? games.Select(g =>
                            new[] { g.Lower.ToString(), g.Upper.ToString() }).ToArray() : null);
                    WriteLine(JsonSerializer.Serialize(entry, Json), writer, checksum);
                }
                digest = Convert.ToHexString(checksum.GetHashAndReset());
                writer.WriteLine(Footer + digest);
            }
            File.Move(temporary, path, overwrite: true);
            return digest;
        }
        finally { File.Delete(temporary); }
    }

    private static void WriteLine(string line, StreamWriter writer, IncrementalHash checksum)
    {
        writer.WriteLine(line);
        AddToChecksum(line, checksum);
    }

    private static void AddToChecksum(string line, IncrementalHash checksum) =>
        checksum.AppendData(Utf8.GetBytes(line + "\n"));

    internal static SolvedTablebase Load(string path)
    {
        try { return Read(path); }
        catch (Exception e) when (e is JsonException or FormatException or OverflowException or DivideByZeroException or DecoderFallbackException)
        {
            throw new InvalidDataException("Malformed tablebase data.", e);
        }
    }

    private static SolvedTablebase Read(string path)
    {
        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Utf8);
        using var checksum = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        string header = reader.ReadLine() ?? throw new InvalidDataException("Empty tablebase.");
        AddToChecksum(header, checksum);
        var info = JsonSerializer.Deserialize<Metadata>(header, Json);
        if (info is null || info.SolverVersion is not (1 or 2 or Version) || info.StateVersion != PositionIdentity.Version ||
            info.DomainFingerprint != DomainDigest || info.Rules != nameof(RuleSet.KnightSupport) ||
            (info.SolverVersion == 1 && info.IgnoresLockedHealth) ||
            (info.SolverVersion < 3 && info.IntervalScale != 0) || info.IntervalScale is < 0 or 1 ||
            info.Precision != (info.IntervalScale == 0 ? Precision : IntervalPrecision) || info.Utilities is null || !info.Utilities.SequenceEqual(new[] { "Outcome", "Margin" }) ||
            info.Path is null || string.IsNullOrWhiteSpace(info.Map) || string.IsNullOrWhiteSpace(info.Starts) ||
            info.Transforms is not { Length: > 0 } || info.Transforms[0] != default ||
            info.Transforms.Any(t => t.Geometry is < 0 or > 7))
            throw new InvalidDataException("Incompatible tablebase metadata.");
        var entries = new Dictionary<string, Equilibrium[]>(StringComparer.Ordinal);
        var pool = new PolicyPool();
        while (reader.ReadLine() is string line)
        {
            if (line.StartsWith(Footer, StringComparison.Ordinal))
            {
                string digest = Convert.ToHexString(checksum.GetHashAndReset());
                if (line != Footer + digest || reader.ReadLine() is not null || entries.Count == 0)
                    throw new InvalidDataException("Tablebase checksum mismatch or empty state index.");
                return new(info, entries, digest);
            }

            AddToChecksum(line, checksum);
            var entry = JsonSerializer.Deserialize<WireEntry>(line, Json);
            if (entry is null || entry.Key is null || entry.Values is not { Length: 2 } ||
                entry.Values.Any(string.IsNullOrWhiteSpace) || entry.Red is not { Length: 2 } ||
                entry.Blue is not { Length: 2 } || entry.Red.Concat(entry.Blue).Any(row =>
                    row is null || row.Any(string.IsNullOrWhiteSpace)))
                throw new InvalidDataException("Invalid tablebase entry.");
            var games = new Equilibrium[2];
            for (int i = 0; i < games.Length; i++)
            {
                var red = entry.Red[i].Select(Rational.Parse).ToArray();
                var blue = entry.Blue[i].Select(Rational.Parse).ToArray();
                if (!(red.Length == 0 && blue.Length == 0) &&
                    (!MatrixGame.Distribution(red, red.Length) || !MatrixGame.Distribution(blue, blue.Length)))
                    throw new InvalidDataException("Invalid tablebase policy.");
                var game = new Equilibrium(Rational.Parse(entry.Values[i]), red, blue);
                if (entry.Bounds is { } bounds)
                {
                    if (info.IntervalScale == 0 || bounds.Length != 2 ||
                        bounds[i] is not { Length: 2 } || bounds[i].Any(string.IsNullOrWhiteSpace))
                        throw new InvalidDataException("Invalid tablebase interval.");
                    game = game with { Lower = Rational.Parse(bounds[i][0]), Upper = Rational.Parse(bounds[i][1]) };
                    if (game.Value < game.Lower || game.Value > game.Upper)
                        throw new InvalidDataException("Tablebase value lies outside its stated interval.");
                }
                games[i] = pool.Intern(game);
            }
            if (!entries.TryAdd(DecodeKey(entry.Key), games)) throw new InvalidDataException("Duplicate tablebase state.");
        }
        throw new InvalidDataException("Truncated tablebase.");
    }

    internal Equilibrium Get(MainPhaseState state, SolverUtility utility)
    {
        var board = state.Board;
        string raw = PositionIdentity.Key(board, state.RuleSet, ignoreLockedHealth: Info.IgnoresLockedHealth);
        var (key, transform) = _symmetry.Canonicalize(raw, board.Width, board.Height);
        if (!_entries.TryGetValue(key, out var games))
        {
            // Exchanging physical assignments always yields an equivalent successful opening.
            var exchange = new BoardTransform(0, true);
            string exchanged = exchange.Apply(raw, board.Width, board.Height);
            (key, transform) = _symmetry.Canonicalize(exchanged, board.Width, board.Height);
            if (!_entries.TryGetValue(key, out games))
                throw new InvalidOperationException("The resolved position is outside the solved tablebase scope.");
            transform = transform with { Exchange = !transform.Exchange };
        }
        var game = games[(int)utility];
        if (transform.Exchange) game = BoardTransform.Negate(game);
        if (state.Result is { } result)
        {
            int margin = result.PlayerOneScore - result.PlayerTwoScore;
            if (game.Red.Length != 0 || game.Blue.Length != 0 ||
                !game.IsExact || game.Value != (utility == SolverUtility.Outcome ? Math.Sign(margin) : margin))
                throw new InvalidDataException("Tablebase terminal value differs from authoritative scoring.");
            return game;
        }
        var red = state.GetLegalSelections(PlayerId.PlayerOne).ToArray();
        var blue = state.GetLegalSelections(PlayerId.PlayerTwo).ToArray();
        if (game.Red.Length != (transform.Exchange ? blue.Length : red.Length) ||
            game.Blue.Length != (transform.Exchange ? red.Length : blue.Length))
            throw new InvalidDataException("Tablebase policy is incompatible with the legal actions.");
        return transform.PolicyFromCanonical(games[(int)utility], red, blue, board.Width, board.Height);
    }

    internal BoardPosition Decide(GameSessionSnapshot snapshot, PlayerId player, Random random) =>
        Decide(snapshot, player, random, out _);

    internal BoardPosition Decide(GameSessionSnapshot snapshot, PlayerId player, Random random, out Equilibrium game)
    {
        if (snapshot.Phase != MatchPhase.Main) throw new InvalidOperationException("Tablebase supports Main decisions only.");
        game = Get(snapshot.MainState!, SolverUtility.Outcome);
        var distribution = player == PlayerId.PlayerOne ? game.Red : game.Blue;
        return snapshot.GetMainPhaseLegalSelections(player).ElementAt(Sample(distribution, random));
    }

    internal static int Sample(Rational[] probabilities, Random random)
    {
        int certain = Array.FindIndex(probabilities, p => p == 1);
        if (certain >= 0) return certain;
        BigInteger denominator = BigInteger.One;
        foreach (var probability in probabilities)
        {
            var d = probability.Parts.Denominator;
            denominator = denominator / BigInteger.GreatestCommonDivisor(denominator, d) * d;
        }
        // Mask to the bit length of denominator-1, then reject excess tickets to remove modulo bias.
        var maximum = (denominator - 1).ToByteArray(isUnsigned: true, isBigEndian: false);
        Span<byte> bytes = maximum.Length <= 256 ? stackalloc byte[maximum.Length] : new byte[maximum.Length];
        int mask = 255 >> (BitOperations.LeadingZeroCount((uint)maximum[^1]) - 24);
        BigInteger ticket;
        do
        {
            random.NextBytes(bytes);
            bytes[^1] &= (byte)mask;
            ticket = new BigInteger(bytes, isUnsigned: true, isBigEndian: false);
        } while (ticket >= denominator);

        for (int i = 0; i < probabilities.Length; i++)
        {
            var (n, d) = probabilities[i].Parts;
            ticket -= n * (denominator / d);
            if (ticket.Sign < 0) return i;
        }
        throw new InvalidDataException("Cannot sample an invalid probability distribution.");
    }

    private static string EncodeKey(string key) => string.Concat(key.Select(c =>
        ((ushort)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture)));

    private static string DecodeKey(string text)
    {
        if (text.Length % 4 != 0) throw new InvalidDataException("Malformed tablebase state identity.");
        return string.Create(text.Length / 4, text, (characters, hex) =>
        {
            for (int i = 0; i < characters.Length; i++)
                characters[i] = (char)Convert.ToUInt16(hex.Substring(i * 4, 4), 16);
        });
    }
}
