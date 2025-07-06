// TilemapScanner.cs
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapScanner : MonoBehaviour
{
    public Tilemap tilemap;
    public TileBase wallTile;
    public TileBase floorTile;
    public TileBase goldWallTile;
    public TileBase jewelVeinTile;
    public TileBase rockTile;
    public TileBase neutralTile;

    void Start()
    {
        var bounds = tilemap.cellBounds;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                var pos = new Vector3Int(x, y, 0);
                var t = tilemap.GetTile(pos);
                if (t == null) continue;

                TileState state;
                if (t == wallTile) state = TileState.Wall_Intact;
                else if (t == floorTile) state = TileState.Floor_Dug;
                else if (t == goldWallTile) state = TileState.Wall_Gold;
                else if (t == jewelVeinTile) state = TileState.Wall_JewelVein;
                else if (t == rockTile) state = TileState.Wall_Rock;
                else if (t == neutralTile) state = TileState.Floor_Neutral;
                else continue;

                DigManager.Instance.tileMap[pos] = new DungeonTileData(state, pos); // ✅ korrigiert
            }
    }
}
