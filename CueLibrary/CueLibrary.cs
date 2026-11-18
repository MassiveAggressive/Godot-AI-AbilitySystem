using System;
using System.Collections.Generic;
using Godot;
using Tags;

namespace AbilitySystem;

/// <summary>
/// Which scene each cue tag opens, for the whole project. Effects only name cue tags (<see cref="EffectData.Cues"/>);
/// this list turns them into scenes. The file is set in the "ability_system/cue_library_path" project setting.
/// A cue tag without an entry uses its closest parent's entry; with none, nothing is shown.
/// </summary>
[Tool]
[GlobalClass]
public partial class CueLibrary : Resource
{
    public const string PathSetting = "ability_system/cue_library_path";
    public const string DefaultPath = "res://Resource/Cues/Cues.tres";

    private static CueLibrary _default;

    private CueEntry[] _entries = Array.Empty<CueEntry>();

    // Lookups by cue tag id, misses included; filled on first use of each tag.
    private Dictionary<int, CueEntry> _found;

    [Export]
    public CueEntry[] Entries
    {
        get => _entries;
        set
        {
            _entries = value ?? Array.Empty<CueEntry>();
            _found = null;
        }
    }

    /// <summary>
    /// The library at the project setting's path, loaded once on first use. Without that file it is empty: effects
    /// work and show no cues.
    /// </summary>
    public static CueLibrary Default => _default ??= LoadDefault();

    public static string GetLibraryPath() =>ProjectSettings.GetSetting(PathSetting, DefaultPath).AsString();

    /// <summary>The entry for <paramref name="tag"/> or its closest parent that has one; null if none.</summary>
    internal CueEntry Find(Tag tag)
    {
        _found ??= new Dictionary<int, CueEntry>();
        if (_found.TryGetValue(tag.Id, out CueEntry entry))
            return entry;

        entry = FindDeepest(tag);
        _found[tag.Id] = entry;
        return entry;
    }

    private CueEntry FindDeepest(Tag tag)
    {
        CueEntry best = null;
        foreach (CueEntry entry in _entries)
        {
            if (entry?.Scene == null || !tag.Matches(entry.Tag))
                continue;

            // Every match is the tag or one of its parents, so the deeper of two matches the other.
            if (best == null || (entry.Tag != best.Tag && entry.Tag.Matches(best.Tag)))
                best = entry;
        }

        return best;
    }

    private static CueLibrary LoadDefault()
    {
        string path = GetLibraryPath();
        if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path))
            return new CueLibrary();

        if (ResourceLoader.Load(path) is CueLibrary library)
            return library;

        GD.PushError($"CueLibrary: '{path}' ({PathSetting}) is not a CueLibrary; no cues are shown.");
        return new CueLibrary();
    }
}
