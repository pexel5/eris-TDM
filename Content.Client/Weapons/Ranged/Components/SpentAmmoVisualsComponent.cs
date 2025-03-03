using Content.Client.Weapons.Ranged.Systems;

namespace Content.Client.Weapons.Ranged.Components;

[RegisterComponent, Friend(typeof(GunSystem))]
public sealed class SpentAmmoVisualsComponent : Component
{
    /// <summary>
    /// Should we do "{_state}-spent" or just "spent"
    /// </summary>
    [DataField("suffix")] public bool Suffix = true;

    [DataField("state")]
    public string State = "base";
}

public enum AmmoVisualLayers : byte
{
    Base,
}
