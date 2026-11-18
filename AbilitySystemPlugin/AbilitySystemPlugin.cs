#if TOOLS
using Godot;

namespace AbilitySystem;

/// <summary>
/// Editor plugin entry point: registers the "ability_system/cue_library_path" project setting.
/// </summary>
[Tool]
public partial class AbilitySystemPlugin : EditorPlugin
{
    public override void _EnterTree()
    {
        RegisterCueLibrarySetting();
    }

    public override void _EnablePlugin()
    {
        RegisterCueLibrarySetting();
    }

    private static void RegisterCueLibrarySetting()
    {
        if (!ProjectSettings.HasSetting(CueLibrary.PathSetting))
            ProjectSettings.SetSetting(CueLibrary.PathSetting, CueLibrary.DefaultPath);

        ProjectSettings.SetInitialValue(CueLibrary.PathSetting, CueLibrary.DefaultPath);
        ProjectSettings.SetAsBasic(CueLibrary.PathSetting, true);

        // Godot API: property info must be passed as a Godot Dictionary.
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", CueLibrary.PathSetting },
            { "type", (int)Variant.Type.String },
            { "hint", (int)PropertyHint.File },
            { "hint_string", "*.tres,*.res" },
        });
    }
}
#endif
