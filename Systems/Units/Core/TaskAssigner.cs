using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DK.Tasks;

namespace DK
{
    public class TaskAssigner : MonoBehaviour
    {
        [Header("Performance Settings")]
        [Tooltip("Maximale Anzahl Task-Zuweisungen pro Frame")]
        public int maxAssignmentsPerFrame = 5; // ERHÖHT für sofortige Reaktion

        [Tooltip("Frames zwischen Task-Assignment-Zyklen")]
        public int framesBetweenAssignments = 1; // SOFORT für minimale Verzögerung

        [Header("Debug Settings")]
        public bool enableDetailedLogging = false;
        public bool showPerformanceGUI = false;

        // Thread-Safe Task-Tracking
        private static Dictionary<Vector2Int, TileTaskInfo> tileTaskTracking = new Dictionary<Vector2Int, TileTaskInfo>();

        // Performance-Tracking
        private int frameCounter = 0;
        private Queue<UnitAI> impProcessingQueue = new Queue<UnitAI>();
        private bool isProcessingQueue = false;

        // Cache für bessere Performance
        private List<UnitAI> cachedAvailableImps = new List<UnitAI>();
        private float lastImpCacheUpdate = 0f;
        private const float IMP_CACHE_INTERVAL = 0.5f;

        // Statistics
        private int totalAssignmentsThisSecond = 0;
        private float lastStatsReset = 0f;

        // Anti-Spam für Logs
        private Dictionary<string, float> lastLogTime = new Dictionary<string, float>();
        private const float LOG_COOLDOWN = 5f;

        void Update()
        {
            frameCounter++;

            if (frameCounter % framesBetweenAssignments != 0)
                return;

            if (frameCounter % 300 == 0)
            {
                CleanupCompletedTasks();
                CleanupStaleReservations();
                ResetStatistics();
            }

            if (Time.time - lastImpCacheUpdate > IMP_CACHE_INTERVAL)
            {
                UpdateAvailableImpsCache();
            }

            if (!isProcessingQueue && cachedAvailableImps.Count > 0)
            {
                BuildPrioritizedImpQueue();
            }

            ProcessImpQueue();
        }

        void UpdateAvailableImpsCache()
        {
            cachedAvailableImps.Clear();

            var allImps = Object.FindObjectsByType<UnitAI>(FindObjectsSortMode.None);
            foreach (var imp in allImps)
            {
                // WICHTIGE ÄNDERUNG: Nur wirklich verfügbare Imps ohne aktuelle Tasks
                if (imp != null && imp.IsAvailable() && !IsImpCurrentlyWorking(imp))
                {
                    cachedAvailableImps.Add(imp);
                }
            }

            lastImpCacheUpdate = Time.time;
        }

        /// <summary>
        /// NEUE METHODE: Prüft ob ein Imp gerade an einem Task arbeitet
        /// </summary>
        bool IsImpCurrentlyWorking(UnitAI imp)
        {
            // EINFACHE PRÜFUNG: Verwende nur IsAvailable() - das ist sicherer
            // IsAvailable() prüft bereits alle internen Zustände
            if (!imp.IsAvailable())
            {
                return true; // Imp ist beschäftigt
            }

            // ZUSÄTZLICH: Prüfe NavMeshAgent-Status wenn verfügbar
            var navAgent = imp.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navAgent != null && navAgent.hasPath && navAgent.velocity.magnitude > 0.1f)
            {
                return true; // Imp bewegt sich gerade zu einem Ziel
            }

            return false; // Imp ist wirklich verfügbar
        }

        /// <summary>
        /// VERBESSERTE Imp-Priorisierung: Priorisiere Imps nach Nähe zu Tasks
        /// </summary>
        void BuildPrioritizedImpQueue()
        {
            impProcessingQueue.Clear();

            if (cachedAvailableImps.Count == 0)
                return;

            var validImps = cachedAvailableImps
                .Where(imp => imp != null && imp.IsAvailable() && !IsImpCurrentlyWorking(imp))
                .Take(15) // ERWEITERT: 15 statt 10 für bessere Abdeckung
                .ToList();

            if (validImps.Count == 0)
                return;

            // NEUE PRIORISIERUNG: Sortiere Imps nach ihrer Nähe zum nähesten Task
            var prioritizedImps = validImps
                .Select(imp => new ImpTaskDistance(imp))
                .Where(itd => itd.DistanceToNearestTask < float.MaxValue)
                .OrderBy(itd => itd.DistanceToNearestTask) // NÄHESTE IMPS ZUERST
                .ThenBy(itd => itd.Age) // Bei gleicher Distanz: Ältere Imps zuerst
                .Take(maxAssignmentsPerFrame * 3) // Mehr Kandidaten für bessere Auswahl
                .Select(itd => itd.Imp)
                .ToList();

            foreach (var imp in prioritizedImps)
            {
                impProcessingQueue.Enqueue(imp);
            }

            isProcessingQueue = true;

            LogWithCooldown("queue_built", $"[TaskAssigner] {prioritizedImps.Count} Imps eingereiht (näheste zuerst)");
        }

        void ProcessImpQueue()
        {
            int assignmentsThisFrame = 0;

            while (impProcessingQueue.Count > 0 && assignmentsThisFrame < maxAssignmentsPerFrame)
            {
                var ai = impProcessingQueue.Dequeue();

                if (ai == null || !ai.IsAvailable() || IsImpCurrentlyWorking(ai))
                    continue;

                if (TryAssignTaskToImp(ai))
                {
                    assignmentsThisFrame++;
                    totalAssignmentsThisSecond++;
                }
            }

            if (impProcessingQueue.Count == 0)
            {
                isProcessingQueue = false;
            }
        }

        /// <summary>
        /// VERBESSERTE Task-Assignment: Keine Unterbrechungen
        /// </summary>
        bool TryAssignTaskToImp(UnitAI ai)
        {
            // ZUSÄTZLICHE SICHERHEITSPRÜFUNG: Imp wirklich verfügbar?
            if (!ai.IsAvailable() || IsImpCurrentlyWorking(ai))
            {
                Debug.Log($"[TaskAssigner] {ai.name} ist beschäftigt - überspringe Assignment");
                return false;
            }

            var openTasks = TaskManager.Instance.openTasks
                .Where(t => !t.isCompleted && !t.isAssigned)
                .ToList();

            if (!openTasks.Any())
            {
                return false;
            }

            Vector3 impPosition = ai.transform.position;
            Task taskToAssign = null;

            var digType = DigManager.Instance.digTaskType;

            // NEUE LOGIK: Finde den NÄHESTEN verfügbaren Dig-Task für diesen spezifischen Imp
            var availableDigTasks = openTasks
                .Where(t => t.type == digType)
                .Where(t => IsDigTaskValidAndAvailable(t))
                .Select(t => new {
                    Task = t,
                    Distance = Vector3.Distance(impPosition, new Vector3(t.location.x, 0, t.location.y)),
                    Position = new Vector3Int(t.location.x, t.location.y, 0)
                })
                .OrderBy(x => x.Distance) // NÄHESTER TASK ZUERST
                .ToList();

            if (availableDigTasks.Any())
            {
                // ZUSÄTZLICHE PRÜFUNG: Wähle den Task mit den besten Slot-Chancen
                foreach (var taskInfo in availableDigTasks.Take(3)) // Prüfe nur die 3 nähesten
                {
                    if (DigManager.Instance.tileMap.TryGetValue(taskInfo.Position, out var data))
                    {
                        // Teste ob der Imp tatsächlich einen Slot für diesen Task bekommen kann
                        if (data.TryReserveBestSlot(impPosition, out var testDir, out var testWorldPos))
                        {
                            // Slot verfügbar - gib ihn sofort wieder frei für echte Zuweisung
                            data.ReleaseSlot(testDir);
                            taskToAssign = taskInfo.Task;

                            Debug.Log($"[TaskAssigner] ✅ OPTIMALER Task für {ai.name}: {taskInfo.Task.location} " +
                                     $"(Dist: {taskInfo.Distance:F1}m, Slot verfügbar)");
                            break;
                        }
                        else
                        {
                            Debug.Log($"[TaskAssigner] ⚠️ Task {taskInfo.Task.location} nah für {ai.name} " +
                                     $"(Dist: {taskInfo.Distance:F1}m) aber kein Slot verfügbar");
                        }
                    }
                }

                // Fallback: Wenn kein Task mit verfügbaren Slots, nimm den nähesten
                if (taskToAssign == null && availableDigTasks.Any())
                {
                    taskToAssign = availableDigTasks.First().Task;
                    Debug.LogWarning($"[TaskAssigner] Fallback: Nähester Task ohne Slot-Garantie für {ai.name}");
                }
            }
            else
            {
                // Fallback: Andere Tasks
                taskToAssign = openTasks
                    .Where(t => t.type != digType)
                    .Where(t => IsTaskLocationValid(t.location))
                    .OrderBy(t => Vector3.Distance(impPosition, new Vector3(t.location.x, 0, t.location.y)))
                    .FirstOrDefault();
            }

            if (taskToAssign == null)
            {
                if (enableDetailedLogging)
                {
                    Debug.Log($"[TaskAssigner] Kein geeigneter Task für {ai.name} gefunden");
                }
                return false;
            }

            // FINALE SICHERHEITSPRÜFUNG vor Task-Zuweisung
            if (!ai.IsAvailable() || IsImpCurrentlyWorking(ai))
            {
                Debug.LogWarning($"[TaskAssigner] {ai.name} wurde während Assignment beschäftigt - Task-Zuweisung abgebrochen");
                return false;
            }

            // SOFORTIGE Task-Zuweisung
            taskToAssign.isAssigned = true;
            TrackTaskAssignmentAtomic(taskToAssign);

            ITask concrete = CreateConcreteTask(taskToAssign, ai);
            if (concrete != null)
            {
                ai.EnqueueTask(concrete, TaskPriority.Normal);

                if (enableDetailedLogging)
                {
                    var distance = Vector3.Distance(impPosition, new Vector3(taskToAssign.location.x, 0, taskToAssign.location.y));
                    Debug.Log($"[TaskAssigner] ✅ ASSIGNED: {taskToAssign.type.taskName} at {taskToAssign.location} → {ai.name} (Dist: {distance:F1}m)");
                }

                return true;
            }
            else
            {
                taskToAssign.isAssigned = false;
                UntrackTaskAssignment(taskToAssign);
                return false;
            }
        }

        bool IsDigTaskValidAndAvailable(Task task)
        {
            var pos = new Vector3Int(task.location.x, task.location.y, 0);

            // NEUE VALIDIERUNG: Prüfe ob Position überhaupt existiert
            if (!DigManager.Instance.tileMap.TryGetValue(pos, out var data))
            {
                Debug.LogWarning($"[TaskAssigner] Task {task.location} verweist auf nicht-existierende Position");
                task.isCompleted = true; // Markiere als completed
                return false;
            }

            // NEUE VALIDIERUNG: Prüfe State genauer
            if (data.State != TileState.Wall_Marked && data.State != TileState.Wall_BeingDug)
            {
                Debug.LogWarning($"[TaskAssigner] Task {task.location} verweist auf Tile mit falschem State: {data.State}");
                task.isCompleted = true; // Markiere als completed
                return false;
            }

            var freeDirs = data.GetAvailableDirections();
            bool hasAdjacentFloor = freeDirs.Any(d =>
            {
                var nb = pos + d;
                return DigManager.Instance.tileMap.TryGetValue(nb, out var nd)
                       && (nd.State == TileState.Floor_Dug
                           || nd.State == TileState.Floor_Neutral
                           || nd.State == TileState.Floor_Conquered);
            });

            if (!hasAdjacentFloor)
            {
                Debug.LogWarning($"[TaskAssigner] Task {task.location} hat keinen angrenzenden Floor");
                return false;
            }

            // Vereinfachte Slot-Validierung
            bool hasBasicSlotAvailability = HasBasicWorkerSlots(task.location, data);

            return hasBasicSlotAvailability;
        }

        bool HasBasicWorkerSlots(Vector2Int tileLocation, DungeonTileData data)
        {
            var availableDirections = data.GetAvailableDirections().Count;
            var maxWorkers = Mathf.Max(1, availableDirections * 3);
            var activeWorkers = data.AssignedWorkers?.Where(w => w != null).Count() ?? 0;
            var pendingWorkers = GetPendingWorkersForTile(tileLocation);
            var totalWorkers = activeWorkers + pendingWorkers;

            // GROSSZÜGIGERE Grenze: Mehr Spielraum für gleichzeitige Zuweisungen
            return totalWorkers < (maxWorkers - 1);
        }

        bool IsTaskLocationValid(Vector2Int location)
        {
            var pos = new Vector3Int(location.x, location.y, 0);
            return DigManager.Instance.tileMap.ContainsKey(pos);
        }

        void TrackTaskAssignmentAtomic(Task task)
        {
            var location = task.location;
            if (!tileTaskTracking.ContainsKey(location))
            {
                tileTaskTracking[location] = new TileTaskInfo();
            }
            tileTaskTracking[location].PendingTasks++;
            tileTaskTracking[location].LastAssignmentTime = Time.time;
        }

        void UntrackTaskAssignment(Task task)
        {
            var location = task.location;
            if (tileTaskTracking.TryGetValue(location, out var info))
            {
                info.PendingTasks = Mathf.Max(0, info.PendingTasks - 1);
                if (info.PendingTasks == 0)
                {
                    tileTaskTracking.Remove(location);
                }
            }
        }

        int GetPendingWorkersForTile(Vector2Int tileLocation)
        {
            if (tileTaskTracking.TryGetValue(tileLocation, out var info))
            {
                return info.PendingTasks;
            }
            return 0;
        }

        void CleanupCompletedTasks()
        {
            var completedTasks = TaskManager.Instance.openTasks
                .Where(t => t.isCompleted)
                .ToList();

            foreach (var task in completedTasks)
            {
                UntrackTaskAssignment(task);
            }
        }

        void CleanupStaleReservations()
        {
            var staleTiles = new List<Vector2Int>();
            foreach (var kvp in tileTaskTracking.ToList())
            {
                var pos = new Vector3Int(kvp.Key.x, kvp.Key.y, 0);
                if (!DigManager.Instance.tileMap.TryGetValue(pos, out var data) ||
                    (data.State != TileState.Wall_Marked && data.State != TileState.Wall_BeingDug))
                {
                    staleTiles.Add(kvp.Key);
                }
                else if (Time.time - kvp.Value.LastAssignmentTime > 10f)
                {
                    staleTiles.Add(kvp.Key);
                }
            }

            foreach (var tile in staleTiles)
            {
                tileTaskTracking.Remove(tile);
            }
        }

        void ResetStatistics()
        {
            if (Time.time - lastStatsReset > 1f)
            {
                if (enableDetailedLogging && totalAssignmentsThisSecond > 0)
                {
                    LogWithCooldown("stats", $"[TaskAssigner] Assignments: {totalAssignmentsThisSecond}/s");
                }

                totalAssignmentsThisSecond = 0;
                lastStatsReset = Time.time;
            }
        }

        void LogWithCooldown(string key, string message)
        {
            if (!lastLogTime.TryGetValue(key, out var lastTime) || Time.time - lastTime > LOG_COOLDOWN)
            {
                Debug.Log(message);
                lastLogTime[key] = Time.time;
            }
        }

        ITask CreateConcreteTask(Task taskData, UnitAI ai)
        {
            return taskData.type.taskName switch
            {
                "Dig" => new DigTask(taskData, ai.config != null ? ai.config.digDuration : 1f),
                "Conquer" => new ConquerTask(taskData, ai.config != null ? ai.config.conquerDuration : 1f),
                _ => null
            };
        }

        float GetImpAge(UnitAI imp)
        {
            var ageComponent = imp.GetComponent<ImpAgeTracker>();
            if (ageComponent == null)
            {
                ageComponent = imp.gameObject.AddComponent<ImpAgeTracker>();
            }
            return ageComponent.Age;
        }

        void OnGUI()
        {
            if (!showPerformanceGUI) return;

            GUILayout.BeginArea(new Rect(10, 10, 400, 300));

            GUILayout.Label("=== TaskAssigner Monitor (Keine Unterbrechungen) ===", GUI.skin.box);
            GUILayout.Label($"Queue: {impProcessingQueue.Count}");
            GUILayout.Label($"Tracked Tiles: {tileTaskTracking.Count}");
            GUILayout.Label($"Processing: {isProcessingQueue}");
            GUILayout.Label($"Assignments/Sec: {totalAssignmentsThisSecond}");

            var cacheAge = Time.time - lastImpCacheUpdate;
            GUILayout.Label($"Imp Cache: {cachedAvailableImps.Count} (age: {cacheAge:F1}s)");

            var allImps = Object.FindObjectsByType<UnitAI>(FindObjectsSortMode.None);
            var availableImps = cachedAvailableImps.Count;
            var busyImps = allImps.Length - availableImps;

            GUILayout.Label($"Imps: {allImps.Length} total ({availableImps} available, {busyImps} busy)");

            // Zeige Dig-Task-Statistiken
            var allTasks = TaskManager.Instance.openTasks;
            var digTasks = allTasks.Where(t => t.type == DigManager.Instance.digTaskType).Count();
            var assignedTasks = allTasks.Where(t => t.isAssigned && !t.isCompleted).Count();
            var completedTasks = allTasks.Where(t => t.isCompleted).Count();

            GUILayout.Label($"Tasks: {allTasks.Count} total ({digTasks} dig, {assignedTasks} assigned, {completedTasks} completed)");

            // Zeige Working-Status
            var workingImps = allImps.Where(imp => IsImpCurrentlyWorking(imp)).Count();
            GUILayout.Label($"Working Imps: {workingImps} (protected from interruption)");

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Assignments/Frame: {maxAssignmentsPerFrame}");
            if (GUILayout.Button("-") && maxAssignmentsPerFrame > 1) maxAssignmentsPerFrame--;
            if (GUILayout.Button("+") && maxAssignmentsPerFrame < 10) maxAssignmentsPerFrame++;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Frames Between: {framesBetweenAssignments}");
            if (GUILayout.Button("-") && framesBetweenAssignments > 1) framesBetweenAssignments--;
            if (GUILayout.Button("+") && framesBetweenAssignments < 20) framesBetweenAssignments++;
            GUILayout.EndHorizontal();

            enableDetailedLogging = GUILayout.Toggle(enableDetailedLogging, "Detailed Logging");

            GUILayout.EndArea();
        }

        /// <summary>
        /// Triggert sofortige Task-Zuweisung für neu verfügbare Tasks
        /// </summary>
        public void TriggerImmediateAssignment()
        {
            Debug.Log("[TaskAssigner] Sofortige Task-Zuweisung getriggert (Keine Unterbrechungen)");

            UpdateAvailableImpsCache();
            impProcessingQueue.Clear();
            isProcessingQueue = false;

            // AGGRESSIVERE Versuche für bessere Slot-Zuweisung
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (cachedAvailableImps.Count > 0)
                {
                    BuildPrioritizedImpQueue();
                    ProcessImpQueue();
                    UpdateAvailableImpsCache();
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// NEUE HILFKLASSE: Berechnet Imp-zu-Task Distanzen nur für VERFÜGBARE Imps
        /// </summary>
        private class ImpTaskDistance
        {
            public UnitAI Imp;
            public float DistanceToNearestTask;
            public float Age;

            private static List<Task> cachedDigTasks = new List<Task>();
            private static float lastDigTaskCacheUpdate = 0f;
            private const float DIG_TASK_CACHE_INTERVAL = 0.5f;

            public ImpTaskDistance(UnitAI imp)
            {
                Imp = imp;

                var ageTracker = imp.GetComponent<ImpAgeTracker>();
                if (ageTracker == null)
                {
                    ageTracker = imp.gameObject.AddComponent<ImpAgeTracker>();
                }
                Age = ageTracker.Age;

                // NUR berechnen wenn Imp wirklich verfügbar ist
                if (imp.IsAvailable())
                {
                    DistanceToNearestTask = CalculateDistanceToNearestDigTask(imp);
                }
                else
                {
                    DistanceToNearestTask = float.MaxValue; // Beschäftigte Imps haben "unendliche" Distanz
                }
            }

            float CalculateDistanceToNearestDigTask(UnitAI imp)
            {
                // Cache nur Dig-Tasks für bessere Performance
                if (Time.time - lastDigTaskCacheUpdate > DIG_TASK_CACHE_INTERVAL)
                {
                    cachedDigTasks.Clear();
                    cachedDigTasks.AddRange(TaskManager.Instance.openTasks
                        .Where(t => !t.isCompleted && !t.isAssigned && t.type == DigManager.Instance.digTaskType));
                    lastDigTaskCacheUpdate = Time.time;
                }

                if (!cachedDigTasks.Any())
                    return float.MaxValue;

                var impPos = imp.transform.position;
                float nearestDistance = float.MaxValue;

                // Finde die 2 nähesten Dig-Tasks
                var nearbyTasks = cachedDigTasks
                    .OrderBy(t => Vector3.Distance(impPos, new Vector3(t.location.x, 0, t.location.y)))
                    .Take(2)
                    .ToList();

                foreach (var task in nearbyTasks)
                {
                    var taskWorldPos = new Vector3(task.location.x, 0, task.location.y);
                    float distance = Vector3.Distance(impPos, taskWorldPos);

                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                    }
                }

                return nearestDistance;
            }
        }

        private class TileTaskInfo
        {
            public int PendingTasks = 0;
            public float LastAssignmentTime = 0f;
        }
    }

    public class ImpAgeTracker : MonoBehaviour
    {
        public float SpawnTime { get; private set; }
        public float Age => Time.time - SpawnTime;

        void Awake()
        {
            SpawnTime = Time.time;
        }
    }
}