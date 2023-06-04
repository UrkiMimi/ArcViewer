using System;
using System.Collections.Generic;
using UnityEngine;

public class RingManager : MonoBehaviour
{
    public static MapElementList<RingRotationEvent> SmallRingRotationEvents = new MapElementList<RingRotationEvent>();
    public static MapElementList<RingRotationEvent> BigRingRotationEvents = new MapElementList<RingRotationEvent>();

    public static MapElementList<RingZoomEvent> RingZoomEvents = new MapElementList<RingZoomEvent>();

    public static event Action<RingRotationEventArgs> OnRingRotationsChanged;
    public static event Action<float> OnRingZoomPositionChanged;

    public const float SmallRingStartAngle = -45f;
    public const float SmallRingRotationAmount = 90f;
    public const float SmallRingStep = 5f;
    public const float SmallRingStartStep = 3f;

    public const float BigRingStartAngle = -45f;
    public const float BigRingRotationAmount = 45f;
    public const float BigRingStep = 5f;
    public const float BigRingStartStep = 0f;

    public const bool StartRingZoomParity = true;
    public const float StartRingZoomPosition = StartRingZoomParity ? 1f : 0f;


    public static void UpdateRings()
    {
        UpdateRingZoom();
        UpdateRingRotations();
    }


    private static void UpdateRingRotations()
    {
        int lastIndex = SmallRingRotationEvents.GetLastIndex(TimeManager.CurrentTime, x => x.Beat <= TimeManager.CurrentBeat);

        RingRotationEventArgs eventArgs = new RingRotationEventArgs
        {
            events = SmallRingRotationEvents,
            currentEventIndex = lastIndex,
            affectBigRings = false
        };
        OnRingRotationsChanged?.Invoke(eventArgs);

        //Need to update big rings separately
        eventArgs.events = BigRingRotationEvents;
        eventArgs.affectBigRings = true;
        OnRingRotationsChanged?.Invoke(eventArgs);
    }


    private static void UpdateRingZoom()
    {
        if(RingZoomEvents.Count == 0)
        {
            OnRingZoomPositionChanged?.Invoke(StartRingZoomPosition);
            return;
        }

        int lastIndex = RingZoomEvents.GetLastIndex(TimeManager.CurrentTime, x => x.Beat <= TimeManager.CurrentBeat);
        if(lastIndex < 0)
        {
            //No ring zoom has taken affect, set defaults
            OnRingZoomPositionChanged?.Invoke(StartRingZoomPosition);
            return;
        }

        RingZoomEvent current = RingZoomEvents[lastIndex];
        OnRingZoomPositionChanged?.Invoke(current.GetRingDist(TimeManager.CurrentTime));
    }


    public static void SetStaticRings()
    {
        RingRotationEventArgs eventArgs = new RingRotationEventArgs
        {
            events = new List<RingRotationEvent>(),
            currentEventIndex = -1,
            affectBigRings = false
        };
        OnRingRotationsChanged?.Invoke(eventArgs);
        eventArgs.affectBigRings = true;
        OnRingRotationsChanged?.Invoke(eventArgs);

        OnRingZoomPositionChanged?.Invoke(StartRingZoomPosition);
    }


    public static void PopulateRingEventData()
    {
        PopulateRingRotationEvents(ref SmallRingRotationEvents, SmallRingRotationAmount, SmallRingStep, SmallRingStartAngle, SmallRingStartStep);
        PopulateRingRotationEvents(ref BigRingRotationEvents, BigRingRotationAmount, BigRingStep, BigRingStartAngle, BigRingStartStep);

        PopulateRingZoomEvents();
    }


    private static void PopulateRingRotationEvents(ref MapElementList<RingRotationEvent> events, float rotationAmount, float maxStep, float startRotation, float startStep)
    {
        for(int i = 0; i < events.Count; i++)
        {
            RingRotationEvent current = events[i];
            if(i == 0)
            {
                //The first event should inherit the default starting positions
                current.startAngle = startRotation;
                current.startStep = startStep;

                current.RandomizeDirectionAndStep(startRotation, rotationAmount, maxStep);
            }
            else
            {
                //Subsequent events get starting values based on the current ring rotations
                RingRotationEvent previous = events[i - 1];
                float timeDifference = current.Time - previous.Time;
                float rotationProgress = GetRingEventProgress(timeDifference, RingRotationEvent.Speed);

                current.startAngle = previous.GetEventAngle(rotationProgress);
                current.startStep = previous.GetEventStepAngle(rotationProgress);

                current.RandomizeDirectionAndStep(previous.targetAngle, rotationAmount, maxStep);
            }
        }
    }


    private static void PopulateRingZoomEvents()
    {
        for(int i = 0; i < RingZoomEvents.Count; i++)
        {
            RingZoomEvent current = RingZoomEvents[i];
            if(i == 0)
            {
                current.IsFarParity = !StartRingZoomParity;
                current.startDistance = StartRingZoomPosition;
            }
            else
            {
                RingZoomEvent previous = RingZoomEvents[i - 1];

                current.IsFarParity = !previous.IsFarParity;
                current.startDistance = previous.GetRingDist(current.Time);
            }
        }
    }


    public static float GetRingEventProgress(float timeDifference, float speed)
    {
        return 1f - Mathf.Pow(2f, -(timeDifference * speed * 2f));
    }
}


public class RingRotationEvent : LightEvent
{
    public const float Speed = 2f;
    public const int Prop = 1;
    public const float FixedDeltaTime = 1f / 60f;
    public const float FloatProp = FixedDeltaTime * Prop;

    public float startAngle;
    public float startStep;
    public float targetAngle;
    public float step;


    public RingRotationEvent() {}

    public RingRotationEvent(BeatmapBasicBeatmapEvent beatmapEvent)
    {
        Beat = beatmapEvent.b;
        Type = (LightEventType)beatmapEvent.et;
        Value = (LightEventValue)beatmapEvent.i;
        FloatValue = beatmapEvent.f;
    }


    public void RandomizeDirectionAndStep(float start, float rotationAmount, float maxStep)
    {
        float rotation = UnityEngine.Random.value >= 0.5f ? rotationAmount : -rotationAmount;
        targetAngle = start + rotation;
        step = UnityEngine.Random.Range(-maxStep, maxStep);
    }


    public float StartInfluenceTime(int ringIndex)
    {
        //Returns the time where this event first starts affecting a given ring
        return Time + (FloatProp * ringIndex);
    }


    public float GetRingAngle(float currentTime, int ringIndex)
    {
        float eventTimeDifference = currentTime - Time;
        float ringTimeDifference = Mathf.Max(eventTimeDifference - (FloatProp * ringIndex), 0f);

        float rotationProgress = RingManager.GetRingEventProgress(ringTimeDifference, Speed);

        float angle = GetEventAngle(rotationProgress);
        float step = GetEventStepAngle(rotationProgress);
        return angle + (step * ringIndex);
    }


    public float GetEventAngle(float rotationProgress)
    {
        return Mathf.Lerp(startAngle, targetAngle, rotationProgress);
    }


    public float GetEventStepAngle(float rotationProgress)
    {
        return Mathf.Lerp(startStep, step, rotationProgress);
    }
}


public class RingZoomEvent : LightEvent
{
    public const float Speed = 1.5f;

    public bool IsFarParity;
    public float startDistance;
    public float targetDistance => IsFarParity ? 1f : 0f;


    public RingZoomEvent() {}

    public RingZoomEvent(BeatmapBasicBeatmapEvent beatmapEvent)
    {
        Beat = beatmapEvent.b;
        Type = (LightEventType)beatmapEvent.et;
        Value = (LightEventValue)beatmapEvent.i;
        FloatValue = beatmapEvent.f;
    }


    public float GetRingDist(float currentTime)
    {
        float timeDifference = currentTime - Time;
        float zoomProgress = RingManager.GetRingEventProgress(timeDifference, Speed);

        return Mathf.Lerp(startDistance, targetDistance, zoomProgress);
    }
}


public class RingRotationEventArgs
{
    public List<RingRotationEvent> events;
    public int currentEventIndex;
    public bool affectBigRings;
}