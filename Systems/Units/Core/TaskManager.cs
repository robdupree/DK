// Assets/Scripts02/Systems/Units/Core/TaskManager.cs
using System.Collections.Generic;
using System.Linq;
using DK;
using UnityEngine;

/// <summary>
/// Zentraler Manager für alle Tasks.
/// </summary>
public class TaskManager : MonoBehaviour
{
    public static TaskManager Instance { get; private set; }

    public List<Task> openTasks = new List<Task>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public Task AssignBestTask(UnitAI unit)
    {
        if (unit == null)
            return null;

        Task best = null;
        float bestScore = float.MinValue;

        foreach (var t in openTasks.Where(t => !t.isAssigned && t.type != null))
        {
            float score = ComputeScore(unit, t);
            if (score > bestScore)
            {
                bestScore = score;
                best = t;
            }
        }

        if (best != null)
            best.isAssigned = true;

        return best;
    }

    private float ComputeScore(UnitAI unit, Task t)
    {
        if (unit == null || t == null || t.type == null || unit.skills == null || unit.tileController == null)
            return float.MinValue;

        float skillValue = t.type.taskName switch
        {
            "Dig" => unit.skills.digging,
            "Conquer" => unit.skills.conquering,
            "Fight" => unit.skills.fighting,
            _ => 1f
        };

        float difficulty = t.type.baseDifficulty;
        float priority = t.type.basePriority;

        Vector3 worldCenter;
        try
        {
            worldCenter = unit.tileController.tilemap.GetCellCenterWorld(
                new Vector3Int(t.location.x, t.location.y, 0));
        }
        catch
        {
            return float.MinValue;
        }

        float dist = Vector3.Distance(unit.transform.position, worldCenter);
        float distPenalty = dist * 0.1f;

        return (skillValue / Mathf.Max(0.0001f, difficulty)) * priority - distPenalty;
    }

    public void CompleteTask(Task t)
    {
        if (t == null) return;
        t.isCompleted = true;
        openTasks.Remove(t);
    }
}
