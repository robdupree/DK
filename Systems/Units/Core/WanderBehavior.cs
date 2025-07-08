using UnityEngine;
using DK;
using DK.Tasks;

/// <summary>
/// Komponente die das Wanderverhalten von Imps steuert wenn sie keine Tasks haben
/// </summary>
public class WanderBehavior : MonoBehaviour
{
    [Header("Wander Settings")]
    [Tooltip("Maximale Distanz für Wanderungen")]
    public float maxWanderDistance = 10f;

    [Tooltip("Minimale Distanz für Wanderungen")]
    public float minWanderDistance = 3f;

    [Tooltip("Wahrscheinlichkeit pro Sekunde dass der Imp zu wandern beginnt (0-1)")]
    [Range(0f, 1f)]
    public float wanderProbability = 0.1f;

    [Tooltip("Wahrscheinlichkeit pro Sekunde dass der Imp aufhört zu wandern (0-1)")]
    [Range(0f, 1f)]
    public float stopWanderProbability = 0.3f;

    [Header("Idle Settings")]
    [Tooltip("Minimale Idle-Zeit in Sekunden")]
    public float minIdleTime = 2f;

    [Tooltip("Maximale Idle-Zeit in Sekunden")]
    public float maxIdleTime = 8f;

    [Tooltip("Wahrscheinlichkeit dass Imp idlet statt wandert (0-1)")]
    [Range(0f, 1f)]
    public float idleProbability = 0.4f;

    [Header("Wander Zones")]
    [Tooltip("Nur in gegrabenen Bereichen wandern")]
    public bool onlyWanderInDugAreas = true;

    [Tooltip("Auch in eroberten Bereichen wandern")]
    public bool includeConqueredAreas = true;

    [Header("Debug")]
    public bool showDebugGizmos = false;
    public bool enableDetailedLogging = false;

    private UnitAI unitAI;
    private Vector3 homePosition;
    private bool isWandering = false;
    private float lastBehaviorChange = 0f;
    private Vector3? currentWanderTarget = null;

    // Für smooth behavior changes
    private const float BEHAVIOR_CHECK_INTERVAL = 1f;

    void Awake()
    {
        unitAI = GetComponent<UnitAI>();
        if (unitAI == null)
        {
            Debug.LogError($"[WanderBehavior] {name} benötigt UnitAI Komponente!");
            enabled = false;
            return;
        }

        // Heimatposition als Spawn-Position festlegen
        homePosition = transform.position;
        lastBehaviorChange = Time.time;
    }

    void Update()
    {
        // Nur aktiv werden wenn Imp verfügbar ist
        if (!unitAI.IsAvailable())
        {
            // Wenn Imp wieder eine Task bekommt, Wanderverhalten zurücksetzen
            if (isWandering)
            {
                StopWandering();
            }
            return;
        }

        // Behavior-Changes nur alle BEHAVIOR_CHECK_INTERVAL Sekunden prüfen
        if (Time.time - lastBehaviorChange < BEHAVIOR_CHECK_INTERVAL)
            return;

        // Entscheide was der Imp als nächstes tun soll
        DecideNextBehavior();
        lastBehaviorChange = Time.time;
    }

    void DecideNextBehavior()
    {
        if (isWandering)
        {
            // Prüfe ob Imp am Wanderziel angekommen ist oder wandern stoppen soll
            if (HasReachedWanderTarget() || ShouldStopWandering())
            {
                StopWandering();

                // Nach dem Wandern oft eine Idle-Pause einlegen
                if (Random.value < 0.7f)
                {
                    StartIdling();
                }
            }
        }
        else
        {
            // Imp idlet gerade - entscheide ob wandern oder weiter idlen
            if (ShouldStartWandering())
            {
                StartWandering();
            }
        }
    }

    bool ShouldStartWandering()
    {
        // Basis-Wahrscheinlichkeit für Wandern
        float baseChance = wanderProbability * BEHAVIOR_CHECK_INTERVAL;

        // Modifikationen basierend auf Situation
        float timeSinceLastChange = Time.time - lastBehaviorChange;

        // Nach längerem Idle erhöhte Wanderlust
        if (timeSinceLastChange > 5f)
        {
            baseChance *= 2f;
        }

        // Weniger wandern wenn Imp weit von Heimat entfernt
        float distanceFromHome = Vector3.Distance(transform.position, homePosition);
        if (distanceFromHome > maxWanderDistance * 0.8f)
        {
            baseChance *= 0.3f;
        }

        return Random.value < baseChance;
    }

    bool ShouldStopWandering()
    {
        float stopChance = stopWanderProbability * BEHAVIOR_CHECK_INTERVAL;
        return Random.value < stopChance;
    }

    void StartWandering()
    {
        Vector3? wanderTarget = FindWanderTarget();

        if (wanderTarget.HasValue)
        {
            isWandering = true;
            currentWanderTarget = wanderTarget.Value;

            // Wander-Task mit niedriger Priorität hinzufügen
            var wanderTask = new WanderTask(currentWanderTarget.Value);
            unitAI.EnqueueTask(wanderTask, TaskPriority.Low);

            if (enableDetailedLogging)
            {
                Debug.Log($"[WanderBehavior] {name} beginnt zu wandern nach {currentWanderTarget.Value}");
            }
        }
        else
        {
            // Kein gültiges Wanderziel gefunden - idle stattdessen
            StartIdling();
        }
    }

    void StopWandering()
    {
        isWandering = false;
        currentWanderTarget = null;

        if (enableDetailedLogging)
        {
            Debug.Log($"[WanderBehavior] {name} stoppt das Wandern");
        }
    }

    void StartIdling()
    {
        float idleDuration = Random.Range(minIdleTime, maxIdleTime);
        var idleTask = new IdleTask(idleDuration);
        unitAI.EnqueueTask(idleTask, TaskPriority.Low);

        if (enableDetailedLogging)
        {
            Debug.Log($"[WanderBehavior] {name} idlet für {idleDuration:F1}s");
        }
    }

    bool HasReachedWanderTarget()
    {
        if (!currentWanderTarget.HasValue)
            return true;

        float distanceToTarget = Vector3.Distance(transform.position, currentWanderTarget.Value);
        return distanceToTarget < 1.5f;
    }

    Vector3? FindWanderTarget()
    {
        int maxAttempts = 20;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // Zufällige Richtung und Distanz
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(minWanderDistance, maxWanderDistance);

            Vector3 randomDirection = new Vector3(
                Mathf.Cos(angle) * distance,
                0f,
                Mathf.Sin(angle) * distance
            );

            Vector3 potentialTarget = homePosition + randomDirection;

            // Prüfe ob Position auf NavMesh erreichbar ist
            if (UnityEngine.AI.NavMesh.SamplePosition(potentialTarget, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            {
                // Prüfe ob Position in erlaubtem Bereich liegt
                if (IsValidWanderLocation(hit.position))
                {
                    return hit.position;
                }
            }
        }

        // Fallback: Bleib in der Nähe der aktuellen Position
        Vector3 nearbyTarget = transform.position + Random.insideUnitSphere * 2f;
        nearbyTarget.y = transform.position.y;

        if (UnityEngine.AI.NavMesh.SamplePosition(nearbyTarget, out UnityEngine.AI.NavMeshHit nearHit, 3f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return nearHit.position;
        }

        return null;
    }

    bool IsValidWanderLocation(Vector3 worldPosition)
    {
        if (!onlyWanderInDugAreas)
            return true;

        // Konvertiere zu Tile-Position
        Vector3Int tilePos = Vector3Int.FloorToInt(worldPosition);

        if (DigManager.Instance?.tileMap?.TryGetValue(tilePos, out var tileData) == true)
        {
            return tileData.State == TileState.Floor_Dug ||
                   tileData.State == TileState.Room_Treasury ||
                   tileData.State == TileState.Room_Lair ||
                   tileData.State == TileState.Room_Training ||
                   (includeConqueredAreas && tileData.State == TileState.Floor_Conquered);
        }

        // Wenn keine Tile-Daten vorhanden, erlaube Wandern (z.B. in Starträumen)
        return true;
    }

    // Public Methods für externe Kontrolle
    public void SetHomePosition(Vector3 newHome)
    {
        homePosition = newHome;
    }

    public void ForceStopWandering()
    {
        if (isWandering)
        {
            StopWandering();
            unitAI.ClearTasks(); // Entferne alle Low-Priority Tasks
        }
    }

    public bool IsCurrentlyWandering => isWandering;

    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos)
            return;

        // Zeichne Heimatposition
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(homePosition, 0.5f);

        // Zeichne Wander-Radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(homePosition, maxWanderDistance);

        // Zeichne aktuelles Wanderziel
        if (currentWanderTarget.HasValue)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(currentWanderTarget.Value, 0.3f);
            Gizmos.DrawLine(transform.position, currentWanderTarget.Value);
        }
    }
}