using UnityEngine;

/// <summary>
/// Zeigt über einem DungeonTile den aktuellen Health-Wert an.
/// Wartet automatisch auf tileMap-Zuordnung nach dem Spawn.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TileResourceDisplay : MonoBehaviour
{
    private DungeonTileData tileData;
    private TextMesh resourceText;
    private Vector3Int gridPos;
    private bool initialized = false;

    [Tooltip("Vertikaler Offset über dem Tile")]
    public float verticalOffset = 1.0f;

    void Start()
    {
        gridPos = DigManager.Instance.tileController.tilemap.WorldToCell(transform.position);

        // Text-Objekt vorbereiten
        GameObject textObj = new GameObject("TileResourceText");
        textObj.transform.SetParent(transform);
        textObj.transform.localPosition = new Vector3(0f, verticalOffset, 0f);

        resourceText = textObj.AddComponent<TextMesh>();
        resourceText.alignment = TextAlignment.Center;
        resourceText.anchor = TextAnchor.LowerCenter;
        resourceText.characterSize = 0.1f;
        resourceText.fontSize = 14;
        resourceText.color = Color.white;
        resourceText.text = "?";

        Font legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        resourceText.font = legacy;
        resourceText.GetComponent<MeshRenderer>().material = legacy.material;
    }

    void Update()
    {
        if (!initialized)
        {
            if (DigManager.Instance != null &&
                DigManager.Instance.tileMap != null &&
                DigManager.Instance.tileMap.TryGetValue(gridPos, out tileData))
            {
                initialized = true;
            }
            else
            {
                resourceText.text = "?";
                return;
            }
        }

        if (tileData != null)
        {
            resourceText.text = tileData.Health.ToString();

            if (Camera.main != null)
            {
                Vector3 dir = resourceText.transform.position - Camera.main.transform.position;
                resourceText.transform.rotation = Quaternion.LookRotation(dir);
            }
        }
    }
}
