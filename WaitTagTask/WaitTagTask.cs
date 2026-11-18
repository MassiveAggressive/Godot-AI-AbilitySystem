using System;
using Tags;

namespace AbilitySystem;

/// <summary>
/// Raises <see cref="Completed"/> once the actor has <see cref="Tag"/> (or, with <see cref="Present"/> off, once
/// it no longer has it). Already so when started → completes right away.
/// </summary>
public sealed class WaitTagTask : AbilityTask
{
    private readonly TagCountContainer _tags;

    // Bound once, so subscribing allocates no delegate.
    private readonly Action<Tag, bool> _onPresenceChanged;

    public WaitTagTask(TagCountContainer tags, Tag tag, bool present = true)
    {
        _tags = tags;
        Tag = tag;
        Present = present;
        _onPresenceChanged = OnPresenceChanged;
    }

    public event Action Completed;

    public Tag Tag { get; }

    public bool Present { get; }

    protected override void OnStart()
    {
        if (_tags.Has(Tag) == Present)
        {
            Complete();
            return;
        }

        _tags.PresenceChanged += _onPresenceChanged;
    }

    protected override void OnEnd() => _tags.PresenceChanged -= _onPresenceChanged;

    private void OnPresenceChanged(Tag tag, bool present)
    {
        if (tag == Tag && present == Present)
            Complete();
    }

    private void Complete()
    {
        End();
        Completed?.Invoke();
    }
}
