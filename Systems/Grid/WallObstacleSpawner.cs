using UnityEngine;
using UnityEngine.Tilemaps;

public class WallObstacleSpawner : MonoBehaviour
{
    public Tilemap tilemap;
    public TileBase wallTile;
    public GameObject obstaclePrefab;

    void Start()
    {
        foreach (var pos in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.GetTile(pos) == wallTile)
            {
                Vector3 world = tilemap.CellToWorld(pos) + new Vector3(0.5f, 0, 0.5f);
                Instantiate(obstaclePrefab, world, Quaternion.identity);
            }
        }
    }
}
