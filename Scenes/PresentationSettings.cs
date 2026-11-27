#nullable enable

using System;
using System.IO;
using System.Text.Json;

internal sealed record PresentationPreferences(
    bool ShowOpponentLegalTargets = true,
    bool ReduceMotion = false,
    bool PlayTurnResolutionAnimations = true);

internal sealed class PresentationSettings(string? path = null)
{
    private static readonly Lazy<PresentationSettings> Saved =
        new(() => new PresentationSettings(Godot.ProjectSettings.GlobalizePath("user://presentation.json")));

    internal static PresentationSettings Application => Saved.Value;

    internal PresentationPreferences Current { get; private set; } = Read(path);
    internal bool IsPersistent => path is not null;
    internal event Action<PresentationPreferences>? Changed;

    // A pathless store gives scene hosts and tests the same live API without touching user files.
    internal void Update(PresentationPreferences preferences)
    {
        if (Current == preferences) return;
        if (path is not null) Save(path, preferences);
        Current = preferences;
        Changed?.Invoke(preferences);
    }

    private static PresentationPreferences Read(string? path)
    {
        var defaults = new PresentationPreferences();
        if (path is null) return defaults;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return defaults;
            return new PresentationPreferences(
                ReadFlag(nameof(PresentationPreferences.ShowOpponentLegalTargets), defaults.ShowOpponentLegalTargets),
                ReadFlag(nameof(PresentationPreferences.ReduceMotion), defaults.ReduceMotion),
                ReadFlag(nameof(PresentationPreferences.PlayTurnResolutionAnimations), defaults.PlayTurnResolutionAnimations));

            bool ReadFlag(string key, bool fallback) => document.RootElement.TryGetProperty(key, out var flag) &&
                flag.ValueKind is JsonValueKind.True or JsonValueKind.False ? flag.GetBoolean() : fallback;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return defaults;
        }
    }

    private static void Save(string path, PresentationPreferences preferences)
    {
        // Replace only after a complete write so an interrupted save leaves the previous preferences readable.
        string temporary = path + "." + Path.GetRandomFileName();
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(preferences));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
