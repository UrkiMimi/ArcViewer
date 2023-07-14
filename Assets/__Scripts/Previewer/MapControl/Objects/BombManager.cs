using UnityEngine;

public class BombManager : MapElementManager<Bomb>
{
    [SerializeField] private ObjectPool<BombHandler> bombPool;

    [Space]
    [SerializeField] private Material complexMaterial;
    [SerializeField] private Material simpleMaterial;


    public void ReloadBombs()
    {
        ClearRenderedVisuals();
        UpdateVisuals();
    }


    public override void UpdateVisual(Bomb b)
    {
        float worldDist = objectManager.GetZPosition(b.Time);
        Vector3 worldPos = new Vector3(b.Position.x, b.Position.y, worldDist);

        worldPos.y += objectManager.playerHeightOffset;

        if(objectManager.doMovementAnimation)
        {
            worldPos.y = objectManager.GetObjectY(b.StartY, worldPos.y, b.Time);
        }

        if(b.Visual == null)
        {
            b.BombHandler = bombPool.GetObject();
            b.Visual = b.BombHandler.gameObject;
            b.source = b.BombHandler.audioSource;

            b.Visual.transform.SetParent(transform);
            b.BombHandler.EnableVisual();

            b.BombHandler.SetMaterial(objectManager.useSimpleBombMaterial ? simpleMaterial : complexMaterial);

            if(SettingsManager.GetBool("chromaobjectcolors") && b.CustomColor != null)
            {
                //This bomb has a unique chroma color
                b.BombHandler.SetProperties(b.CustomMaterialProperties);
            }
            else if(b.BombHandler.HasCustomProperties)
            {
                //This bomb has no custom color, so properties should be cleared
                b.BombHandler.ClearProperties();
            }

            b.Visual.SetActive(true);
            RenderedObjects.Add(b);
        }
        b.Visual.transform.localPosition = worldPos;
    }


    public override bool VisualInSpawnRange(Bomb b)
    {
        return objectManager.CheckInSpawnRange(b.Time, true, true, b.HitOffset);
    }


    public override void ReleaseVisual(Bomb b)
    {
        b.source.Stop();
        bombPool.ReleaseObject(b.BombHandler);

        b.Visual = null;
        b.source = null;
        b.BombHandler = null;
    }


    public override void ClearOutsideVisuals()
    {
        for(int i = RenderedObjects.Count - 1; i >= 0; i--)
        {
            Bomb b = RenderedObjects[i];
            if(!objectManager.CheckInSpawnRange(b.Time, !b.WasHit, true, b.HitOffset))
            {
                if(b.source.isPlaying || (ReplayManager.IsReplayMode && b.Time > TimeManager.CurrentTime && b.Time < TimeManager.CurrentTime + 0.5f))
                {
                    //Only clear the visual elements if the hitsound is still playing
                    b.BombHandler.DisableVisual();
                }
                else
                {
                    ReleaseVisual(b);
                    RenderedObjects.Remove(b);
                }
            }
            else b.BombHandler.EnableVisual();
        }
    }


    public override void UpdateVisuals()
    {
        ClearOutsideVisuals();

        if(Objects.Count == 0)
        {
            return;
        }

        int startIndex = GetStartIndex(TimeManager.CurrentTime);
        if(startIndex < 0)
        {
            return;
        }

        for(int i = startIndex; i < Objects.Count; i++)
        {
            Bomb b = Objects[i];
            if(objectManager.CheckInSpawnRange(b.Time, !b.WasHit, true, b.HitOffset))
            {
                UpdateVisual(b);
            }
            else if(!VisualInSpawnRange(b))
            {
                break;
            }
        }
    }
}


public class Bomb : HitSoundEmitter
{
    public float StartY;

    public BombHandler BombHandler;
    public MaterialPropertyBlock CustomMaterialProperties;

    public Bomb(BeatmapBombNote b)
    {
        Vector2 position = ObjectManager.CalculateObjectPosition(b.x, b.y, b.customData?.coordinates);

        Beat = b.b;
        Position = position;

        WasHit = false;
        WasBadCut = false;
        HitOffset = 0f;

        if(b.customData?.color != null)
        {
            CustomColor = ColorManager.ColorFromCustomDataColor(b.customData.color);

            CustomMaterialProperties = new MaterialPropertyBlock();
            CustomMaterialProperties.SetColor("_BaseColor", (Color)CustomColor);
        }
    }
}