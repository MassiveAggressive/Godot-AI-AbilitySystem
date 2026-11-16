using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace AbilitySystem;

/// <summary>
/// Finds every <see cref="AttributeSet"/> subclass once by reflection and gives each attribute an id.
/// Names are "SetClass.Property" (e.g. "CombatSet.Health"). Built on first use; no autoload needed.
/// </summary>
public static class AttributeRegistry
{
    // Null until initialized; index 0 is AttributeId.None.
    private static string[] _names;
    private static Dictionary<string, int> _idsByName;
    private static Dictionary<Type, SetInfo> _setsByType;

    /// <summary>Number of ids including <see cref="AttributeId.None"/>; arrays of this size can be indexed by <see cref="AttributeId.Value"/>.</summary>
    public static int Count
    {
        get
        {
            EnsureInitialized();
            return _names.Length;
        }
    }

    /// <summary>Resolves an attribute by "SetClass.Property" name. Call at load time, not every frame.</summary>
    public static AttributeId Request(string name)
    {
        EnsureInitialized();

        if (name != null && _idsByName.TryGetValue(name, out int id))
            return new AttributeId(id);

        GD.PushError($"Attribute not found: '{name}'.");
        return AttributeId.None;
    }

    public static string GetName(AttributeId id)
    {
        EnsureInitialized();
        return id.IsValid && id.Value < _names.Length ? _names[id.Value] : "None";
    }

    /// <summary>False if <paramref name="setType"/> was not registered (abstract, generic or duplicate class name).</summary>
    internal static bool TryGetSetInfo(Type setType, out SetInfo info)
    {
        EnsureInitialized();
        return _setsByType.TryGetValue(setType, out info);
    }

    /// <summary>Comma separated attribute names for an editor dropdown, grouped by set.</summary>
    internal static string BuildOptions()
    {
        EnsureInitialized();
        return string.Join(",", _names, 1, _names.Length - 1);
    }

    private static void EnsureInitialized()
    {
        if (_names == null)
            Initialize();
    }

    private static void Initialize()
    {
        var names = new List<string> { string.Empty };
        var idsByName = new Dictionary<string, int>(StringComparer.Ordinal);
        var setsByType = new Dictionary<Type, SetInfo>();
        var setNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (Type setType in FindSetTypes())
        {
            // Dropdown names leave out the namespace, so class names must be unique.
            if (!setNames.Add(setType.Name))
            {
                GD.PushError($"AttributeSet class name '{setType.Name}' is used more than once; '{setType.FullName}' is skipped.");
                continue;
            }

            PropertyInfo[] properties = FindAttributeProperties(setType);
            var ids = new AttributeId[properties.Length];
            for (int i = 0; i < properties.Length; i++)
            {
                string name = $"{setType.Name}.{properties[i].Name}";
                int id = names.Count;
                names.Add(name);
                idsByName.Add(name, id);
                ids[i] = new AttributeId(id);
            }

            setsByType.Add(setType, new SetInfo(ids, properties));
        }

        _idsByName = idsByName;
        _setsByType = setsByType;
        _names = names.ToArray();
    }

    private static List<Type> FindSetTypes()
    {
        // Godot compiles addons and game code into one assembly, so the sets live next to AttributeSet.
        var setTypes = new List<Type>();
        foreach (Type type in typeof(AttributeSet).Assembly.GetTypes())
        {
            if (type.IsSubclassOf(typeof(AttributeSet)) && !type.IsAbstract && !type.ContainsGenericParameters)
                setTypes.Add(type);
        }

        setTypes.Sort(static (a, b) =>
        {
            int byName = string.CompareOrdinal(a.Name, b.Name);
            return byName != 0 ? byName : string.CompareOrdinal(a.FullName, b.FullName);
        });

        return setTypes;
    }

    private static PropertyInfo[] FindAttributeProperties(Type setType)
    {
        var properties = new List<PropertyInfo>();
        foreach (PropertyInfo property in setType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.PropertyType != typeof(AttributeValue) || property.GetIndexParameters().Length != 0)
                continue;

            // A setter could swap the instance after the container has bound it.
            if (property.SetMethod != null)
            {
                GD.PushError($"{setType.Name}.{property.Name} must be get-only ({{ get; }}); it is skipped.");
                continue;
            }

            properties.Add(property);
        }

        // Declaration order, so the dropdown lists attributes as they are written.
        properties.Sort(static (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
        return properties.ToArray();
    }

    /// <summary>Attributes of one set type: <see cref="Ids"/>[i] belongs to <see cref="Properties"/>[i].</summary>
    internal sealed class SetInfo
    {
        public readonly AttributeId[] Ids;
        public readonly PropertyInfo[] Properties;

        public SetInfo(AttributeId[] ids, PropertyInfo[] properties)
        {
            Ids = ids;
            Properties = properties;
        }
    }
}
