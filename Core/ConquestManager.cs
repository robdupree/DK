using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using DK.Tasks;

public class ConquestManager : MonoBehaviour
{
    [Tooltip("Sekunden zum Erobern (wird aktuell nicht mehr verwendet)")]
    public float timeToConquer = 1f;

    [Tooltip("Tilemap für Conquest")]
    public Tilemap tilemap;

    [Tooltip("Floor-Tile für eroberte Felder")]
    public TileBase floorTile;

    [Tooltip("TaskType-Asset für Conquer-Tasks")]
    public TaskType conquerTaskType;

    // Tile → reservierender Imp
    private Dictionary<Vector3Int, GameObject> reservations = new();

    private static readonly Vector3Int[] CardinalDirs =
        { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down };

    void Start()
    {
        InvokeRepeating(nameof(ScanForConquerTasks), 0f, 0.5f);
    }

    void ScanForConquerTasks()
    {
        var candidates = DigManager.Instance.tileMap
            .Where(kv => kv.Value.State == TileState.Floor_Neutral)
            .Select(kv => kv.Key)
            .Where(pos => CardinalDirs.Any(d =>
            {
                var adj = pos + d;
                return DigManager.Instance.tileMap.TryGetValue(adj, out var dt)
                       && (dt.State == TileState.Floor_Dug || dt.State == TileState.Floor_Conquered);
            }))
            .ToList();

        foreach (var pos in candidates)
        {
            if (reservations.ContainsKey(pos))
                continue;

            if (TaskManager.Instance.openTasks.Any(t =>
                t.type == conquerTaskType && t.location == new Vector2Int(pos.x, pos.y)))
                continue;

            TaskManager.Instance.openTasks.Add(new Task
            {
                type = conquerTaskType,
                location = new Vector2Int(pos.x, pos.y),
                isAssigned = false,
                isCompleted = false,
                progress = 0f
            });

            Debug.Log($"[ConquestManager] Created Conquer-Task at {pos}");
        }
    }

    public bool TryReserveTile(GameObject imp, out Vector3Int pos)
    {
        pos = default;
        var ownPos = imp.transform.position;
        var free = DigManager.Instance.tileMap
            .Where(kv => kv.Value.State == TileState.Floor_Neutral && !reservations.ContainsKey(kv.Key))
            .Select(kv => kv.Key)
            .Where(p => CardinalDirs.Any(d =>
            {
                var adj = p + d;
                return DigManager.Instance.tileMap.TryGetValue(adj, out var dt)
                       && (dt.State == TileState.Floor_Dug || dt.State == TileState.Floor_Conquered);
            }))
            .OrderBy(p => Vector3.Distance(ownPos, tilemap.GetCellCenterWorld(p)))
            .ToList();

        if (free.Count == 0)
            return false;

        pos = free[0];
        reservations[pos] = imp;
        return true;
    }

    public void FinishReservation(Vector3Int pos)
    {
        reservations.Remove(pos);
    }
}
