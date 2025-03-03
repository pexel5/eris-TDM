using System.Linq;
using Content.Server.Chat;
using Content.Server.Station.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.AlertLevel;

public sealed class AlertLevelSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;

    // Until stations are a prototype, this is how it's going to have to be.
    public const string DefaultAlertLevelSet = "stationAlerts";

    public override void Initialize()
    {
        SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialize);

        _prototypeManager.PrototypesReloaded += OnPrototypeReload;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _prototypeManager.PrototypesReloaded -= OnPrototypeReload;
    }

    public override void Update(float time)
    {
        foreach (var station in _stationSystem.Stations)
        {
            if (!TryComp(station, out AlertLevelComponent? alert))
            {
                continue;
            }

            if (alert.CurrentDelay <= 0)
            {
                if (alert.ActiveDelay)
                {
                    RaiseLocalEvent(new AlertLevelDelayFinishedEvent());
                    alert.ActiveDelay = false;
                }
                continue;
            }

            alert.CurrentDelay -= time;
        }
    }

    private void OnStationInitialize(StationInitializedEvent args)
    {
        var alertLevelComponent = AddComp<AlertLevelComponent>(args.Station);

        if (!_prototypeManager.TryIndex(DefaultAlertLevelSet, out AlertLevelPrototype? alerts))
        {
            return;
        }

        alertLevelComponent.AlertLevels = alerts;

        var defaultLevel = alertLevelComponent.AlertLevels.DefaultLevel;
        if (string.IsNullOrEmpty(defaultLevel))
        {
            defaultLevel = alertLevelComponent.AlertLevels.Levels.Keys.First();
        }

        SetLevel(args.Station, defaultLevel, false, false, true);
    }

    private void OnPrototypeReload(PrototypesReloadedEventArgs args)
    {
        if (!args.ByType.TryGetValue(typeof(AlertLevelPrototype), out var alertPrototypes)
            || !alertPrototypes.Modified.TryGetValue(DefaultAlertLevelSet, out var alertObject)
            || alertObject is not AlertLevelPrototype alerts)
        {
            return;
        }

        foreach (var comp in EntityQuery<AlertLevelComponent>())
        {
            comp.AlertLevels = alerts;

            if (!comp.AlertLevels.Levels.ContainsKey(comp.CurrentLevel))
            {
                var defaultLevel = comp.AlertLevels.DefaultLevel;
                if (string.IsNullOrEmpty(defaultLevel))
                {
                    defaultLevel = comp.AlertLevels.Levels.Keys.First();
                }

                SetLevel(comp.Owner, defaultLevel, true, true, true);
            }
        }

        RaiseLocalEvent(new AlertLevelPrototypeReloadedEvent());
    }

    public float GetAlertLevelDelay(EntityUid station, AlertLevelComponent? alert = null)
    {
        if (!Resolve(station, ref alert))
        {
            return float.NaN;
        }

        return alert.CurrentDelay;
    }

    /// <summary>
    /// Set the alert level based on the station's entity ID.
    /// </summary>
    /// <param name="station">Station entity UID.</param>
    /// <param name="level">Level to change the station's alert level to.</param>
    /// <param name="playSound">Play the alert level's sound.</param>
    /// <param name="announce">Say the alert level's announcement.</param>
    /// <param name="force">Force the alert change. This applies if the alert level is not selectable or not.</param>
    /// <param name="locked">Will it be possible to change level by crew.</param>
    public void SetLevel(EntityUid station, string level, bool playSound, bool announce, bool force = false,
        bool locked = false, MetaDataComponent? dataComponent = null, AlertLevelComponent? component = null)
    {
        if (!Resolve(station, ref component, ref dataComponent)
            || component.AlertLevels == null
            || !component.AlertLevels.Levels.TryGetValue(level, out var detail)
            || component.CurrentLevel == level)
        {
            return;
        }

        if (!force)
        {
            if (!detail.Selectable
                || component.CurrentDelay > 0
                || component.IsLevelLocked)
            {
                return;
            }

            component.CurrentDelay = AlertLevelComponent.Delay;
            component.ActiveDelay = true;
        }

        component.CurrentLevel = level;
        component.IsLevelLocked = locked;

        var stationName = dataComponent.EntityName;

        var name = level.ToLower();

        if (Loc.TryGetString($"alert-level-{level}", out var locName))
        {
            name = locName.ToLower();
        }

        // Announcement text. Is passed into announcementFull.
        var announcement = detail.Announcement;

        if (Loc.TryGetString(detail.Announcement, out var locAnnouncement))
        {
            announcement = locAnnouncement;
        }

        // The full announcement to be spat out into chat.
        var announcementFull = Loc.GetString("alert-level-announcement", ("name", name), ("announcement", announcement));

        var playDefault = false;
        if (playSound)
        {
            if (detail.Sound != null)
            {
                SoundSystem.Play(Filter.Broadcast(), detail.Sound.GetSound());
            }
            else
            {
                playDefault = true;
            }
        }

        if (announce)
        {

            _chatSystem.DispatchStationAnnouncement(station, announcementFull, playDefaultSound: playDefault,
                colorOverride: detail.Color, sender: stationName);
        }

        RaiseLocalEvent(new AlertLevelChangedEvent(station, level));
    }
}

public sealed class AlertLevelDelayFinishedEvent : EntityEventArgs
{}

public sealed class AlertLevelPrototypeReloadedEvent : EntityEventArgs
{}

public sealed class AlertLevelChangedEvent : EntityEventArgs
{
    public EntityUid Station { get; }
    public string AlertLevel { get; }

    public AlertLevelChangedEvent(EntityUid station, string alertLevel)
    {
        Station = station;
        AlertLevel = alertLevel;
    }
}
