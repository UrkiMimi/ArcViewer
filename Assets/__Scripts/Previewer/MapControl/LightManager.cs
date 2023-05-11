using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LightManager : MonoBehaviour
{
    public static bool BoostActive;

    public static event Action<LightingPropertyEventArgs> OnLightPropertiesChanged;
    public static event Action<LaserSpeedEvent, LightEventType> OnLaserRotationsChanged;

    private static ColorPalette colors => ColorManager.CurrentColors;
    private static Color lightColor1 => BoostActive ? colors.BoostLightColor1 : colors.LightColor1;
    private static  Color lightColor2 => BoostActive ? colors.BoostLightColor2 : colors.LightColor2;
    private static Color whiteLightColor => BoostActive ? colors.BoostWhiteLightColor : colors.WhiteLightColor;

    private static BeatmapColorBoostBeatmapEvent[] boostEvents => BeatmapManager.CurrentDifficulty.beatmapDifficulty.colorBoostBeatMapEvents;

    private static MaterialPropertyBlock lightProperties;
    private static MaterialPropertyBlock glowProperties;

    private static readonly string[] lightSettings = new string[]
    {
        "lightglowbrightness"
    };

    public List<LightEvent> backLaserEvents = new List<LightEvent>();
    public List<LightEvent> ringEvents = new List<LightEvent>();
    public List<LightEvent> leftLaserEvents = new List<LightEvent>();
    public List<LightEvent> rightLaserEvents = new List<LightEvent>();
    public List<LightEvent> centerLightEvents = new List<LightEvent>();

    public List<LaserSpeedEvent> leftLaserSpeedEvents = new List<LaserSpeedEvent>();
    public List<LaserSpeedEvent> rightLaserSpeedEvents = new List<LaserSpeedEvent>();

    [SerializeField, Range(0f, 1f)] private float lightSaturation;
    [SerializeField, Range(0f, 1f)] private float lightEmissionSaturation;
    [SerializeField] private float lightEmission;


    public void UpdateLights(float beat)
    {
        BoostActive = boostEvents.LastOrDefault(x => x.b <= beat)?.o ?? false;

        UpdateLightEventType(LightEventType.BackLasers, backLaserEvents, beat);
        UpdateLightEventType(LightEventType.Rings, ringEvents, beat);
        UpdateLightEventType(LightEventType.LeftRotatingLasers, leftLaserEvents, beat);
        UpdateLightEventType(LightEventType.RightRotatingLasers, rightLaserEvents, beat);
        UpdateLightEventType(LightEventType.CenterLights, centerLightEvents, beat);

        UpdateLaserSpeedEventType(LightEventType.LeftRotationSpeed, leftLaserSpeedEvents, beat);
        UpdateLaserSpeedEventType(LightEventType.RightRotationSpeed, rightLaserSpeedEvents, beat);
    }


    private void UpdateLightEventType(LightEventType type, List<LightEvent> events, float beat)
    {
        int lastIndex = events.FindLastIndex(x => x.Beat <= beat);
        bool foundEvent = lastIndex >= 0;

        LightEvent currentEvent = foundEvent ? events[lastIndex] : null;
        LightEventValue value = foundEvent ? currentEvent.Value : LightEventValue.Off;

        bool hasNextEvent = lastIndex + 1 < events.Count;
        LightEvent nextEvent = hasNextEvent ? events[lastIndex + 1] : null;

        Color baseColor = GetEventColor(value, currentEvent, nextEvent);
        SetLightProperties(baseColor);

        LightingPropertyEventArgs eventArgs = new LightingPropertyEventArgs
        {
            laserProperties = lightProperties,
            glowProperties = glowProperties,
            emission = lightEmission * Mathf.Max(baseColor.a, 1f),
            type = type
        };
        OnLightPropertiesChanged?.Invoke(eventArgs);
    }


    private void UpdateLaserSpeedEventType(LightEventType type, List<LaserSpeedEvent> events, float beat)
    {
        LaserSpeedEvent lastEvent = events.LastOrDefault(x => x.Beat <= beat);
        OnLaserRotationsChanged?.Invoke(lastEvent, type);
    }


    private void SetLightProperties(Color baseColor)
    {
        glowProperties.SetColor("_BaseColor", baseColor);
        glowProperties.SetFloat("_Alpha", baseColor.a * SettingsManager.GetFloat("lightglowbrightness"));
        lightProperties.SetColor("_BaseColor", GetLightColor(baseColor));
        lightProperties.SetColor("_EmissionColor", GetLightEmission(baseColor));
    }


    private Color GetLightColor(Color baseColor)
    {
        float h, s, v;
        Color.RGBToHSV(baseColor, out h, out s, out v);
        Color newColor = baseColor.SetHSV(h, s * lightSaturation, v);
        newColor.a = Mathf.Clamp(baseColor.a, 0f, 1f);
        return newColor;
    }


    private Color GetLightEmission(Color baseColor)
    {
        //Alpha values below 1 will naturally reduce glowiness
        float emission = lightEmission * Mathf.Max(baseColor.a, 1f);

        float h, s;
        Color.RGBToHSV(baseColor, out h, out s, out _);
        Color newColor = baseColor.SetHSV(h, s * lightEmissionSaturation, emission, true);
        newColor.a = Mathf.Clamp(baseColor.a, 0f, 1f);
        return newColor;
    }


    private Color GetEventColor(LightEventValue value, LightEvent lightEvent, LightEvent nextEvent)
    {
        switch(value)
        {
            case LightEventValue.RedOn:
            case LightEventValue.RedTransition:
                //We can be sure lightEvent isn't null because value would be set to Off
                return GetOnColor(lightEvent, lightColor1, nextEvent);
            case LightEventValue.RedFlash:
                return GetFlashColor(lightEvent, lightColor1);
            case LightEventValue.RedFade:
                return GetFadeColor(lightEvent, lightColor1);

            case LightEventValue.BlueOn:
            case LightEventValue.BlueTransition:
                return GetOnColor(lightEvent, lightColor2, nextEvent);
            case LightEventValue.BlueFlash:
                return GetFlashColor(lightEvent, lightColor2);
            case LightEventValue.BlueFade:
                return GetFadeColor(lightEvent, lightColor2);

            case LightEventValue.WhiteOn:
            case LightEventValue.WhiteTransition:
                return GetOnColor(lightEvent, whiteLightColor, nextEvent);
            case LightEventValue.WhiteFlash:
                return GetFlashColor(lightEvent, whiteLightColor);
            case LightEventValue.WhiteFade:
                return GetFadeColor(lightEvent, whiteLightColor);

            case LightEventValue.Off:
            default:
                return new Color(0f, 0f, 0f, 0f);
        }
    }


    private Color GetEventBaseColor(LightEvent lightEvent)
    {
        Color baseColor = new Color();
        switch(lightEvent.Value)
        {
            case LightEventValue.RedOn:
            case LightEventValue.RedTransition:
                baseColor = lightColor1;
                break;
            case LightEventValue.BlueOn:
            case LightEventValue.BlueTransition:
                baseColor = lightColor2;
                break;
            case LightEventValue.WhiteOn:
            case LightEventValue.WhiteTransition:
                baseColor = whiteLightColor;
                break;
            default:
                return new Color(0f, 0f, 0f, 0f);
        }
        baseColor.a = lightEvent.FloatValue;
        return baseColor;
    }


    private Color GetOnColor(LightEvent lightEvent, Color baseColor, LightEvent nextEvent)
    {
        baseColor.a = lightEvent.FloatValue;

        if(nextEvent?.isTransition ?? false)
        {
            Color transitionColor = GetEventBaseColor(nextEvent);
            float transitionTime = nextEvent.Beat - lightEvent.Beat;

            float t = (TimeManager.CurrentBeat - lightEvent.Beat) / transitionTime;
            baseColor = Color.Lerp(baseColor, transitionColor, Easings.Quad.InOut(t));
        }

        return baseColor;
    }


    private Color GetFlashColor(LightEvent lightEvent, Color baseColor)
    {
        const float flashIntensity = 1.5f;
        const float fadeTime = 0.5f;

        float floatValue = lightEvent.FloatValue;
        float flashBrightness = floatValue * flashIntensity;

        baseColor.a = GetV1TransitionAlpha(flashBrightness, floatValue, fadeTime, lightEvent.Time);
        return baseColor;
    }


    private Color GetFadeColor(LightEvent lightEvent, Color baseColor)
    {
        const float flashIntensity = 1.2f;
        const float fadeTime = 0.8f;

        float floatValue = lightEvent.FloatValue;
        float flashBrightness = floatValue * flashIntensity;

        baseColor.a = GetV1TransitionAlpha(flashBrightness, 0f, fadeTime, lightEvent.Time);
        return baseColor;
    }


    private float GetV1TransitionAlpha(float startAlpha, float endAlpha, float fadeTime, float eventTime)
    {
        float timeDifference = TimeManager.CurrentTime - eventTime;
        if(timeDifference >= fadeTime)
        {
            return endAlpha;
        }
        if(timeDifference >= 0)
        {
            float t = timeDifference / fadeTime;
            return Mathf.Lerp(startAlpha, endAlpha, Easings.Quad.Out(t));
        }
        return 0f;
    }


    public void UpdateColors()
    {
        UpdateLights(TimeManager.CurrentBeat);
    }


    public void UpdateSettings(string setting)
    {
        if(setting == "all" || lightSettings.Contains(setting))
        {
            UpdateLights(TimeManager.CurrentBeat);
        }
    }


    public void UpdateDifficulty(Difficulty newDifficulty)
    {
        if(newDifficulty.beatmapDifficulty.basicBeatMapEvents.Length == 0)
        {
            SetStaticLayout();
            UpdateLights(TimeManager.CurrentBeat);
            return;
        }

        backLaserEvents.Clear();
        ringEvents.Clear();
        leftLaserEvents.Clear();
        rightLaserEvents.Clear();
        centerLightEvents.Clear();

        leftLaserSpeedEvents.Clear();
        rightLaserSpeedEvents.Clear();

        foreach(BeatmapBasicBeatmapEvent beatmapEvent in newDifficulty.beatmapDifficulty.basicBeatMapEvents)
        {
            AddLightEvent(beatmapEvent);
        }

        backLaserEvents = SortLightsByBeat(backLaserEvents);
        ringEvents = SortLightsByBeat(ringEvents);
        leftLaserEvents = SortLightsByBeat(leftLaserEvents);
        rightLaserEvents = SortLightsByBeat(rightLaserEvents);
        centerLightEvents = SortLightsByBeat(centerLightEvents);

        leftLaserSpeedEvents = SortLightsByBeat(leftLaserSpeedEvents);
        rightLaserEvents = SortLightsByBeat(rightLaserEvents);

        PopulateLaserRotationEventData();

        UpdateLights(TimeManager.CurrentBeat);
    }


    private static List<T> SortLightsByBeat<T>(List<T> events) where T : LightEvent
    {
        return events.OrderBy(x => x.Beat).ToList();
    }


    private void AddLightEvent(BeatmapBasicBeatmapEvent beatmapEvent)
    {
        LightEvent newEvent = LightEvent.LightEventFromBasicBeatmapEvent(beatmapEvent);
        switch(newEvent.Type)
        {
            case LightEventType.BackLasers:
                backLaserEvents.Add(newEvent);
                break;
            case LightEventType.Rings:
                ringEvents.Add(newEvent);
                break;
            case LightEventType.LeftRotatingLasers:
                leftLaserEvents.Add(newEvent);
                break;
            case LightEventType.RightRotatingLasers:
                rightLaserEvents.Add(newEvent);
                break;
            case LightEventType.CenterLights:
                centerLightEvents.Add(newEvent);
                break;
            case LightEventType.LeftRotationSpeed:
                leftLaserSpeedEvents.Add(new LaserSpeedEvent(newEvent));
                break;
            case LightEventType.RightRotationSpeed:
                rightLaserSpeedEvents.Add(new LaserSpeedEvent(newEvent));
                break;
        }
    }


    private void PopulateLaserRotationEventData()
    {
        const int laserCount = 4;
        foreach(LaserSpeedEvent speedEvent in leftLaserSpeedEvents)
        {
            speedEvent.PopulateRotationData(laserCount);
        }

        int i = 0;
        foreach(LaserSpeedEvent speedEvent in rightLaserSpeedEvents)
        {
            if(i >= leftLaserSpeedEvents.Count)
            {
                speedEvent.PopulateRotationData(laserCount);
                continue;
            }

            speedEvent.rotationValues = new List<LaserSpeedEvent.LaserRotationData>();
            while(i < leftLaserSpeedEvents.Count)
            {
                //Find a left laser event (if any) that matches this event's beat
                LaserSpeedEvent leftSpeedEvent = leftLaserSpeedEvents[i];

                if(ObjectManager.CheckSameTime(speedEvent.Time, leftSpeedEvent.Time))
                {
                    //Events on the same time get the same parameters
                    speedEvent.rotationValues.AddRange(leftSpeedEvent.rotationValues);
                    break;
                }
                else if(leftSpeedEvent.Beat >= speedEvent.Beat)
                {
                    //We've passed this event's time, so there's no event sharing this beat
                    speedEvent.PopulateRotationData(laserCount);
                    break;
                }
                else i++;
            }
        }
    }


    private void SetStaticLayout()
    {
        backLaserEvents = new List<LightEvent>()
        {
            new LightEvent
            {
                Beat = 0f,
                Type = LightEventType.BackLasers,
                Value = LightEventValue.BlueOn
            }
        };
        ringEvents = new List<LightEvent>()
        {
            new LightEvent
            {
                Beat = 0f,
                Type = LightEventType.Rings,
                Value = LightEventValue.BlueOn
            }
        };
        leftLaserEvents = new List<LightEvent>()
        {
            new LightEvent
            {
                Beat = 0f,
                Type = LightEventType.LeftRotatingLasers,
                Value = LightEventValue.Off
            }
        };
        rightLaserEvents = new List<LightEvent>()
        {
            new LightEvent
            {
                Beat = 0f,
                Type = LightEventType.RightRotatingLasers,
                Value = LightEventValue.Off
            }
        };
        centerLightEvents = new List<LightEvent>()
        {
            new LightEvent
            {
                Beat = 0f,
                Type = LightEventType.CenterLights,
                Value = LightEventValue.BlueOn
            }
        };
        leftLaserSpeedEvents = new List<LaserSpeedEvent>();
        rightLaserSpeedEvents = new List<LaserSpeedEvent>();
    }


    private void Start()
    {
        TimeManager.OnBeatChanged += UpdateLights;
        BeatmapManager.OnBeatmapDifficultyChanged += UpdateDifficulty;
        SettingsManager.OnSettingsUpdated += UpdateSettings;
        ColorManager.OnColorsChanged += (_) => UpdateColors();
    }


    private void Awake()
    {
        lightProperties = new MaterialPropertyBlock();
        glowProperties = new MaterialPropertyBlock();

        SetStaticLayout();
    }
}


public class LightEvent
{
    private float _beat;
    public float Beat
    {
        get => _beat;
        set
        {
            _beat = value;
            Time = TimeManager.TimeFromBeat(_beat);
        }
    }
    public float Time { get; private set; }

    public LightEventType Type;
    public LightEventValue Value;
    public float FloatValue = 1f;

    public bool isTransition => Value == LightEventValue.RedTransition || Value == LightEventValue.BlueTransition || Value == LightEventValue.WhiteTransition;


    public static LightEvent LightEventFromBasicBeatmapEvent(BeatmapBasicBeatmapEvent beatmapEvent)
    {
        return new LightEvent
        {
            Beat = beatmapEvent.b,
            Type = (LightEventType)beatmapEvent.et,
            Value = (LightEventValue)beatmapEvent.i,
            FloatValue = beatmapEvent.f
        };
    }
}


public class LaserSpeedEvent : LightEvent
{
    //Lasers rotate at 20 degrees/sec multiplied by value
    public float rotationSpeed => (int)Value * 20f;
    public List<LaserRotationData> rotationValues;


    public struct LaserRotationData
    {
        public float startPosition;
        public bool direction;
    }


    public LaserSpeedEvent() {}

    public LaserSpeedEvent(LightEvent baseEvent)
    {
        Beat = baseEvent.Beat;
        Type = baseEvent.Type;
        Value = baseEvent.Value;
        FloatValue = baseEvent.FloatValue;
    }


    public void PopulateRotationData(int laserCount)
    {
        rotationValues = new List<LaserRotationData>();
        for(int i = 0; i < laserCount; i++)
        {
            LaserRotationData newData = new LaserRotationData();
            if((int)Value > 0)
            {
                newData.startPosition = UnityEngine.Random.Range(0f, 360f);
                newData.direction = UnityEngine.Random.value >= 0.5f;
            }
            else
            {
                newData.startPosition = 0f;
            }
            rotationValues.Add(newData);
        }
    }
}


public struct LightingPropertyEventArgs
{
    public MaterialPropertyBlock laserProperties;
    public MaterialPropertyBlock glowProperties;
    public float emission;
    public LightEventType type;
}


public enum LightEventType
{
    BackLasers = 0,
    Rings = 1,
    LeftRotatingLasers = 2,
    RightRotatingLasers = 3,
    CenterLights = 4,
    LeftSideExtra = 6,
    RightSideExtra = 7,
    RingSpin = 8,
    RingZoom = 9,
    BillieLeftLasers = 10,
    BillieRightLasers = 11,
    LeftRotationSpeed = 12,
    RightRotationSpeed = 13,
    InterscopeHydraulicsDown = 16,
    InterscopeHydraulicsUp = 17,
    GagaLeftTowerHeight = 18,
    GagaRightTowerHeight = 19
}


public enum LightEventValue
{
    Off,
    BlueOn,
    BlueFlash,
    BlueFade,
    BlueTransition,
    RedOn,
    RedFlash,
    RedFade,
    RedTransition,
    WhiteOn,
    WhiteFlash,
    WhiteFade,
    WhiteTransition
}