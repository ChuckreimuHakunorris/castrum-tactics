using System.Collections;
using System.Numerics;

namespace CastrumTactics.Domain;

// A compact set for a rectangular board. MainPhaseState owns construction and publishes it through IReadOnlySet.
internal sealed class LegalSelectionSet(int width, int height) : IReadOnlySet<BoardPosition>
{
    private readonly ulong[] _bits = new ulong[(checked(width * height) - 1) / 64 + 1];

    public int Count { get; private set; }

    internal void Add(BoardPosition position)
    {
        int index = position.Y * width + position.X;
        ref ulong word = ref _bits[index / 64];
        ulong bit = 1UL << (index % 64);
        if ((word & bit) != 0) return;
        word |= bit;
        Count++;
    }

    public bool Contains(BoardPosition item)
    {
        if ((uint)item.X >= (uint)width || (uint)item.Y >= (uint)height) return false;
        int index = item.Y * width + item.X;
        return (_bits[index / 64] & (1UL << (index % 64))) != 0;
    }

    public IEnumerator<BoardPosition> GetEnumerator()
    {
        for (int i = 0; i < _bits.Length; i++)
        {
            ulong word = _bits[i];
            while (word != 0)
            {
                int index = i * 64 + BitOperations.TrailingZeroCount(word);
                yield return new BoardPosition(index % width, index / width);
                word &= word - 1;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool IsSubsetOf(IEnumerable<BoardPosition> other) => new HashSet<BoardPosition>(this).IsSubsetOf(other);
    public bool IsProperSubsetOf(IEnumerable<BoardPosition> other) => new HashSet<BoardPosition>(this).IsProperSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<BoardPosition> other) => other.All(Contains);
    public bool IsProperSupersetOf(IEnumerable<BoardPosition> other) => new HashSet<BoardPosition>(this).IsProperSupersetOf(other);
    public bool Overlaps(IEnumerable<BoardPosition> other) => other.Any(Contains);
    public bool SetEquals(IEnumerable<BoardPosition> other) => new HashSet<BoardPosition>(this).SetEquals(other);
}
