using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DK; // für UnitAI

public class DungeonTileData
{
    public TileState State;
    public TileState OriginalState;
    public int Health = 3;
    public bool IsInfinite = false;

    public Vector3Int Position { get; set; }
    public Vector3Int GridPosition => Position;

    public DungeonTileData(TileState state, Vector3Int gridPos)
    {
        State = state;
        OriginalState = state;
        Position = gridPos;

        foreach (var dir in CardinalDirs)
        {
            for (int i = -1; i <= 1; i++)
                reservedSlots[(dir, i)] = false;
        }
    }

    private Dictionary<(Vector3Int dir, int offsetIndex), bool> reservedSlots = new();
    public HashSet<GameObject> AssignedWorkers = new();

    private static readonly Vector3Int[] CardinalDirs = {
        Vector3Int.right,
        Vector3Int.left,
        Vector3Int.up,
        Vector3Int.down
    };

    /// <summary>
    /// Gibt verfügbare Richtungen sortiert nach Priorität zurück
    /// </summary>
    public List<Vector3Int> GetAvailableDirections()
    {
        var result = new List<Vector3Int>();
        foreach (var dir in CardinalDirs)
        {
            var neighborPos = Position + dir;
            if (DigManager.Instance.tileMap.TryGetValue(neighborPos, out var neighborData))
            {
                if (neighborData.State == TileState.Floor_Dug
                    || neighborData.State == TileState.Floor_Neutral
                    || neighborData.State == TileState.Floor_Conquered)
                {
                    result.Add(dir);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Findet den besten verfügbaren Slot basierend auf Imp-Position
    /// Priorität: 1. Mittiger Slot, 2. Nähester Slot zur Imp-Position
    /// </summary>
    public bool TryReserveBestSlot(Vector3 impPosition, out Vector3Int bestDir, out Vector3 bestWorldPos)
    {
        bestDir = Vector3Int.zero;
        bestWorldPos = Vector3.zero;

        var availableDirections = GetAvailableDirections();
        if (availableDirections.Count == 0)
            return false;

        var tileCenter = DigManager.Instance.tileController.tilemap.GetCellCenterWorld(Position);

        // Sammle alle verfügbaren Slot-Optionen mit Distanz-Info
        var slotOptions = new List<SlotOption>();

        foreach (var dir in availableDirections)
        {
            // Prüfe alle 3 Slots in dieser Richtung (links, mitte, rechts)
            for (int offsetIndex = -1; offsetIndex <= 1; offsetIndex++)
            {
                var key = (dir, offsetIndex);
                if (reservedSlots.TryGetValue(key, out var isTaken) && !isTaken)
                {
                    Vector3 slotWorldPos = CalculateSlotWorldPosition(dir, offsetIndex, tileCenter);
                    float distanceToImp = Vector3.Distance(impPosition, slotWorldPos);
                    bool isCenter = offsetIndex == 0; // Mittiger Slot hat Priorität

                    slotOptions.Add(new SlotOption
                    {
                        Direction = dir,
                        OffsetIndex = offsetIndex,
                        WorldPosition = slotWorldPos,
                        DistanceToImp = distanceToImp,
                        IsCenter = isCenter
                    });
                }
            }
        }

        if (slotOptions.Count == 0)
            return false;

        // Sortierung: Erst nach Center-Priorität, dann nach Distanz
        var bestSlot = slotOptions
            .OrderByDescending(s => s.IsCenter) // Center-Slots zuerst
            .ThenBy(s => s.DistanceToImp)       // Dann nach Distanz
            .First();

        // Besten Slot reservieren
        var reservationKey = (bestSlot.Direction, bestSlot.OffsetIndex);
        reservedSlots[reservationKey] = true;

        bestDir = bestSlot.Direction;
        bestWorldPos = bestSlot.WorldPosition;

        Debug.Log($"Bester Dig-Slot gewählt: Richtung {GetDirectionName(bestDir)}, " +
                  $"Offset {bestSlot.OffsetIndex} (center: {bestSlot.IsCenter}, {bestSlot.DistanceToImp:F2}m), " +
                  $"Position: {bestWorldPos}");

        return true;
    }

    /// <summary>
    /// Legacy-Methode für Rückwärtskompatibilität - verwendet jetzt die optimierte Logik
    /// </summary>
    public bool TryReserveSlotWithPosition(Vector3Int dir, out Vector3 worldPos)
    {
        worldPos = Vector3.zero;

        // Verwende Tile-Center als Standard-Position für Legacy-Aufrufe
        var tileCenter = DigManager.Instance.tileController.tilemap.GetCellCenterWorld(Position);

        for (int offsetIndex = 0; offsetIndex <= 1; offsetIndex++) // Erst mitte (0), dann rechts (1), dann links (-1)
        {
            var actualOffset = offsetIndex == 0 ? 0 : (offsetIndex == 1 ? 1 : -1);
            var key = (dir, actualOffset);

            if (!reservedSlots.TryGetValue(key, out var isTaken) || isTaken)
            {
                if (offsetIndex == 1) actualOffset = -1; // Nach rechts (1) kommt links (-1)
                continue;
            }

            reservedSlots[key] = true;
            worldPos = CalculateSlotWorldPosition(dir, actualOffset, tileCenter);

            Debug.Log($"Legacy Dig-Slot: Richtung {GetDirectionName(dir)}, Offset {actualOffset}, Position: {worldPos}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Berechnet die Welt-Position eines Slots basierend auf Richtung und Offset
    /// </summary>
    private Vector3 CalculateSlotWorldPosition(Vector3Int dir, int offsetIndex, Vector3 tileCenter)
    {
        Vector3 perpOffset = Vector3.zero;
        Vector3 mainOffset = Vector3.zero;

        // KORREKTUR für XZY Cell Swizzle: 
        // Vector3Int.up (0,1,0) wird zu Z-Bewegung in der Welt
        // Vector3Int.down (0,-1,0) wird zu -Z-Bewegung in der Welt

        if (dir == Vector3Int.right) // (1,0,0) -> X+
        {
            mainOffset = Vector3.right * 0.7f;
            perpOffset = Vector3.forward * offsetIndex * 0.3f; // Z für seitlichen Versatz
        }
        else if (dir == Vector3Int.left) // (-1,0,0) -> X-
        {
            mainOffset = Vector3.left * 0.7f;
            perpOffset = Vector3.forward * offsetIndex * 0.3f; // Z für seitlichen Versatz
        }
        else if (dir == Vector3Int.up) // (0,1,0) -> Z+ wegen Cell Swizzle
        {
            mainOffset = Vector3.forward * 0.7f; // Z+ statt Y+
            perpOffset = Vector3.right * offsetIndex * 0.3f; // X für seitlichen Versatz
        }
        else if (dir == Vector3Int.down) // (0,-1,0) -> Z- wegen Cell Swizzle
        {
            mainOffset = Vector3.back * 0.7f; // Z- statt Y-
            perpOffset = Vector3.right * offsetIndex * 0.3f; // X für seitlichen Versatz
        }

        return tileCenter + mainOffset + perpOffset;
    }

    private string GetDirectionName(Vector3Int dir)
    {
        if (dir == Vector3Int.right) return "RECHTS (X+)";
        if (dir == Vector3Int.left) return "LINKS (X-)";
        if (dir == Vector3Int.up) return "OBEN (Y+->Z+ wegen Swizzle)";
        if (dir == Vector3Int.down) return "UNTEN (Y-->Z- wegen Swizzle)";
        return "UNBEKANNT";
    }

    // KORRIGIERTE ReleaseSlot Methode - ersetzt die bestehende
    public void ReleaseSlot(Vector3Int dir)
    {
        // Finde und gib alle Slots in dieser Richtung frei
        var slotsToRelease = reservedSlots.Keys
            .Where(key => key.dir == dir)
            .ToList();

        foreach (var key in slotsToRelease)
        {
            reservedSlots[key] = false;
            Debug.Log($"Slot freigegeben: Richtung {GetDirectionName(dir)}, Offset {key.offsetIndex}");
        }

        // Benachrichtige benachbarte Tiles
        NotifyAdjacentTilesOfSlotRelease();
    }

    // NEUE METHODE: Spezifischer Slot mit Offset
    public void ReleaseSpecificSlot(Vector3Int dir, int offsetIndex)
    {
        var key = (dir, offsetIndex);
        if (reservedSlots.ContainsKey(key))
        {
            reservedSlots[key] = false;
            Debug.Log($"Spezifischer Slot freigegeben: Richtung {GetDirectionName(dir)}, Offset {offsetIndex}");

            // Benachrichtige benachbarte Tiles
            NotifyAdjacentTilesOfSlotRelease();
        }
    }

    // NEUE METHODE: Alle Slots freigeben
    public void ReleaseAllSlots()
    {
        // Alle Slot-Reservierungen freigeben
        foreach (var key in reservedSlots.Keys.ToList())
        {
            reservedSlots[key] = false;
        }

        // Worker-Liste leeren
        AssignedWorkers.Clear();

        Debug.Log($"Alle Slots für Tile {Position} wurden freigegeben");

        // Benachrichtige benachbarte Tiles über Slot-Freigabe
        NotifyAdjacentTilesOfSlotRelease();
    }

    // NEUE METHODE: Benachrichtige benachbarte Tiles
    private void NotifyAdjacentTilesOfSlotRelease()
    {
        var directions = new Vector3Int[] {
            Vector3Int.right, Vector3Int.left,
            Vector3Int.up, Vector3Int.down
        };

        foreach (var dir in directions)
        {
            Vector3Int neighborPos = Position + dir;
            if (DigManager.Instance.tileMap.TryGetValue(neighborPos, out var neighborData))
            {
                neighborData.RefreshSlotAvailability();
            }
        }
    }

    // NEUE METHODE: Aktualisiere Slot-Verfügbarkeit
    public void RefreshSlotAvailability()
    {
        // Sammle alle Slots die freigegeben werden sollen
        var slotsToRelease = new List<(Vector3Int dir, int offsetIndex)>();

        foreach (var kvp in reservedSlots.ToList())
        {
            if (kvp.Value) // Slot ist reserviert
            {
                // Prüfe ob noch ein aktiver Worker existiert
                bool hasActiveWorker = AssignedWorkers.Any(worker =>
                    worker != null &&
                    worker.GetComponent<UnitAI>()?.IsAvailable() == false); // Nicht verfügbar = arbeitet

                if (!hasActiveWorker)
                {
                    Debug.Log($"Gebe verwaisten Slot frei: {kvp.Key}");
                    slotsToRelease.Add(kvp.Key);
                }
            }
        }

        // Gib verwaiste Slots frei
        foreach (var slot in slotsToRelease)
        {
            reservedSlots[slot] = false;
        }
    }

    public void AssignWorker(GameObject unit)
    {
        AssignedWorkers.Add(unit);
    }

    public void UnassignWorker(GameObject unit)
    {
        AssignedWorkers.Remove(unit);
    }

    public bool ReduceHealth()
    {
        if (IsInfinite) return false;
        Health--;
        return Health <= 0;
    }

    /// <summary>
    /// Hilfklasse für Slot-Optionen
    /// </summary>
    private class SlotOption
    {
        public Vector3Int Direction;
        public int OffsetIndex;
        public Vector3 WorldPosition;
        public float DistanceToImp;
        public bool IsCenter;
    }
}