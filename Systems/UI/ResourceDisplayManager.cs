using System.Collections;
using System.Linq;
using UnityEngine;

public class ResourceDisplayManager : MonoBehaviour
{
    [Tooltip("Prefab mit Collider + GoldResourceDisplay")]
    public GameObject goldTileDisplayPrefab;

    void Start()
    {
        // Sehr kurz warten, damit TilemapScanner & DigManager vollständig gefüllt sind
        StartCoroutine(InitializeDisplaysNextFrame());
    }

    IEnumerator InitializeDisplaysNextFrame()
    {
        yield return null;        // eine frame warten
        yield return null;        // ggf. noch einen

        // Alle DungeonTileData holen, deren OriginalState Gold ist
        var goldTiles = DigManager.Instance.tileMap
            .Where(kv => kv.Value.OriginalState == TileState.Wall_Gold)
            .Select(kv => kv.Value)
            .ToList();

        foreach (var tileData in goldTiles)
        {
            Vector3 worldCenter = DigManager.Instance
                .tileController
                .tilemap
                .CellToWorld(tileData.GridPosition)
                + new Vector3(0.5f, 0f, 0.5f);

            var disp = Instantiate(
                goldTileDisplayPrefab,
                worldCenter,
                Quaternion.identity,
                transform
            );
            disp.name = $"GoldDisplay_{tileData.GridPosition.x}_{tileData.GridPosition.y}";
        }
    }
}
