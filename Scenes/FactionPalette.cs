using System;
using CastrumTactics.Domain;
using Godot;

internal static class FactionPalette
{
    private static readonly Color PlayerOne = new("#f06c68");
    private static readonly Color PlayerTwo = new("#69a4ff");

    internal static Color GetColor(PlayerId player) => player switch
    {
        PlayerId.PlayerOne => PlayerOne,
        PlayerId.PlayerTwo => PlayerTwo,
        _ => throw new ArgumentOutOfRangeException(nameof(player)),
    };
}
