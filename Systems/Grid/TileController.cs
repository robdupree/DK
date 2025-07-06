using UnityEngine;
using UnityEngine.Tilemaps;

public class TileController : MonoBehaviour
{
    public Tilemap tilemap;
    public TileBase floorTile;
    public ParticleSystem digEffectPrefab;

    public void ReplaceWallWithFloor(Vector3Int pos)
    {
        tilemap.SetTile(pos, floorTile);
        PlayDigEffect(pos);
    }

    public void PlayDigEffect(Vector3Int pos)
    {
        if (digEffectPrefab == null) return;
        Vector3 worldPos = tilemap.CellToWorld(pos) + Vector3.up * 0.5f;
        Instantiate(digEffectPrefab, worldPos, Quaternion.identity);
    }
}
