namespace AbilitySystem;

/// <summary>
/// Optional hooks for a cue scene. Cue scenes need no code; when the root node's script implements this, its
/// methods are called after the scene is added (and placed). Implement only the ones needed:
/// <code>
/// public partial class BurnFlames : GpuParticles2D, ICueNotify
/// {
///     public void OnCueAdded(in CueParameters parameters) => Emitting = true;
/// }
/// </code>
/// </summary>
public interface ICueNotify
{
    /// <summary>An instant effect was applied or a periodic effect executed; the scene opens once per execution.</summary>
    void OnCueExecuted(in CueParameters parameters) { }

    /// <summary>The first effect with this cue became active (later ones only share the scene).</summary>
    void OnCueAdded(in CueParameters parameters) { }

    /// <summary>
    /// The last effect with this cue ended or was inhibited. Return true to free the scene yourself (e.g. after
    /// fading out); false frees it right away.
    /// </summary>
    bool OnCueRemoved(in CueParameters parameters) => false;
}
