using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DK.Tasks;

namespace DK
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class UnitAI : MonoBehaviour
    {
        [Header("Stats & Skills")]
        public StatsProfile stats;
        public SkillSet skills;
        public BalancingConfig config;

        [Header("Worker Prefabs")]
        public GameObject groundGoldPrefab;
        public GameObject goldPilePrefab;

        [Header("Tilemap Reference")]
        public TileController tileController;

        [Header("Navigation")]
        [Tooltip("Maximaler Abstand zum Ziel, um als ›angekommen‹ zu gelten")]
        public float stoppingThreshold = 0.2f;

        [Header("Rotation Settings")]
        [Tooltip("Rotationsgeschwindigkeit des Imps")]
        public float rotationSpeed = 360f;
        [Tooltip("Das Child-GameObject mit dem Mesh/Model")]
        public Transform meshTransform;
        [Tooltip("Rotationsgeschwindigkeit für Task-spezifische Ausrichtung")]
        public float taskRotationSpeed = 720f;

        [Header("Collision Avoidance")]
        [Tooltip("Radius für Kollisionserkennung mit anderen Imps")]
        public float avoidanceRadius = 1.0f;
        [Tooltip("Kraft zum Wegschubsen bei Kollisionen")]
        public float pushForce = 2.0f;
        [Tooltip("LayerMask für andere Imps")]
        public LayerMask impLayerMask = -1;
        [Tooltip("Zeit zwischen Ausweichmanövern")]
        public float avoidanceCooldown = 0.5f;
        [Tooltip("Zeit nach der ein Imp als 'stuck' gilt")]
        public float stuckTimeThreshold = 5.0f;
        [Tooltip("Distanz die als 'bewegungslos' gilt")]
        public float stuckDistanceThreshold = 0.05f;
        [Tooltip("Wie weich/träge die Bewegung ist (höher = weicher)")]
        public float movementSmoothing = 0.3f;

        [Header("Animation Settings")]
        [Tooltip("Name der Dig-Animation im Animator")]
        public string digAnimationName = "dig";
        [Tooltip("Basis-Länge der Dig-Animation in Sekunden (wird durch Animator Speed geteilt)")]
        public float digAnimationBaseLength = 1.0f;

        [Header("Wander Behavior")]
        [Tooltip("Automatisches Wanderverhalten wenn idle")]
        public bool enableWanderBehavior = true;

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
        [Tooltip("Zeigt Wanderradius und Ziele an")]
        public bool showDebugGizmos = false;
        [Tooltip("Detaillierte Logs für Wanderverhalten")]
        public bool enableDetailedWanderLogging = false;


        [Header("Idle Animation Settings")]
        [Tooltip("Liste der verfügbaren Idle-Animationen (Trigger oder Bool Parameter)")]
        public string[] availableIdleAnimations = {
    "Idle1",
    "Idle2",
    "Idle3",
    "IdleLookAround",
    "IdleStretch",
    "IdleBored"
};

        [Tooltip("Minimale Zeit zwischen Animationswechseln")]
        public float minAnimationSwitchTime = 1.5f;

        [Tooltip("Maximale Zeit zwischen Animationswechseln")]
        public float maxAnimationSwitchTime = 5f;

        [Tooltip("Verhindert die gleiche Animation zweimal hintereinander")]
        public bool preventSameAnimationTwice = true;

        [Tooltip("Bevorzuge bestimmte Animationen (höhere Werte = häufiger)")]
        public float[] animationWeights = { 1f, 1f, 1f, 1f, 1f, 1f };

        [Tooltip("Debug-Logs für Idle-Animationen")]
        public bool debugIdleAnimations = false;

        private NavMeshAgent agent;
        private ITask currentTask;
        [HideInInspector] public readonly Queue<ITask> highPriority = new();
        [HideInInspector] public readonly Queue<ITask> normalPriority = new();
        private readonly Queue<ITask> lowPriority = new();

        [HideInInspector] public Animator animator;
        private Quaternion originalMeshRotation;
        private Vector3? targetLookDirection;
        [HideInInspector] public bool isTaskRotating = false;
        private float lastAvoidanceTime = 0f;

        // Stuck-Detection Variablen
        private Vector3 lastPosition;
        private float stuckTimer = 0f;
        private bool isStuck = false;
        private float lastUnstuckTime = 0f;

        // Bewegungs-Smoothing
        private Vector3 smoothedVelocity;
        private Vector3 targetPosition;
        private bool hasValidTarget = false;

        // Wander Behavior - Interne Variablen
        private Vector3 homePosition;
        private bool isWandering = false;
        private float lastBehaviorChange = 0f;
        private Vector3? currentWanderTarget = null;
        private const float BEHAVIOR_CHECK_INTERVAL = 1f;


        [Header("Root Motion & Animation")]
        [Tooltip("Aktiviert Root Motion für Idle-Animationen")]
        public bool useRootMotionForIdle = true;

        [Tooltip("Position-Lock während Idle-Animationen")]
        public bool lockPositionDuringIdle = true;

        [Tooltip("Maximale erlaubte Bewegung während Idle (in Metern)")]
        public float maxIdleMovement = 0.1f;

        // Root Motion Kontrolle
        private Vector3 idleStartPosition;
        private bool isInIdleState = false;
        private bool wasApplyingRootMotion = false;

        public NavMeshAgent Agent => agent;


        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.stoppingDistance = stoppingThreshold;

            // Für manuelle Rotation: NavMeshAgent Rotation deaktivieren
            agent.updateRotation = false;
            agent.angularSpeed = rotationSpeed;

            if (tileController == null && DigManager.Instance != null)
                tileController = DigManager.Instance.tileController;

            animator = GetComponentInChildren<Animator>();

            // Mesh-Transform automatisch finden, falls nicht zugewiesen
            if (meshTransform == null)
                meshTransform = transform.GetChild(0);

            // Ursprüngliche Mesh-Rotation speichern
            if (meshTransform != null)
                originalMeshRotation = meshTransform.localRotation;

            // Stuck-Detection initialisieren
            lastPosition = transform.position;

            // NavMeshAgent für weichere Bewegung konfigurieren
            agent.acceleration = 8f;
            agent.angularSpeed = 120f;

            // Wander Behavior initialisieren
            homePosition = transform.position;
            lastBehaviorChange = Time.time;
        }

        void Update()
        {
            float agentSpeed = agent.velocity.magnitude;
            animator.SetFloat("Speed", agentSpeed);

            HandleIdlePositionLock();
            HandleStuckDetection();
            HandleCollisionAvoidance();

            if (isTaskRotating)
            {
                agent.updateRotation = false;
            }
            else
            {
                agent.updateRotation = false;
            }

            HandleRotation();

            // ERWEITERTE Task-Unterbrechungslogik
            if (currentTask is IdleTask || currentTask is WanderTask)
            {
                if (highPriority.Count > 0 || normalPriority.Count > 0)
                {
                    Debug.Log($"[{name}] Unterbreche {currentTask.GetType().Name} für wichtigere Task");

                    // WICHTIG: Animator-Cleanup vor Task-Wechsel
                    if (currentTask is IdleTask && animator != null)
                    {
                        // Alle Idle-Animationen sofort stoppen
                        animator.SetFloat("Speed", 0f);
                        foreach (string idleAnim in availableIdleAnimations)
                        {
                            if (HasAnimatorBoolParameter(idleAnim))
                            {
                                animator.SetBool(idleAnim, false);
                            }
                        }
                        animator.Update(0f); // Forciere sofortiges Update
                    }

                    currentTask.OnExit(this);
                    currentTask = null;

                    // Stoppe Wanderverhalten
                    if (isWandering)
                    {
                        StopWandering();
                    }
                }
            }

            // SOFORTIGE neue Task-Zuweisung
            if (currentTask == null)
            {
                if (highPriority.Count > 0) StartNext(highPriority);
                else if (normalPriority.Count > 0) StartNext(normalPriority);
                else if (lowPriority.Count > 0) StartNext(lowPriority);
            }

            if (currentTask != null && currentTask.UpdateTask(this))
            {
                Debug.Log($"[{name}] Task beendet: {currentTask.GetType().Name}");
                currentTask.OnExit(this);
                currentTask = null;
            }

            // Wanderverhalten nur wenn aktiviert und verfügbar
            if (enableWanderBehavior && IsAvailable())
            {
                HandleWanderBehavior();
            }
        }

        private bool HasAnimatorBoolParameter(string paramName)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return false;

            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == paramName && param.type == AnimatorControllerParameterType.Bool)
                    return true;
            }
            return false;
        }

        private bool IsPerformingCriticalWork()
        {
            if (currentTask == null) return false;

            // DigTask in verschiedenen Phasen
            if (currentTask is DigTask)
            {
                // Während DigTask NIE stuck detection oder avoidance
                return true;
            }

            // ConquerTask
            if (currentTask is ConquerTask)
            {
                return true;
            }

            // Weitere kritische Tasks hier hinzufügen...

            return false;
        }


        private void HandleIdlePositionLock()
        {
            bool currentlyIdle = (currentTask is IdleTask);

            // Idle-Zustand geändert
            if (currentlyIdle != isInIdleState)
            {
                if (currentlyIdle)
                {
                    StartIdleState();
                }
                else
                {
                    EndIdleState();
                }
                isInIdleState = currentlyIdle;
            }

            // Während Idle: Position kontrollieren
            if (isInIdleState && lockPositionDuringIdle)
            {
                LockIdlePosition();
            }
        }

        private void StartIdleState()
        {
            idleStartPosition = transform.position;

            if (useRootMotionForIdle && animator != null)
            {
                wasApplyingRootMotion = agent.updatePosition;
                agent.updatePosition = false; // NavMeshAgent soll Position nicht überschreiben
                animator.applyRootMotion = true;

                Debug.Log($"[{name}] Idle-Modus: Root Motion aktiviert, Position gelockt bei {idleStartPosition}");
            }
            else
            {
                // Klassischer Approach: Komplett statisch
                if (agent != null)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
            }
        }

        private void EndIdleState()
        {
            if (useRootMotionForIdle && animator != null)
            {
                agent.updatePosition = wasApplyingRootMotion;
                animator.applyRootMotion = false;

                // Stelle sicher dass Agent an aktueller Position ist
                if (agent.isOnNavMesh)
                {
                    agent.Warp(transform.position);
                }

                Debug.Log($"[{name}] Idle-Modus beendet: Root Motion deaktiviert");
            }
            else
            {
                if (agent != null)
                {
                    agent.isStopped = false;

                    // Warp zu aktueller Position um Synchronisation sicherzustellen
                    if (agent.isOnNavMesh)
                    {
                        agent.Warp(transform.position);
                    }
                }
            }
        }

        private void LockIdlePosition()
        {
            Vector3 currentPos = transform.position;
            float driftDistance = Vector3.Distance(currentPos, idleStartPosition);

            // Erlaube kleine Bewegungen (Foot IK), aber verhindere großes Driften
            if (driftDistance > maxIdleMovement)
            {
                Vector3 correctedPosition = idleStartPosition +
                    (currentPos - idleStartPosition).normalized * maxIdleMovement;

                correctedPosition.y = currentPos.y; // Y-Position von Animation übernehmen (wichtig für Foot IK)

                transform.position = correctedPosition;

                if (debugIdleAnimations)
                {
                    Debug.Log($"[{name}] Position korrigiert: Drift {driftDistance:F3}m → {maxIdleMovement:F3}m");
                }
            }
        }

        /// <summary>
        /// Unity's OnAnimatorMove - wird für Root Motion aufgerufen
        /// </summary>
        void OnAnimatorMove()
        {
            if (!useRootMotionForIdle || !isInIdleState)
                return;

            // Root Motion nur für Y-Achse (Foot IK) anwenden, XZ blockieren
            if (animator != null)
            {
                Vector3 rootMotionDelta = animator.deltaPosition;
                Vector3 newPosition = transform.position;

                // Nur Y-Bewegung erlauben (für Foot IK / Gewichtsverlagerung)
                newPosition.y += rootMotionDelta.y;

                // XZ-Bewegung begrenzen
                Vector3 xzOffset = new Vector3(rootMotionDelta.x, 0, rootMotionDelta.z);
                if (xzOffset.magnitude < maxIdleMovement)
                {
                    newPosition.x += rootMotionDelta.x;
                    newPosition.z += rootMotionDelta.z;
                }

                transform.position = newPosition;

                // Rotation von Root Motion übernehmen (für Look-Around Animationen)
                if (!isTaskRotating) // Nur wenn keine Task-Rotation aktiv
                {
                    transform.rotation *= animator.deltaRotation;
                }
            }
        }

        private void HandleWanderBehavior()
        {
            // Behavior-Changes nur alle BEHAVIOR_CHECK_INTERVAL Sekunden prüfen
            if (Time.time - lastBehaviorChange < BEHAVIOR_CHECK_INTERVAL)
                return;

            // Entscheide was der Imp als nächstes tun soll
            DecideNextBehavior();
            lastBehaviorChange = Time.time;
        }

        private void DecideNextBehavior()
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

        private bool ShouldStartWandering()
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

        private bool ShouldStopWandering()
        {
            float stopChance = stopWanderProbability * BEHAVIOR_CHECK_INTERVAL;
            return Random.value < stopChance;
        }

        private void StartWandering()
        {
            Vector3? wanderTarget = FindWanderTarget();

            if (wanderTarget.HasValue)
            {
                isWandering = true;
                currentWanderTarget = wanderTarget.Value;

                // Wander-Task mit niedriger Priorität hinzufügen
                var wanderTask = new WanderTask(currentWanderTarget.Value);
                EnqueueTask(wanderTask, TaskPriority.Low);

                if (enableDetailedWanderLogging)
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

        private void StopWandering()
        {
            isWandering = false;
            currentWanderTarget = null;

            if (enableDetailedWanderLogging)
            {
                Debug.Log($"[WanderBehavior] {name} stoppt das Wandern");
            }
        }

        private void StartIdling()
        {
            float idleDuration = Random.Range(minIdleTime, maxIdleTime);
            var idleTask = CreateConfiguredIdleTask(idleDuration);
            EnqueueTask(idleTask, TaskPriority.Low);

            if (enableDetailedWanderLogging)
            {
                Debug.Log($"[WanderBehavior] {name} idlet für {idleDuration:F1}s mit konfigurierten Animationen");
            }
        }

        private bool HasReachedWanderTarget()
        {
            if (!currentWanderTarget.HasValue)
                return true;

            float distanceToTarget = Vector3.Distance(transform.position, currentWanderTarget.Value);
            return distanceToTarget < 1.5f;
        }

        private Vector3? FindWanderTarget()
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

        private bool IsValidWanderLocation(Vector3 worldPosition)
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

        // Rest deiner bestehenden Methoden...
        public void DebugAnimationState()
        {
            if (animator != null)
            {
                bool isDigging = animator.GetBool("IsDigging");
                float speed = animator.GetFloat("Speed");
                Debug.Log($"[{name}] Animation State - IsDigging: {isDigging}, Speed: {speed:F2}, CurrentTask: {currentTask?.GetType().Name ?? "None"}");
            }
        }

        public void CheckForNewTasksImmediately()
        {
            if (IsAvailable())
            {
                Debug.Log($"[{name}] Prüfe sofort auf neue Tasks nach Tile-Completion");

                TaskAssigner taskAssigner = FindObjectOfType<TaskAssigner>();
                if (taskAssigner != null)
                {
                    taskAssigner.TriggerImmediateAssignment();
                }
            }
        }

        void HandleStuckDetection()
        {
            // NEUE ZEILE: Nicht ausführen während kritischer Arbeit
            if (IsPerformingCriticalWork())
            {
                stuckTimer = 0f;
                isStuck = false;
                lastPosition = transform.position;
                return;
            }

            // Nicht prüfen wenn der Imp sich bewegen sollte und nicht gerade Tasks wechselt
            if (!agent.hasPath || isTaskRotating || !agent.enabled)
            {
                stuckTimer = 0f;
                isStuck = false;
                lastPosition = transform.position;
                return;
            }

            // Rest der Methode bleibt gleich...
            float distanceMoved = Vector3.Distance(transform.position, lastPosition);

            bool isMovingSlowly = distanceMoved < stuckDistanceThreshold && agent.velocity.sqrMagnitude < 0.05f;
            bool hasDestination = agent.hasPath && !agent.pathPending;

            if (isMovingSlowly && hasDestination)
            {
                stuckTimer += Time.deltaTime;

                if (stuckTimer > stuckTimeThreshold && !isStuck)
                {
                    Debug.LogWarning($"[{name}] ist STUCK nach {stuckTimer:F1}s! Emergency-Unstuck.");
                    EmergencyUnstuck();
                    isStuck = true;
                    lastUnstuckTime = Time.time;
                }
            }
            else
            {
                if (distanceMoved > stuckDistanceThreshold * 2f)
                {
                    stuckTimer = Mathf.Max(0f, stuckTimer - Time.deltaTime * 2f);
                    isStuck = false;
                }
            }

            if (Time.time % 0.5f < Time.deltaTime)
            {
                lastPosition = transform.position;
            }
        }


        void EmergencyUnstuck()
        {
            StopAllCoroutines();

            if (TryGentleUnstuck())
            {
                Debug.Log($"[{name}] wurde sanft entstuckt");
                return;
            }

            Debug.LogWarning($"[{name}] benötigt Emergency-Teleportation");
            TeleportUnstuck();
        }

        bool TryGentleUnstuck()
        {
            Collider[] nearbyImps = Physics.OverlapSphere(transform.position, avoidanceRadius * 2f, impLayerMask);
            Vector3 pushDirection = Vector3.zero;

            foreach (var collider in nearbyImps)
            {
                if (collider.gameObject == gameObject) continue;
                Vector3 directionAway = (transform.position - collider.transform.position).normalized;
                pushDirection += directionAway;
            }

            if (pushDirection != Vector3.zero)
            {
                pushDirection = pushDirection.normalized;
                Vector3 targetPosition = transform.position + pushDirection * 1.5f;

                if (UnityEngine.AI.NavMesh.SamplePosition(targetPosition, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    StartCoroutine(SmoothMoveToPosition(hit.position));
                    return true;
                }
            }

            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 testPosition = transform.position + direction * 1.2f;

                if (UnityEngine.AI.NavMesh.SamplePosition(testPosition, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    if (!Physics.CheckSphere(hit.position, 0.5f, impLayerMask))
                    {
                        StartCoroutine(SmoothMoveToPosition(hit.position));
                        return true;
                    }
                }
            }

            return false;
        }

        System.Collections.IEnumerator SmoothMoveToPosition(Vector3 targetPosition)
        {
            Vector3 startPosition = transform.position;
            float moveTime = 0.5f;
            float elapsed = 0f;

            agent.enabled = false;

            while (elapsed < moveTime)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / moveTime;

                transform.position = Vector3.Lerp(startPosition, targetPosition, progress);
                yield return null;
            }

            transform.position = targetPosition;

            agent.enabled = true;

            yield return new WaitForSeconds(0.2f);
            RestorePathAfterUnstuck();
        }

        void TeleportUnstuck()
        {
            Vector3 randomDirection = Random.insideUnitSphere * 1.2f;
            randomDirection.y = 0;
            Vector3 unstuckPosition = transform.position + randomDirection;

            if (UnityEngine.AI.NavMesh.SamplePosition(unstuckPosition, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            {
                if (!Physics.CheckSphere(hit.position, 0.3f, impLayerMask))
                {
                    agent.Warp(hit.position);
                    Debug.Log($"[{name}] wurde minimal teleportiert zu {hit.position} (Distanz: {Vector3.Distance(transform.position, hit.position):F1})");
                    StartCoroutine(RestoreNormalBehavior());
                }
                else
                {
                    randomDirection = Random.insideUnitSphere * 0.8f;
                    unstuckPosition = transform.position + randomDirection;

                    if (UnityEngine.AI.NavMesh.SamplePosition(unstuckPosition, out UnityEngine.AI.NavMeshHit fallbackHit, 1.5f, UnityEngine.AI.NavMesh.AllAreas))
                    {
                        agent.Warp(fallbackHit.position);
                        Debug.Log($"[{name}] wurde minimal teleportiert (Fallback) zu {fallbackHit.position}");
                        StartCoroutine(RestoreNormalBehavior());
                    }
                }
            }
        }

        void RestorePathAfterUnstuck()
        {
            if (agent.hasPath)
            {
                Vector3 originalDestination = agent.destination;
                agent.SetDestination(originalDestination);
            }
        }

        System.Collections.IEnumerator RestoreNormalBehavior()
        {
            yield return new WaitForSeconds(0.5f);

            if (agent.hasPath)
            {
                Vector3 originalDestination = agent.destination;
                yield return new WaitForSeconds(0.1f);
                agent.SetDestination(originalDestination);
            }
        }

        void HandleCollisionAvoidance()
        {
            // NEUE ZEILE: Nicht ausführen während kritischer Arbeit
            if (IsPerformingCriticalWork())
            {
                return;
            }

            // Nicht ausführen wenn gerade Emergency-Unstuck aktiv ist
            if (isStuck || Time.time - lastUnstuckTime < 2f)
                return;
            if (agent.velocity.sqrMagnitude < 0.05f || Time.time - lastAvoidanceTime < avoidanceCooldown * 1.5f)
                return;

            Collider[] nearbyImps = Physics.OverlapSphere(transform.position, avoidanceRadius * 0.8f, impLayerMask);

            Vector3 avoidanceDirection = Vector3.zero;
            int impCount = 0;

            foreach (var collider in nearbyImps)
            {
                if (collider.gameObject == gameObject) continue;

                UnitAI otherImp = collider.GetComponent<UnitAI>();
                if (otherImp == null) continue;

                if (otherImp.isTaskRotating || otherImp.agent.velocity.sqrMagnitude < 0.05f)
                    continue;

                Vector3 directionAway = (transform.position - collider.transform.position).normalized;
                float distance = Vector3.Distance(transform.position, collider.transform.position);

                float strength = Mathf.Clamp01((1f - (distance / (avoidanceRadius * 0.8f))) * 0.5f);
                avoidanceDirection += directionAway * strength;
                impCount++;
            }

            if (impCount > 0)
            {
                avoidanceDirection = avoidanceDirection.normalized;

                if (!isTaskRotating && !(currentTask != null && HasTrulyArrived()))
                {
                    if (Random.value < 0.3f)
                    {
                        TryGentleAvoidance(avoidanceDirection);
                    }
                }

                lastAvoidanceTime = Time.time;
            }
        }

        void TryGentleAvoidance(Vector3 avoidanceDirection)
        {
            if (!agent.hasPath) return;

            Vector3 currentDestination = agent.destination;
            Vector3 currentPos = transform.position;

            Vector3 moveDirection = (currentDestination - currentPos).normalized;
            Vector3 rightDirection = Vector3.Cross(Vector3.up, moveDirection);

            float dotRight = Vector3.Dot(avoidanceDirection, rightDirection);
            Vector3 sideDirection = dotRight > 0 ? rightDirection : -rightDirection;

            Vector3 avoidancePoint = currentPos + sideDirection * 0.8f + moveDirection * 0.3f;

            if (UnityEngine.AI.NavMesh.SamplePosition(avoidancePoint, out UnityEngine.AI.NavMeshHit hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                StartCoroutine(ReturnToOriginalDestination(currentDestination, 0.8f));
            }
        }

        System.Collections.IEnumerator ReturnToOriginalDestination(Vector3 originalDest, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.SetDestination(originalDest);
            }
        }

        void TryAvoidanceManeuver(Vector3 avoidanceDirection)
        {
            if (!agent.hasPath) return;

            Vector3 currentDestination = agent.destination;
            Vector3 currentPos = transform.position;

            Vector3 moveDirection = (currentDestination - currentPos).normalized;
            Vector3 rightDirection = Vector3.Cross(Vector3.up, moveDirection);

            float dotRight = Vector3.Dot(avoidanceDirection, rightDirection);
            Vector3 sideDirection = dotRight > 0 ? rightDirection : -rightDirection;

            Vector3 avoidancePoint = currentPos + sideDirection * 1.5f + moveDirection * 0.5f;

            if (UnityEngine.AI.NavMesh.SamplePosition(avoidancePoint, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            {
                StartCoroutine(AvoidanceCoroutine(hit.position, currentDestination));
            }
        }

        void ApplyGentlePush(Vector3 pushDirection)
        {
            Vector3 pushPosition = transform.position + pushDirection * 0.3f;

            if (UnityEngine.AI.NavMesh.SamplePosition(pushPosition, out UnityEngine.AI.NavMeshHit hit, 1f, UnityEngine.AI.NavMesh.AllAreas))
            {
                transform.position = Vector3.Lerp(transform.position, hit.position, pushForce * Time.deltaTime);
            }
        }

        System.Collections.IEnumerator AvoidanceCoroutine(Vector3 avoidancePoint, Vector3 originalDestination)
        {
            agent.SetDestination(avoidancePoint);

            while (!HasTrulyArrived() && Vector3.Distance(transform.position, avoidancePoint) > 1f)
            {
                yield return null;
            }

            yield return new WaitForSeconds(0.2f);

            agent.SetDestination(originalDestination);
        }

        void HandleRotation()
        {
            if (meshTransform != null)
            {
                meshTransform.localRotation = originalMeshRotation;
            }

            if (isTaskRotating && targetLookDirection.HasValue)
            {
                Vector3 direction = targetLookDirection.Value;
                direction.y = 0;

                if (direction.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    float currentAngle = Quaternion.Angle(transform.rotation, targetRotation);

                    float rotSpeed = taskRotationSpeed * Time.deltaTime;
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotSpeed);

                    if (currentAngle < 3f)
                    {
                        transform.rotation = targetRotation;
                        isTaskRotating = false;
                        Debug.Log($"[{name}] Task-Rotation FERTIG! Endwinkel: {currentAngle:F1}°");
                    }
                }
                else
                {
                    isTaskRotating = false;
                }

                return;
            }
            else if (agent.velocity.sqrMagnitude > 0.1f)
            {
                Vector3 direction = agent.velocity.normalized;
                direction.y = 0;

                if (direction.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }
        }

        void StartNext(Queue<ITask> queue)
        {
            currentTask = queue.Dequeue();
            currentTask.OnEnter(this);
        }

        public void EnqueueTask(ITask task, TaskPriority priority)
        {
            // NEUE LOGIK: Unterbreche Wandern/Idle für wichtige Tasks
            if (currentTask is WanderTask || currentTask is IdleTask)
            {
                if (priority == TaskPriority.High || priority == TaskPriority.Normal)
                {
                    Debug.Log($"[{name}] Unterbreche {currentTask.GetType().Name} für {task.GetType().Name}");
                    currentTask.OnExit(this);
                    currentTask = null;

                    // Stoppe Wanderverhalten
                    if (currentTask is WanderTask)
                    {
                        StopWandering();
                    }
                }
            }

            // SICHERHEITSPRÜFUNG: Verhindere Task-Assignment während wichtiger Tasks
            if (currentTask != null && !(currentTask is IdleTask) && !(currentTask is WanderTask))
            {
                Debug.LogWarning($"[{name}] Task-Assignment verweigert - bereits beschäftigt mit: {currentTask.GetType().Name}");
                return;
            }

            switch (priority)
            {
                case TaskPriority.High:
                    highPriority.Enqueue(task);
                    break;
                case TaskPriority.Normal:
                    normalPriority.Enqueue(task);
                    break;
                case TaskPriority.Low:
                    lowPriority.Enqueue(task);
                    break;
            }
        }

        public bool IsAtDestination()
            => !agent.pathPending && agent.remainingDistance <= stoppingThreshold && agent.velocity.sqrMagnitude == 0f;

        public void MoveTo(Vector3 worldPosition)
        {
            if (agent == null) return;

            if (!agent.isOnNavMesh)
            {
                Debug.LogWarning($"[{name}] Agent ist nicht auf NavMesh! Position: {transform.position}");

                UnityEngine.AI.NavMeshHit hit;
                if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out hit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                    Debug.Log($"[{name}] Agent auf NavMesh repositioniert: {hit.position}");
                }
                else
                {
                    Debug.LogError($"[{name}] Konnte Agent nicht auf NavMesh repositionieren!");
                    return;
                }
            }

            if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.SetDestination(worldPosition);
                Debug.Log($"[{name}] Bewegung zu {worldPosition} gestartet");
            }
            else
            {
                Debug.LogWarning($"[{name}] NavMeshAgent ist nicht aktiv oder nicht auf NavMesh");
            }
        }

        public bool IsNavMeshHealthy()
        {
            return agent != null &&
                   agent.isActiveAndEnabled &&
                   agent.isOnNavMesh &&
                   !agent.pathPending;
        }

        public bool IsAvailable()
        {
            // Für echte Arbeit: Imp muss komplett frei sein oder nur wandern/idlen
            return currentTask == null || currentTask is IdleTask || currentTask is WanderTask;
        }

        public void ForceAssignNewJobImmediately()
        {
            if (IsAvailable() && normalPriority.Count > 0)
                StartNext(normalPriority);
        }

        public void ClearTasks()
        {
            if (currentTask != null)
                currentTask.OnExit(this);
            highPriority.Clear();
            normalPriority.Clear();
            lowPriority.Clear();
            currentTask = null;
        }

        void LateUpdate()
        {
            if (meshTransform != null)
            {
                meshTransform.localRotation = originalMeshRotation;
                meshTransform.localPosition = Vector3.zero;
            }
        }

        public void LookAtDirection(Vector3 direction)
        {
            targetLookDirection = direction.normalized;
            isTaskRotating = true;
        }

        public void LookAtPoint(Vector3 targetPoint)
        {
            Vector3 direction = (targetPoint - transform.position).normalized;
            LookAtDirection(direction);
        }

        public void StopTaskRotation()
        {
            isTaskRotating = false;
            targetLookDirection = null;
        }

        public bool IsCorrectlyOriented()
        {
            bool result = !isTaskRotating;
            if (Time.frameCount % 30 == 0)
            {
                Debug.Log($"[{name}] IsCorrectlyOriented: isTaskRotating = {isTaskRotating}, Ergebnis = {result}");
            }
            return result;
        }

        public bool HasTrulyArrived()
        {
            bool pathPending = agent.pathPending;
            float remainingDistance = agent.remainingDistance;
            float stoppingDistance = agent.stoppingDistance;
            bool hasPath = agent.hasPath;
            float velocityMagnitude = agent.velocity.sqrMagnitude;

            bool result = false;

            if (!pathPending && remainingDistance <= stoppingDistance)
            {
                if (!hasPath || velocityMagnitude == 0f)
                {
                    result = true;
                }
            }

            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"[{name}] HasTrulyArrived: {result} " +
                         $"(PathPending: {pathPending}, RemDist: {remainingDistance:F2}, " +
                         $"StopDist: {stoppingDistance:F2}, HasPath: {hasPath}, Vel: {velocityMagnitude:F2})");
            }

            return result;
        }

        // Neue Methoden für Wanderverhalten
        public bool IsWandering()
        {
            return currentTask is WanderTask || isWandering;
        }

        public bool IsAvailableForWork()
        {
            return currentTask == null || currentTask is IdleTask || currentTask is WanderTask;
        }

        public void SetWanderHome(Vector3 newHomePosition)
        {
            homePosition = newHomePosition;
        }

        public void SetWanderBehaviorEnabled(bool enabled)
        {
            enableWanderBehavior = enabled;
            if (!enabled)
            {
                StopWandering();
            }
        }

        public void ForceStopWandering()
        {
            if (isWandering)
            {
                StopWandering();
                ClearTasks(); // Entferne alle Low-Priority Tasks
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
        public string GetRandomIdleAnimation(string currentAnimation = "")
        {
            if (availableIdleAnimations == null || availableIdleAnimations.Length == 0)
                return "Idle1";

            // Gewichtete Auswahl
            if (animationWeights != null && animationWeights.Length == availableIdleAnimations.Length)
            {
                return GetWeightedRandomAnimation(currentAnimation);
            }

            // Einfache zufällige Auswahl
            int randomIndex;
            do
            {
                randomIndex = Random.Range(0, availableIdleAnimations.Length);
            }
            while (preventSameAnimationTwice &&
                   availableIdleAnimations.Length > 1 &&
                   availableIdleAnimations[randomIndex] == currentAnimation);

            return availableIdleAnimations[randomIndex];
        }

        private string GetWeightedRandomAnimation(string currentAnimation)
        {
            float totalWeight = 0f;

            // Berechne Gesamtgewicht
            for (int i = 0; i < animationWeights.Length; i++)
            {
                if (!preventSameAnimationTwice || availableIdleAnimations[i] != currentAnimation)
                {
                    totalWeight += animationWeights[i];
                }
            }

            float randomValue = Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            // Wähle basierend auf Gewichtung
            for (int i = 0; i < availableIdleAnimations.Length; i++)
            {
                if (preventSameAnimationTwice && availableIdleAnimations[i] == currentAnimation)
                    continue;

                currentWeight += animationWeights[i];
                if (randomValue <= currentWeight)
                {
                    return availableIdleAnimations[i];
                }
            }

            // Fallback
            return availableIdleAnimations[0];
        }

        /// <summary>
        /// Prüft ob eine bestimmte Animation im Animator verfügbar ist
        /// </summary>
        public bool HasIdleAnimation(string animationName)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return false;

            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == animationName &&
                    (param.type == AnimatorControllerParameterType.Trigger ||
                     param.type == AnimatorControllerParameterType.Bool))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gibt die Zeit für den nächsten Animationswechsel zurück
        /// </summary>
        public float GetRandomAnimationSwitchTime()
        {
            return Random.Range(minAnimationSwitchTime, maxAnimationSwitchTime);
        }

        /// <summary>
        /// Erstellt eine einfache IdleTask mit den konfigurierten Einstellungen
        /// </summary>
        public IdleTask CreateConfiguredIdleTask(float duration)
        {
            var idleTask = new IdleTask(duration);

            // Übergebe Konfiguration an den Task (falls erweitert)
            if (debugIdleAnimations)
            {
                Debug.Log($"[{name}] Erstelle IdleTask mit {availableIdleAnimations.Length} verfügbaren Animationen");
            }

            return idleTask;
        }



    }

    public enum TaskPriority { High, Normal, Low }




}