using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using DK.Tasks;
using DK;

public class DigManager : MonoBehaviour
{
    public static DigManager Instance;

    [Tooltip("Drag & Drop TileSelectionManager hier")]
    public TileSelectionManager selectionManager;

    [Tooltip("TileController für das Ersetzen von Walls mit Floors")]
    public TileController tileController;

    [Tooltip("TaskType-Asset für Dig-Tasks")]
    public TaskType digTaskType;

    [Tooltip("Tile für neutralen Boden nach dem Graben")]
    public TileBase neutralFloorTile;

    // Grid-Position → Tile-Daten
    public Dictionary<Vector3Int, DungeonTileData> tileMap = new();

    void Awake()
    {
        Instance = this;
        if (selectionManager == null)
            selectionManager = FindFirstObjectByType<TileSelectionManager>();
        if (tileController == null)
            tileController = FindFirstObjectByType<TileController>();
    }

    /// <summary>
    /// Klick-Handling: markiert oder demarkiert ein Tile zum Graben
    /// </summary>
    public void ToggleMarking(Vector3Int pos)
    {
        if (!tileMap.TryGetValue(pos, out var tile))
            return;

        if (tile.State == TileState.Wall_Intact ||
            tile.State == TileState.Wall_Gold ||
            tile.State == TileState.Wall_JewelVein)
        {
            if (TryMarkTile(pos))
                selectionManager?.ToggleTileSelection(pos);
        }
        else if (tile.State == TileState.Wall_Marked || tile.State == TileState.Wall_BeingDug)
        {
            if (TryUnmarkTile(pos))
                selectionManager?.ToggleTileSelection(pos);
        }
    }

    public bool IsTileMarked(Vector3Int pos)
        => tileMap.TryGetValue(pos, out var tile) && tile.State == TileState.Wall_Marked;

    public bool TryMarkTile(Vector3Int pos)
    {
        if (!tileMap.TryGetValue(pos, out var tile))
            return false;

        if (tile.State != TileState.Wall_Intact &&
            tile.State != TileState.Wall_Gold &&
            tile.State != TileState.Wall_JewelVein)
            return false;

        tile.OriginalState = tile.State;
        DebugTileMonitor.LogStateChange(pos, tile.State, TileState.Wall_Marked, "TryMarkTile");
        tile.State = TileState.Wall_Marked;

        if (HasAdjacentFloor(pos))
            AddDigTask(pos);

        return true;
    }

    public bool TryUnmarkTile(Vector3Int pos)
    {
        if (!tileMap.TryGetValue(pos, out var tile))
            return false;
        if (tile.State != TileState.Wall_Marked && tile.State != TileState.Wall_BeingDug)
            return false;

        TileState newState = tile.OriginalState switch
        {
            TileState.Wall_Gold => TileState.Wall_Gold,
            TileState.Wall_JewelVein => TileState.Wall_JewelVein,
            _ => TileState.Wall_Intact
        };
        DebugTileMonitor.LogStateChange(pos, tile.State, newState, "TryUnmarkTile");
        tile.State = newState;

        TaskManager.Instance.openTasks.RemoveAll(
            t => !t.isAssigned && t.type == digTaskType && t.location == new Vector2Int(pos.x, pos.y));

        return true;
    }

    public void CompleteDig(Vector3Int pos)
    {
        if (!tileMap.TryGetValue(pos, out var tile))
            return;

        // WICHTIG: Sofort alle Tasks für dieses Tile als completed markieren
        CleanupTasksForPosition(pos);

        var workersToNotify = new List<GameObject>(tile.AssignedWorkers);
        foreach (var worker in workersToNotify)
        {
            if (worker != null)
            {
                var unitAI = worker.GetComponent<UnitAI>();
                if (unitAI != null)
                {
                    Debug.Log($"Benachrichtige {worker.name} dass Tile {pos} zerstört wurde");
                }
            }
        }

        // NEUER ZERSTÖRUNGSEFFEKT VOR der Tile-Änderung abspielen
        if (tileController != null)
        {
            // Verwende die neue Methode mit Tile-Daten für kontextspezifische Effekte
            tileController.PlayDestructionEffectForTileData(pos, tile);
        }

        // Tile-State ändern
        tile.State = TileState.Floor_Neutral;

        // Tile visuell ersetzen (NACH dem Effekt für besseres Timing)
        if (neutralFloorTile != null)
            tileController.tilemap.SetTile(pos, neutralFloorTile);
        else
            tileController.ReplaceWallWithFloor(pos);

        // Cleanup
        tile.ReleaseAllSlots();
        tile.AssignedWorkers.Clear();

        selectionManager?.RemoveHighlightAt(pos);
        FindFirstObjectByType<DigReactionSystem>()?.OnTileDug(pos);

        // Warte einen Frame bevor Adjacent-Tasks erstellt werden
        StartCoroutine(DelayedAdjacentTaskCreation(pos));
        StartCoroutine(DelayedTaskAssignment(0.5f));
    }

    private System.Collections.IEnumerator DelayedTaskAssignment(float delay)
    {
        yield return new WaitForSeconds(delay);

        TaskAssigner taskAssigner = FindObjectOfType<TaskAssigner>();
        if (taskAssigner != null)
        {
            taskAssigner.TriggerImmediateAssignment();
        }
    }

    [ContextMenu("Test Destruction Effect at (0,0,0)")]
    public void TestDestructionEffectAtOrigin()
    {
        Vector3Int testPos = Vector3Int.zero;
        if (tileMap.TryGetValue(testPos, out var tile))
        {
            tileController?.PlayDestructionEffectForTileData(testPos, tile);
        }
        else
        {
            // Erstelle Test-Tile-Daten
            var testTileData = new DungeonTileData(TileState.Wall_Intact, testPos);
            tileController?.PlayDestructionEffectForTileData(testPos, testTileData);
        }
    }

    // NEUE METHODE: Bereinige Tasks für spezifische Position
    void CleanupTasksForPosition(Vector3Int pos)
    {
        var tasksToComplete = TaskManager.Instance.openTasks
            .Where(t => t.type == digTaskType &&
                       t.location == new Vector2Int(pos.x, pos.y))
            .ToList();

        foreach (var task in tasksToComplete)
        {
            task.isCompleted = true;
            Debug.Log($"[DigManager] Task für Position {pos} als completed markiert");
        }

        // Entferne completed Tasks aus der Liste
        TaskManager.Instance.openTasks.RemoveAll(t => t.isCompleted);
    }

    // NEUE METHODE: Verzögerte Adjacent-Task-Erstellung
    System.Collections.IEnumerator DelayedAdjacentTaskCreation(Vector3Int pos)
    {
        yield return new WaitForEndOfFrame();

        TryCreateAdjacentTasks(pos);

        TaskAssigner taskAssigner = FindObjectOfType<TaskAssigner>();
        if (taskAssigner != null)
        {
            taskAssigner.TriggerImmediateAssignment();
        }
    }

    private System.Collections.IEnumerator DelayedTaskCheck(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Prüfe ob es noch markierte Tiles ohne Tasks gibt
        foreach (var kvp in tileMap)
        {
            var pos = kvp.Key;
            var data = kvp.Value;

            if (data.State == TileState.Wall_Marked)
            {
                var existingTasks = TaskManager.Instance.openTasks
                    .Where(t => t.type == digTaskType &&
                               t.location == new Vector2Int(pos.x, pos.y) &&
                               !t.isCompleted)
                    .Count();

                if (existingTasks == 0 && HasAdjacentFloor(pos))
                {
                    Debug.LogWarning($"[DigManager] REPARATUR: Erstelle fehlende Tasks für vergessenes Tile {pos}");
                    AddDigTask(pos);

                    // Triggere TaskAssigner
                    TaskAssigner taskAssigner = FindObjectOfType<TaskAssigner>();
                    if (taskAssigner != null)
                    {
                        taskAssigner.TriggerImmediateAssignment();
                    }
                }
            }
        }
    }

    private System.Collections.IEnumerator DelayedTaskAssignment(TaskAssigner taskAssigner)
    {
        yield return new WaitForEndOfFrame(); // 1 Frame warten
        taskAssigner.TriggerImmediateAssignment();
    }

    public void MarkTileAsBeingDug(Vector3Int pos)
    {
        // intentionally left blank – handled by DigTask now
    }

    // In DigManager.cs die TryCreateAdjacentTasks Methode erweitern:
    void TryCreateAdjacentTasks(Vector3Int pos)
    {
        var dirs = new[] { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down };
        foreach (var d in dirs)
        {
            var n = pos + d;
            if (tileMap.TryGetValue(n, out var data) && data.State == TileState.Wall_Marked)
            {
                if (HasAdjacentFloor(n) &&
                    !TaskManager.Instance.openTasks.Any(x => !x.isCompleted && x.type == digTaskType && x.location == new Vector2Int(n.x, n.y)))
                {
                    Debug.Log($"[DigManager] Erstelle Adjacent-Task für neu erreichbares Tile {n}");
                    AddDigTask(n);

                    // NEUE ZEILE: Sofortige Task-Zuweisung für verfügbare Imps
                    TaskAssigner taskAssigner = FindObjectOfType<TaskAssigner>();
                    if (taskAssigner != null)
                    {
                        taskAssigner.TriggerImmediateAssignment();
                    }
                }
            }
        }
    }

    bool HasAdjacentFloor(Vector3Int pos)
    {
        var dirs = new[] { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down };
        foreach (var d in dirs)
        {
            if (tileMap.TryGetValue(pos + d, out var nd) &&
                (nd.State == TileState.Floor_Dug || nd.State == TileState.Floor_Neutral || nd.State == TileState.Floor_Conquered))
                return true;
        }
        return false;
    }

    // nur die Methode AddDigTask wurde angepasst
    // Verbesserte AddDigTask Methode für DigManager.cs

    /// <summary>
    /// Erstellt Tasks basierend auf verfügbaren Slots
    /// </summary>
    void AddDigTask(Vector3Int pos)
    {
        if (!tileMap.TryGetValue(pos, out var tile))
            return;

        var availableDirections = tile.GetAvailableDirections();
        if (availableDirections.Count == 0)
        {
            Debug.LogWarning($"[DigManager] Keine verfügbaren Richtungen für Tile {pos}");
            return;
        }

        // Berechne Anzahl verfügbarer Slots
        var totalSlots = availableDirections.Count * 3; // 3 Slots pro Richtung

        // Prüfe wie viele Tasks bereits existieren
        var existingTasks = TaskManager.Instance.openTasks
            .Where(t => t.type == digTaskType &&
                        t.location == new Vector2Int(pos.x, pos.y) &&
                        !t.isCompleted)
            .Count();

        var tasksNeeded = totalSlots - existingTasks;

        if (tasksNeeded <= 0)
        {
            Debug.Log($"[DigManager] Tile {pos} hat bereits genug Tasks ({existingTasks}/{totalSlots})");
            return;
        }

        // Erstelle benötigte Tasks
        for (int i = 0; i < tasksNeeded; i++)
        {
            TaskManager.Instance.openTasks.Add(new Task
            {
                type = digTaskType,
                location = new Vector2Int(pos.x, pos.y),
                isAssigned = false,
                isCompleted = false,
                progress = 0f
            });
        }

        Debug.Log($"[DigManager] {tasksNeeded} neue Dig-Tasks erstellt für Tile {pos} " +
                  $"(Total: {existingTasks + tasksNeeded}/{totalSlots} Tasks)");
    }

    // In DigManager.cs hinzufügen für Debugging:
    [ContextMenu("Debug Marked Tiles")]
    public void DebugMarkedTiles()
    {
        int markedTiles = 0;
        int tasksForMarkedTiles = 0;

        foreach (var kvp in tileMap)
        {
            var pos = kvp.Key;
            var data = kvp.Value;

            if (data.State == TileState.Wall_Marked || data.State == TileState.Wall_BeingDug)
            {
                markedTiles++;

                var tasksForThisTile = TaskManager.Instance.openTasks
                    .Where(t => t.type == digTaskType &&
                               t.location == new Vector2Int(pos.x, pos.y) &&
                               !t.isCompleted)
                    .Count();

                tasksForMarkedTiles += tasksForThisTile;

                if (tasksForThisTile == 0)
                {
                    Debug.LogWarning($"PROBLEM: Markiertes Tile {pos} hat keine Tasks! State: {data.State}");

                    // Versuche Task zu erstellen
                    if (HasAdjacentFloor(pos))
                    {
                        Debug.Log($"Erstelle fehlende Tasks für Tile {pos}");
                        AddDigTask(pos);
                    }
                    else
                    {
                        Debug.LogWarning($"Tile {pos} hat keinen angrenzenden Floor!");
                    }
                }
            }
        }

        Debug.Log($"[DigManager] Markierte Tiles: {markedTiles}, Tasks dafür: {tasksForMarkedTiles}");
    }

}
