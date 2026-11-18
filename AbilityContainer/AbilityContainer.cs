using System;
using System.Collections.Generic;
using Godot;
using Tags;

namespace AbilitySystem;

/// <summary>
/// Abilities given to one actor: gives and removes them, checks and runs activations (tags, blocking, cooldown,
/// cost), cancels them. Listeners may give, remove, activate and cancel abilities.
/// <code>
/// AbilityHandle fireball = abilities.Give(fireballData);
/// if (!abilities.TryActivate(fireball)) ...  // ActivationFailed tells why
/// </code>
/// </summary>
public sealed class AbilityContainer
{
    private readonly AttributeContainer _attributes;
    private readonly TagCountContainer _tags;
    private readonly ActiveEffectContainer _effects;
    private readonly Node _actor;
    private readonly Node _abilitySystem;
    private readonly List<Ability> _abilities = new(4);

    // Bound once, so giving an ability allocates no new delegate.
    private readonly Action<Ability, bool> _onAbilityEnded;

    private int _lastActivationId;

    /// <param name="attributes">Read by cost checks and changed by cost effects.</param>
    /// <param name="tags">Checked by tag requirements and cooldowns, given owned tags.</param>
    /// <param name="effects">Cost and cooldown effects are applied here.</param>
    /// <param name="actor">The owning actor, see <see cref="Ability.Actor"/>; may be null.</param>
    /// <param name="abilitySystem">The actor's ability system component, see <see cref="Ability.AbilitySystem"/>; may be null.</param>
    public AbilityContainer(AttributeContainer attributes, TagCountContainer tags, ActiveEffectContainer effects, Node actor = null, Node abilitySystem = null)
    {
        _attributes = attributes;
        _tags = tags;
        _effects = effects;
        _actor = actor;
        _abilitySystem = abilitySystem;
        _onAbilityEnded = OnAbilityEnded;
    }

    public event Action<Ability> AbilityGiven;

    /// <summary>After the ability was taken off the list (and cancelled if it was active).</summary>
    public event Action<Ability> AbilityRemoved;

    /// <summary>The ability is active (owned tags given, cost and cooldown committed if automatic); raised right before its <c>Activate</c>.</summary>
    public event Action<Ability> AbilityActivated;

    /// <summary>(ability, cancelled) once it ended and its owned tags were taken back.</summary>
    public event Action<Ability, bool> AbilityEnded;

    /// <summary>(ability, reason) when an activation was refused.</summary>
    public event Action<Ability, ActivationFailure> ActivationFailed;

    public int Count => _abilities.Count;

    /// <summary>The actor's copy, not the definition.</summary>
    public Ability this[int index] => _abilities[index];

    /// <summary>Null if the ability is not given here.</summary>
    public Ability Get(AbilityHandle handle)
    {
        for (int i = 0; i < _abilities.Count; i++)
        {
            if (_abilities[i].Handle == handle)
                return _abilities[i];
        }

        return null;
    }

    /// <summary>The actor's copy of <paramref name="ability"/> (a definition or the copy itself); null if not given here.</summary>
    public Ability Find(Ability ability)
    {
        if (ability == null)
            return null;

        for (int i = 0; i < _abilities.Count; i++)
        {
            if (_abilities[i] == ability || _abilities[i].Definition == ability)
                return _abilities[i];
        }

        return null;
    }

    /// <summary>
    /// Gives the actor a copy of <paramref name="definition"/>. A definition already given returns its existing
    /// handle (one copy per definition). None if the definition is null.
    /// </summary>
    public AbilityHandle Give(Ability definition, int level = 1)
    {
        if (definition == null)
        {
            GD.PushError("AbilityContainer.Give: the ability is null.");
            return AbilityHandle.None;
        }

        if (definition.Definition != null)
        {
            GD.PushError("AbilityContainer.Give: this is an actor's copy; give the definition (.tres) instead.");
            return AbilityHandle.None;
        }

        Ability existing = Find(definition);
        if (existing != null)
            return existing.Handle;

        var ability = (Ability)definition.Duplicate();
        AbilityHandle handle = AbilityHandle.Next();
        ability.Grant(definition, handle, level, _attributes, _tags, _effects, _actor, _abilitySystem, _onAbilityEnded);
        _abilities.Add(ability);
        AbilityGiven?.Invoke(ability);
        return handle;
    }

    /// <summary>Takes the ability away; an active one is cancelled.</summary>
    public bool Remove(AbilityHandle handle)
    {
        Ability ability = Get(handle);
        if (ability == null)
            return false;

        // Off the list first, so listeners of the cancel cannot activate it again.
        _abilities.Remove(ability);
        ability.Finish(cancelled: true);
        AbilityRemoved?.Invoke(ability);
        return true;
    }

    public bool CanActivate(AbilityHandle handle) => CanActivate(handle, out _);

    /// <summary>Whether the ability could activate now; allocates nothing, so UI may ask every frame.</summary>
    public bool CanActivate(AbilityHandle handle, out ActivationFailure reason)
    {
        Ability ability = Get(handle);
        reason = ability == null ? ActivationFailure.NotGranted : Check(ability);
        return reason == ActivationFailure.None;
    }

    public bool TryActivate(AbilityHandle handle)
    {
        Ability ability = Get(handle);
        return ability != null && Activate(ability);
    }

    /// <summary>Activates the actor's copy of <paramref name="ability"/> (a definition or the copy itself).</summary>
    public bool TryActivate(Ability ability)
    {
        Ability own = Find(ability);
        return own != null && Activate(own);
    }

    /// <summary>Activates the first ability whose asset tags match <paramref name="tag"/> (hierarchically) and can activate.</summary>
    public bool TryActivateWithTag(Tag tag)
    {
        for (int i = 0; i < _abilities.Count; i++)
        {
            Ability ability = _abilities[i];
            if (ability.AssetTags != null && ability.AssetTags.Has(tag) && Activate(ability))
                return true;
        }

        return false;
    }

    public bool Cancel(AbilityHandle handle)
    {
        Ability ability = Get(handle);
        if (ability == null || !ability.IsActive || ability.IsEnding)
            return false;

        ability.Finish(cancelled: true);
        return true;
    }

    /// <summary>Cancels the active abilities whose asset tags match any of <paramref name="tags"/> (hierarchically). Returns how many.</summary>
    public int CancelWithTags(TagContainer tags) => CancelMatching(tags, null);

    /// <summary>Cancels every active ability. Returns how many.</summary>
    public int CancelAll() => CancelMatching(null, null);

    private ActivationFailure Check(Ability ability)
    {
        if (ability.IsActive)
            return ActivationFailure.AlreadyActive;

        if (!ability.MeetsTagRequirements())
            return ActivationFailure.TagRequirements;

        if (IsBlocked(ability))
            return ActivationFailure.BlockedByAbility;

        if (ability.IsOnCooldown)
            return ActivationFailure.Cooldown;

        if (!ability.CanAffordCost)
            return ActivationFailure.Cost;

        return ability.PassesCustomCheck() ? ActivationFailure.None : ActivationFailure.Custom;
    }

    private bool IsBlocked(Ability ability)
    {
        TagContainer assetTags = ability.AssetTags;
        if (assetTags == null || assetTags.Count == 0)
            return false;

        for (int i = 0; i < _abilities.Count; i++)
        {
            Ability other = _abilities[i];
            if (other.IsActive && other.BlockAbilitiesWithTags != null && assetTags.HasAny(other.BlockAbilitiesWithTags))
                return true;
        }

        return false;
    }

    private bool Activate(Ability ability)
    {
        ActivationFailure reason = Check(ability);
        if (reason != ActivationFailure.None)
        {
            ActivationFailed?.Invoke(ability, reason);
            return false;
        }

        if (ability.CancelAbilitiesWithTags != null)
            CancelMatching(ability.CancelAbilitiesWithTags, ability);

        // Listeners of the cancelled abilities may have removed or activated this one.
        if (!_abilities.Contains(ability) || ability.IsActive)
            return false;

        if (ability.CommitOnActivate)
        {
            reason = ability.TryCommit();
            if (reason != ActivationFailure.None)
            {
                ActivationFailed?.Invoke(ability, reason);
                return false;
            }
        }

        ability.BeginActivation(++_lastActivationId);

        // A listener of an owned tag may have ended it already; it still counts as activated.
        if (ability.IsActive)
            AbilityActivated?.Invoke(ability);

        if (ability.IsActive)
            ability.RunActivate();

        return true;
    }

    /// <summary>Cancels active abilities matching <paramref name="tags"/> (all when null), except <paramref name="except"/>.</summary>
    private int CancelMatching(TagContainer tags, Ability except)
    {
        if (tags != null && tags.Count == 0)
            return 0;

        // Abilities activated by listeners during this call are left alone, so a listener re-activating cannot loop forever.
        int lastActivationId = _lastActivationId;
        int cancelled = 0;
        int i = 0;
        while (i < _abilities.Count)
        {
            Ability ability = _abilities[i];
            bool matches = ability != except
                && ability.IsActive
                && !ability.IsEnding
                && ability.ActivationId <= lastActivationId
                && (tags == null || (ability.AssetTags != null && ability.AssetTags.HasAny(tags)));

            if (!matches)
            {
                i++;
                continue;
            }

            ability.Finish(cancelled: true);
            cancelled++;
            // Listeners may have changed the list; start over (lists are short).
            i = 0;
        }

        return cancelled;
    }

    private void OnAbilityEnded(Ability ability, bool cancelled) => AbilityEnded?.Invoke(ability, cancelled);
}
