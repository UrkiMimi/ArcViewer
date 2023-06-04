using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ObjectManager : MonoBehaviour
{
    public static ObjectManager Instance { get; private set; }

    [Header("Animation Settings")]
    [SerializeField] public float moveZ;
    [SerializeField] public float moveTime;
    [SerializeField] public float rotationAnimationTime;
    [SerializeField] public float behindCameraZ;
    [SerializeField] public float objectFloorOffset;

    [Header("Managers")]
    public NoteManager noteManager;
    public BombManager bombManager;
    public WallManager wallManager;
    public ChainManager chainManager;
    public ArcManager arcManager;

    public bool useSimpleNoteMaterial => SettingsManager.GetBool("simplenotes");
    public bool doRotationAnimation => SettingsManager.GetBool("rotateanimations");
    public bool doMovementAnimation => SettingsManager.GetBool("moveanimations");
    public bool doFlipAnimation => SettingsManager.GetBool("flipanimations");

    public static readonly Vector2 GridBottomLeft = new Vector2(-0.9f, 0);
    public const float LaneWidth = 0.6f;
    public const float RowHeight = 0.55f;
    public const float StartYSpacing = 0.6f;
    public const float WallHScale = 0.6f;
    public const float PrecisionUnits = 0.6f;

    public static readonly Dictionary<int, float> VanillaRowHeights = new Dictionary<int, float>
    {
        {0, 0},
        {1, 0.55f},
        {2, 1.05f}
    };

    public static readonly Dictionary<int, float> DirectionAngles = new Dictionary<int, float>
    {
        {0, 180},
        {1, 0},
        {2, -90},
        {3, 90},
        {4, -135},
        {5, 135},
        {6, -45},
        {7, 45},
        {8, 0}
    };

    public static readonly Dictionary<int, int> ReverseCutDirection = new Dictionary<int, int>
    {
        {0, 1},
        {1, 0},
        {2, 3},
        {3, 2},
        {4, 7},
        {5, 6},
        {6, 5},
        {7, 4},
        {8, 8}
    };

    public float BehindCameraTime => TimeFromWorldspace(behindCameraZ);


    public static bool CheckSameBeat(float beat1, float beat2)
    {
        return CheckSameTime(TimeManager.TimeFromBeat(beat1), TimeManager.TimeFromBeat(beat2));
    }


    public static bool CheckSameTime(float time1, float time2)
    {
        const float leeway = 0.001f;
        return Mathf.Abs(time1 - time2) <= leeway;
    }


    public bool CheckInSpawnRange(float time, bool extendBehindCamera = false, bool includeMoveTime = true)
    {
        float despawnTime = extendBehindCamera ? TimeManager.CurrentTime + BehindCameraTime : TimeManager.CurrentTime;
        float spawnTime = TimeManager.CurrentTime + BeatmapManager.ReactionTime;
        if(includeMoveTime)
        {
            spawnTime += Instance.moveTime;
        }

        return time <= spawnTime && time > despawnTime;
    }


    public bool DurationObjectInSpawnRange(float startTime, float endTime, bool extendBehindCamera = true, bool includeMoveTime = true)
    {
        if(extendBehindCamera)
        {
            endTime = endTime - BehindCameraTime;
        }

        bool timeInRange = TimeManager.CurrentTime >= startTime && TimeManager.CurrentTime <= endTime;
        return timeInRange || CheckInSpawnRange(startTime, extendBehindCamera, includeMoveTime);
    }


    public float GetZPosition(float objectTime)
    {
        float reactionTime = BeatmapManager.ReactionTime;
        float jumpTime = TimeManager.CurrentTime + reactionTime;

        if(objectTime <= jumpTime)
        {
            //Note has jumped in. Place based on Jump Setting stuff
            float timeDist = objectTime - TimeManager.CurrentTime;
            return WorldSpaceFromTime(timeDist);
        }
        else
        {
            //Note hasn't jumped in yet. Place based on the jump-in stuff
            float timeDist = (objectTime - jumpTime) / moveTime;
            return (BeatmapManager.JumpDistance / 2) + (moveZ * timeDist);
        }
    }


    public float WorldSpaceFromTime(float time)
    {
        return time * BeatmapManager.NJS;
    }


    public float TimeFromWorldspace(float position)
    {
        return position / BeatmapManager.NJS;
    }


    public static float SpawnParabola(float targetHeight, float baseHeight, float halfJumpDistance, float t)
    {
        float dSquared = Mathf.Pow(halfJumpDistance, 2);
        float tSquared = Mathf.Pow(t, 2);

        float movementRange = targetHeight - baseHeight;

        return -(movementRange / dSquared) * tSquared + targetHeight;
    }


    public float GetObjectY(float startY, float targetY, float objectTime)
    {
        float jumpTime = TimeManager.CurrentTime + BeatmapManager.ReactionTime;

        if(objectTime > jumpTime)
        {
            return startY;
        }
        else if(objectTime < TimeManager.CurrentTime)
        {
            return targetY;
        }

        float halfJumpDistance = BeatmapManager.JumpDistance / 2;
        return SpawnParabola(targetY, startY, halfJumpDistance, GetZPosition(objectTime));
    }


    public static float MappingExtensionsPrecision(float value)
    {
        //When position values are further than 1000 away, they're on the "precision placement" grid
        if(Mathf.Abs(value) >= 1000)
        {
            value -= 1000 * Mathf.Sign(value);
            value /= 1000;
        }
        return value;
    }


    public static Vector2 MappingExtensionsPosition(Vector2 position)
    {
        position.x = MappingExtensionsPrecision(position.x);
        position.y = MappingExtensionsPrecision(position.y);
        return position;
    }


    public static float? MappingExtensionsAngle(int cutDirection)
    {
        //When the cut direction is above 1000, it's in the "precision angle" space
        if(cutDirection >= 1000)
        {
            float angle = (cutDirection - 1000) % 360;
            if(angle > 180)
            {
                angle -= 360;
            }
            return angle * -1;
        }
        return null;
    }


    public static Vector2 DirectionVector(float angle)
    {
        return new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad), -Mathf.Cos(angle * Mathf.Deg2Rad));
    }


    public static Vector2 CalculateObjectPosition(float x, float y, float[] coordinates = null)
    {
        Vector2 position = GridBottomLeft;
        if(coordinates != null && coordinates.Length >= 2)
        {
            //Noodle coordinates treat x differently for some reason
            x = coordinates[0] + 2;
            y = coordinates[1];

            position.x += x * PrecisionUnits;
            position.y += y * PrecisionUnits;
            return position;
        }

        if(BeatmapManager.MappingExtensions)
        {
            Vector2 adjustedPosition = MappingExtensionsPosition(new Vector2(x, y));

            position.x += adjustedPosition.x * PrecisionUnits;
            position.y += adjustedPosition.y * PrecisionUnits;
            return position;
        }

        position.x += (int)Mathf.Clamp(x, 0, 3) * LaneWidth;
        position.y += (int)Mathf.Clamp(y, 0, 2) * RowHeight;

        //Replace the other position.y calculation with this one for game-accurate spacing
        //This spacing looks like garbage so I'm not using it even though fixed grid is technically inaccurate
        // position.y += VanillaRowHeights[(int)Mathf.Clamp(y, 0, 2)];

        return position;
    }


    public static float CalculateObjectAngle(int cutDirection, float angleOffset = 0)
    {
        if(BeatmapManager.MappingExtensions)
        {
            float? angle = MappingExtensionsAngle(cutDirection);
            if(angle != null)
            {
                //Mapping extensions angle applies
                return (float)angle;
            }
        }
        return DirectionAngles[Mathf.Clamp(cutDirection, 0, 8)] + angleOffset;
    }


    public static bool SamePlaneAngles(float a, float b)
    {
        float diff = Mathf.Abs(a - b);
        return diff.Approximately(0) || diff.Approximately(180);
    }


    public void UpdateDifficulty(Difficulty difficulty)
    {
        HitSoundManager.ClearScheduledSounds();

        LoadMapObjects(difficulty.beatmapDifficulty, out noteManager.Objects, out bombManager.Objects, out chainManager.Chains, out arcManager.Objects, out wallManager.Objects);

        noteManager.ReloadNotes();
        chainManager.ReloadChains();
        arcManager.ReloadArcs();
        wallManager.ReloadWalls();
    }


    public void UpdateBeat(float currentBeat)
    {
        noteManager.UpdateVisuals();
        bombManager.UpdateVisuals();
        wallManager.UpdateVisuals();
        chainManager.UpdateVisuals();
        arcManager.UpdateVisuals();
    }


    public void UpdateColors()
    {
        HitSoundManager.ClearScheduledSounds();

        noteManager.UpdateMaterials();
        chainManager.ClearRenderedVisuals();
        chainManager.UpdateVisuals();

        arcManager.UpdateMaterials();
        wallManager.UpdateMaterial();
    }


    public void RescheduleHitsounds(bool playing)
    {
        if(!playing)
        {
            return;
        }

        noteManager.RescheduleHitsounds();
        chainManager.RescheduleHitsounds();
    }


    // only used for loading purposes
    private class BeatmapSliderEnd : BeatmapObject
    {
        public int id; // used for pairing back up
        public int d;
        public float StartY;
        public bool HasAttachment;
    }


    public static void LoadMapObjects(BeatmapDifficulty beatmapDifficulty, out MapElementList<Note> notes, out MapElementList<Bomb> bombs, out MapElementList<Chain> chains, out MapElementList<Arc> arcs, out MapElementList<Wall> walls)
    {
        // split arcs into heads and tails for easier processing
        List<BeatmapSliderEnd> beatmapSliderHeads = new List<BeatmapSliderEnd>();
        List<BeatmapSliderEnd> beatmapSliderTails = new List<BeatmapSliderEnd>();
        for(int i = 0; i < beatmapDifficulty.sliders.Length; i++)
        {
            BeatmapSlider a = beatmapDifficulty.sliders[i];

            BeatmapSliderEnd head = new BeatmapSliderEnd
            {
                id = i,
                b = a.b,
                x = a.x,
                y = a.y,
                d = a.d,
                HasAttachment = false
            };

            BeatmapSliderEnd tail = new BeatmapSliderEnd
            {
                id = i,
                b = a.tb,
                x = a.tx,
                y = a.ty,
                d = a.tc,
                HasAttachment = false
            };

            if(a.tb < a.b)
            {
                //Swap the head and tail if the arc is backwards
                //Flip the directions as well since heads and tails are different
                head.d = ReverseCutDirection[Mathf.Clamp(head.d, 0, 8)];
                tail.d = ReverseCutDirection[Mathf.Clamp(tail.d, 0, 8)];
                beatmapSliderTails.Add(head);
                beatmapSliderHeads.Add(tail);
            }
            else
            {
                beatmapSliderHeads.Add(head);
                beatmapSliderTails.Add(tail);
            }
        }

        List<BeatmapObject> allObjects = new List<BeatmapObject>();
        allObjects.AddRange(beatmapDifficulty.colorNotes);
        allObjects.AddRange(beatmapDifficulty.bombNotes);
        allObjects.AddRange(beatmapDifficulty.burstSliders);
        allObjects.AddRange(beatmapDifficulty.obstacles);
        allObjects.AddRange(beatmapSliderHeads);
        allObjects.AddRange(beatmapSliderTails);
        allObjects = allObjects.OrderBy(x => x.b).ToList();

        notes = new MapElementList<Note>();
        bombs = new MapElementList<Bomb>();
        chains = new MapElementList<Chain>();
        arcs = new MapElementList<Arc>();
        walls = new MapElementList<Wall>();

        List<BeatmapObject> sameBeatObjects = new List<BeatmapObject>();
        for(int i = 0; i < allObjects.Count; i++)
        {
            BeatmapObject current = allObjects[i];

            sameBeatObjects.Clear();
            sameBeatObjects.Add(current);

            for(int x = i + 1; x < allObjects.Count; x++)
            {
                //Gather all consecutive objects that share the same beat
                BeatmapObject check = allObjects[x];
                if(CheckSameBeat(check.b, current.b))
                {
                    sameBeatObjects.Add(check);
                    //Skip to the first object that doesn't share this beat next loop
                    i = x;
                }
                else break;
            }

            //Precalculate values for all objects on this beat
            List<BeatmapColorNote> notesOnBeat = sameBeatObjects.OfType<BeatmapColorNote>().ToList();
            List<BeatmapBombNote> bombsOnBeat = sameBeatObjects.OfType<BeatmapBombNote>().ToList();
            List<BeatmapBurstSlider> burstSlidersOnBeat = sameBeatObjects.OfType<BeatmapBurstSlider>().ToList();
            List<BeatmapObstacle> obstaclesOnBeat = sameBeatObjects.OfType<BeatmapObstacle>().ToList();
            List<BeatmapSliderEnd> sliderEndsOnBeat = sameBeatObjects.OfType<BeatmapSliderEnd>().ToList();

            List<BeatmapObject> notesAndBombs = sameBeatObjects.Where(x => (x is BeatmapColorNote) || (x is BeatmapBombNote)).ToList();

            List<Note> newNotes = new List<Note>();
            (float? redSnapAngle, float? blueSnapAngle) = NoteManager.GetSnapAngles(notesOnBeat);

            //Used to disable swap animations whenever notes are attached to arcs or chains
            bool arcAttachment = false;
            bool chainAttachment = false;
            foreach(BeatmapColorNote n in notesOnBeat)
            {
                Note newNote = Note.NoteFromBeatmapColorNote(n);
                newNote.StartY = ((float)NoteManager.GetStartY(n, notesAndBombs) * StartYSpacing) + Instance.objectFloorOffset;

                newNote.IsChainHead = NoteManager.CheckChainHead(n, burstSlidersOnBeat);
                chainAttachment |= newNote.IsChainHead;

                // set angle snapping here because angle offset is an int in ColorNote
                if(newNote.Color == 0)
                {
                    newNote.Angle = redSnapAngle ?? newNote.Angle;
                }
                else
                {
                    newNote.Angle = blueSnapAngle ?? newNote.Angle;
                }

                // check attachment to arcs
                foreach(BeatmapSliderEnd a in sliderEndsOnBeat)
                {
                    if(!a.HasAttachment && a.x == n.x && n.y == a.y)
                    {
                        a.StartY = newNote.StartY;
                        a.HasAttachment = true;
                        arcAttachment = true;
                    }
                }

                newNotes.Add(newNote);
            }

            if(notesOnBeat.Count == 2 && !arcAttachment && !chainAttachment)
            {
                BeatmapColorNote first = notesOnBeat[0];
                BeatmapColorNote second = notesOnBeat[1];
                (newNotes[0].FlipYHeight, newNotes[1].FlipYHeight) = NoteManager.GetFlipYHeights(first, second);

                if(newNotes[0].FlipYHeight != 0)
                {
                    newNotes[0].FlipStartX = newNotes[1].Position.x;
                }

                if(newNotes[1].FlipYHeight != 0)
                {
                    newNotes[1].FlipStartX = newNotes[0].Position.x;
                }
            }

            notes.AddRange(newNotes);

            foreach(BeatmapBombNote b in bombsOnBeat)
            {
                Bomb newBomb = Bomb.BombFromBeatmapBombNote(b);
                newBomb.StartY = ((float)NoteManager.GetStartY(b, notesAndBombs) * StartYSpacing) + Instance.objectFloorOffset;

                // check attachment to arcs
                foreach(BeatmapSliderEnd a in sliderEndsOnBeat)
                {
                    if(!a.HasAttachment && a.x == b.x && b.y == a.y)
                    {
                        a.StartY = newBomb.StartY;
                        a.HasAttachment = true;
                    }
                }

                bombs.Add(newBomb);
            }

            foreach(BeatmapBurstSlider b in burstSlidersOnBeat)
            {
                Chain newChain = Chain.ChainFromBeatmapBurstSlider(b);
                chains.Add(newChain);
            }

            foreach(BeatmapObstacle o in obstaclesOnBeat)
            {
                Wall newWall = Wall.WallFromBeatmapObstacle(o);
                walls.Add(newWall);
            }
        }

        // pair slider heads/tails back up and make final arcs
        for(int i = 0; i < beatmapSliderHeads.Count; i++)
        {
            const float halfNoteOffset = 0.225f;

            BeatmapSliderEnd head = beatmapSliderHeads[i];
            BeatmapSliderEnd tail = beatmapSliderTails[i];

            Arc newArc = Arc.ArcFromBeatmapSlider(beatmapDifficulty.sliders[i]);

            if(head.HasAttachment)
            {
                Vector2 offset = newArc.HeadOffsetDirection * halfNoteOffset;
                newArc.Position += offset;
                newArc.HeadControlPoint += offset;
                newArc.HeadStartY = head.StartY + offset.y;
                newArc.HasHeadAttachment = true;
            }
            if(tail.HasAttachment)
            {
                Vector2 offset = newArc.TailOffsetDirection * halfNoteOffset;
                newArc.TailPosition += offset;
                newArc.TailControlPoint += offset;
                newArc.TailStartY = tail.StartY + offset.y;
            }

            arcs.Add(newArc);
        }
    }


    private void Awake()
    {
        if(Instance && Instance != this)
        {
            Debug.Log("Duplicate ObjectManager in scene.");
            this.enabled = false;
        }
        else Instance = this;
    }


    private void Start()
    {
        //Using this event instead of BeatmapManager.OnDifficultyChanged
        //ensures that bpm changes are loaded before precalculating object times
        TimeManager.OnDifficultyBpmEventsLoaded += UpdateDifficulty;
        TimeManager.OnBeatChanged += UpdateBeat;
        TimeManager.OnPlayingChanged += RescheduleHitsounds;

        ColorManager.OnColorsChanged += (_) => UpdateColors();
    }


    private void OnDisable()
    {
        if(Instance == this)
        {
            Instance = null;
        }
    }
}


public abstract class MapObject : MapElement
{   
    public GameObject Visual;
    public Vector2 Position;
}


public abstract class HitSoundEmitter : MapObject
{
    public AudioSource source;
}


public abstract class BaseSlider : MapObject
{
    private float _tailBeat;
    public float TailBeat
    {
        get => _tailBeat;
        set
        {
            _tailBeat = value;
            TailTime = TimeManager.TimeFromBeat(_tailBeat);
        }
    }
    public float TailTime { get; private set; }

    public int Color;
    public Vector2 TailPosition;
}


public abstract class MapElementManager<T> : MonoBehaviour where T : MapElement
{
    public MapElementList<T> Objects = new MapElementList<T>();
    public List<T> RenderedObjects = new List<T>();

    public ObjectManager objectManager => ObjectManager.Instance;

    public abstract void UpdateVisual(T visual);
    public abstract bool VisualInSpawnRange(T visual);
    public abstract void ReleaseVisual(T visual);
    public abstract void UpdateVisuals();


    public virtual void ClearOutsideVisuals()
    {
        for(int i = RenderedObjects.Count - 1; i >= 0; i--)
        {
            T visual = RenderedObjects[i];
            if(!VisualInSpawnRange(visual))
            {
                ReleaseVisual(visual);
                RenderedObjects.Remove(visual);
            }
        }
    }


    public void ClearRenderedVisuals()
    {
        foreach(T visual in RenderedObjects)
        {
            ReleaseVisual(visual);
        }
        RenderedObjects.Clear();
    }


    public int GetStartIndex(float currentTime) => Objects.GetFirstIndex(currentTime, VisualInSpawnRange);
}