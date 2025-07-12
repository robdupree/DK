using UnityEngine;

/// <summary>
/// Hängt man an jede Gold-Tile-Instanz, um über dem Tile in Weiß anzuzeigen,
/// wie viele Health-/Resource-Einheiten noch übrig sind.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GoldResourceDisplay : MonoBehaviour
{
    private DungeonTileData tileData;
    private TextMesh resourceText;
    private float verticalOffset = 1.0f;

    void Start()
    {
        // Grid-Position aus der Weltposition ableiten
        Vector3 worldPos = transform.position;
        Vector3Int gridPos = DigManager.Instance
            .tileController
            .tilemap
            .WorldToCell(worldPos);

        // DungeonTileData holen
        if (!DigManager.Instance.tileMap.TryGetValue(gridPos, out tileData))
        {
           // Debug.LogWarning($"[GoldResourceDisplay] Kein DungeonTileData bei {gridPos}");
            return;
        }

        // TextMesh-Objekt erstellen
        GameObject textObj = new GameObject("GoldResourceText");
        textObj.transform.SetParent(transform);
        textObj.transform.localPosition = new Vector3(0f, verticalOffset, 0f);

        resourceText = textObj.AddComponent<TextMesh>();
        resourceText.alignment = TextAlignment.Center;
        resourceText.anchor = TextAnchor.LowerCenter;
        resourceText.characterSize = 0.1f;
        resourceText.fontSize = 12;
        resourceText.color = Color.white;
        resourceText.text = tileData.Health.ToString();

        // Built-in Font „LegacyRuntime.ttf“ zuweisen
        Font legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        resourceText.font = legacy;
        resourceText.GetComponent<MeshRenderer>().material = legacy.material;
    }

    void Update()
    {
        if (tileData == null || resourceText == null) return;

        // Text aktualisieren
        resourceText.text = tileData.Health.ToString();

        // Immer zur Kamera ausrichten
        if (Camera.main != null)
        {
            Vector3 dir = resourceText.transform.position - Camera.main.transform.position;
            resourceText.transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
