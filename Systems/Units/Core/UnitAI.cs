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
        public float stuckTimeThreshold = 5.0f; // Erhöht von 3.0f
        [Tooltip("Distanz die als 'bewegungslos' gilt")]
        public float stuckDistanceThreshold = 0.05f; // Reduziert von 0.1f
        [Tooltip("Wie weich/träge die Bewegung ist (höher = weicher)")]
        public float movementSmoothing = 0.3f;

        private NavMeshAgent agent;
        private ITask currentTask;
        private readonly Queue<ITask> highPriority = new();
        private readonly Queue<ITask> normalPriority = new();
        private readonly Queue<ITask> lowPriority = new();

        [HideInInspector] public Animator animator; // Öffentlich für Tasks
        private Quaternion originalMeshRotation; // Ursprüngliche Mesh-Rotation speichern
        private Vector3? targetLookDirection; // Zielrichtung für Tasks
        [HideInInspector] public bool isTaskRotating = false; // Öffentlich für Debug
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

        [Header("Animation Settings")]
        [Tooltip("Name der Dig-Animation im Animator")]
        public string digAnimationName = "dig";

        [Tooltip("Basis-Länge der Dig-Animation in Sekunden (wird durch Animator Speed geteilt)")]
        public float digAnimationBaseLength = 1.0f;

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
                meshTransform = transform.GetChild(0); // Nimmt das erste Child

            // Ursprüngliche Mesh-Rotation speichern
            if (meshTransform != null)
                originalMeshRotation = meshTransform.localRotation;

            // Stuck-Detection initialisieren
            lastPosition = transform.position;

            // NavMeshAgent für weichere Bewegung konfigurieren
            agent.acceleration = 8f; // Reduziert von Standard 8f
            agent.angularSpeed = 120f; // Reduziert von Standard 120f für weichere Drehungen
        }

        // ÄNDERUNGEN für UnitAI.cs Update() Methode
        // Füge diese Änderungen in deine UnitAI.cs ein:

        // KRITISCHE ÄNDERUNG in UnitAI.cs Update() Methode:
        // Entferne diese Zeilen aus der Update() Methode:

        /*
        // DIESE ZEILEN LÖSCHEN/AUSKOMMENTIEREN:
        if (animator != null)
        {
            animator.SetBool("IsDigging", false);
            Debug.Log($"[{name}] Dig-Animation sicherheitshalber zurückgesetzt");
        }
        */

        // Die neue Update() Methode sollte so aussehen:

        // In UnitAI.cs Update() Methode ändern:
        void Update()
        {
            float speed = agent.velocity.magnitude;
            animator.SetFloat("Speed", speed);

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

            // SOFORTIGE Task-Aufnahme wenn Idle
            if (currentTask is IdleTask && (highPriority.Count > 0 || normalPriority.Count > 0))
            {
                currentTask.OnExit(this);
                currentTask = null;
            }

            // SOFORTIGE neue Task-Zuweisung
            if (currentTask == null)
            {
                if (highPriority.Count > 0) StartNext(highPriority);
                else if (normalPriority.Count > 0) StartNext(normalPriority);
                else if (lowPriority.Count > 0) StartNext(lowPriority);
                // WICHTIG: Kein IdleTask mehr - TaskAssigner kann sofort zuweisen
            }

            if (currentTask != null && currentTask.UpdateTask(this))
            {
                Debug.Log($"[{name}] Task beendet: {currentTask.GetType().Name}");
                currentTask.OnExit(this);
                currentTask = null;
                // SOFORT verfügbar für neue Tasks
            }
        }

        // Neue Hilfsmethode für Debug-Zwecke
        public void DebugAnimationState()
        {
            if (animator != null)
            {
                bool isDigging = animator.GetBool("IsDigging");
                float speed = animator.GetFloat("Speed");
                Debug.Log($"[{name}] Animation State - IsDigging: {isDigging}, Speed: {speed:F2}, CurrentTask: {currentTask?.GetType().Name ?? "None"}");
            }
        }


        // In UnitAI.cs hinzufügen (falls noch nicht vorhanden):
        public void CheckForNewTasksImmediately()
        {
            if (IsAvailable())
            {
                Debug.Log($"[{name}] Prüfe sofort auf neue Tasks nach Tile-Completion");

                // TaskAssigner triggern
                TaskAssigner taskAssigner = FindObjectOfType<TaskAssigner>();
                if (taskAssigner != null)
                {
                    taskAssigner.TriggerImmediateAssignment();
                }
            }
        }

        void HandleStuckDetection()
        {
            // Nur prüfen wenn der Imp sich bewegen sollte und nicht gerade Tasks wechselt
            if (!agent.hasPath || isTaskRotating || !agent.enabled)
            {
                stuckTimer = 0f;
                isStuck = false;
                lastPosition = transform.position;
                return;
            }

            // Prüfen ob sich der Imp bewegt hat (über längeren Zeitraum)
            float distanceMoved = Vector3.Distance(transform.position, lastPosition);

            // Weniger strenge Stuck-Detection
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
                // Nur zurücksetzen wenn wirklich Bewegung stattgefunden hat
                if (distanceMoved > stuckDistanceThreshold * 2f)
                {
                    stuckTimer = Mathf.Max(0f, stuckTimer - Time.deltaTime * 2f); // Langsames Abbauen
                    isStuck = false;
                }
            }

            // Position nur alle 0.5 Sekunden aktualisieren für weniger aggressive Detection
            if (Time.time % 0.5f < Time.deltaTime)
            {
                lastPosition = transform.position;
            }
        }

        void EmergencyUnstuck()
        {
            // Stoppe alle Coroutines die eventuell laufen
            StopAllCoroutines();

            // Versuche zuerst sanftere Methoden
            if (TryGentleUnstuck())
            {
                Debug.Log($"[{name}] wurde sanft entstuckt");
                return;
            }

            // Falls sanfte Methode fehlschlägt, verwende Teleportation als letzten Ausweg
            Debug.LogWarning($"[{name}] benötigt Emergency-Teleportation");
            TeleportUnstuck();
        }

        bool TryGentleUnstuck()
        {
            // Methode 1: Versuche starkes Push-Away von anderen Imps
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
                    // Sanfte Bewegung über mehrere Frames
                    StartCoroutine(SmoothMoveToPosition(hit.position));
                    return true;
                }
            }

            // Methode 2: Versuche zufällige Richtung
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 testPosition = transform.position + direction * 1.2f;

                if (UnityEngine.AI.NavMesh.SamplePosition(testPosition, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    // Prüfe ob Position frei von anderen Imps ist
                    if (!Physics.CheckSphere(hit.position, 0.5f, impLayerMask))
                    {
                        StartCoroutine(SmoothMoveToPosition(hit.position));
                        return true;
                    }
                }
            }

            return false; // Sanfte Methoden fehlgeschlagen
        }

        System.Collections.IEnumerator SmoothMoveToPosition(Vector3 targetPosition)
        {
            Vector3 startPosition = transform.position;
            float moveTime = 0.5f; // Zeit für die Bewegung
            float elapsed = 0f;

            // NavMeshAgent temporär deaktivieren für manuelle Bewegung
            agent.enabled = false;

            while (elapsed < moveTime)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / moveTime;

                // Smooth interpolation
                transform.position = Vector3.Lerp(startPosition, targetPosition, progress);
                yield return null;
            }

            transform.position = targetPosition;

            // NavMeshAgent wieder aktivieren
            agent.enabled = true;

            // Kurz warten und dann Pfad wiederherstellen
            yield return new WaitForSeconds(0.2f);
            RestorePathAfterUnstuck();
        }

        void TeleportUnstuck()
        {
            // Nur als allerletzter Ausweg - finde einen nahen freien Punkt
            Vector3 randomDirection = Random.insideUnitSphere * 1.2f; // Reduziert von 3f auf 1.2f
            randomDirection.y = 0;
            Vector3 unstuckPosition = transform.position + randomDirection;

            if (UnityEngine.AI.NavMesh.SamplePosition(unstuckPosition, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            {
                // Prüfe nochmal ob Position wirklich frei ist
                if (!Physics.CheckSphere(hit.position, 0.3f, impLayerMask))
                {
                    agent.Warp(hit.position);
                    Debug.Log($"[{name}] wurde minimal teleportiert zu {hit.position} (Distanz: {Vector3.Distance(transform.position, hit.position):F1})");
                    StartCoroutine(RestoreNormalBehavior());
                }
                else
                {
                    // Position ist besetzt, versuche es nochmal mit anderer Richtung
                    randomDirection = Random.insideUnitSphere * 0.8f; // Noch näher
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
            // Versuche ursprünglichen Pfad wiederherzustellen
            if (agent.hasPath)
            {
                Vector3 originalDestination = agent.destination;
                agent.SetDestination(originalDestination);
            }
        }

        System.Collections.IEnumerator RestoreNormalBehavior()
        {
            // Kurz warten
            yield return new WaitForSeconds(0.5f);

            // Pfad zur ursprünglichen Destination wiederherstellen
            if (agent.hasPath)
            {
                Vector3 originalDestination = agent.destination;
                yield return new WaitForSeconds(0.1f);
                agent.SetDestination(originalDestination);
            }
        }

        void HandleCollisionAvoidance()
        {
            // Nicht ausführen wenn gerade Emergency-Unstuck aktiv ist
            if (isStuck || Time.time - lastUnstuckTime < 2f) // Längere Pause nach Unstuck
                return;

            // Weniger aggressive Kollisionsvermeidung
            if (agent.velocity.sqrMagnitude < 0.05f || Time.time - lastAvoidanceTime < avoidanceCooldown * 1.5f)
                return;

            // Andere Imps in der Nähe finden
            Collider[] nearbyImps = Physics.OverlapSphere(transform.position, avoidanceRadius * 0.8f, impLayerMask);

            Vector3 avoidanceDirection = Vector3.zero;
            int impCount = 0;

            foreach (var collider in nearbyImps)
            {
                if (collider.gameObject == gameObject) continue;

                UnitAI otherImp = collider.GetComponent<UnitAI>();
                if (otherImp == null) continue;

                // Ignoriere Imps die sich nicht bewegen oder arbeiten
                if (otherImp.isTaskRotating || otherImp.agent.velocity.sqrMagnitude < 0.05f)
                    continue;

                Vector3 directionAway = (transform.position - collider.transform.position).normalized;
                float distance = Vector3.Distance(transform.position, collider.transform.position);

                // Sanftere Ausweichkraft
                float strength = Mathf.Clamp01((1f - (distance / (avoidanceRadius * 0.8f))) * 0.5f);
                avoidanceDirection += directionAway * strength;
                impCount++;
            }

            if (impCount > 0)
            {
                avoidanceDirection = avoidanceDirection.normalized;

                // Nur sehr sanfte Korrekturen
                if (!isTaskRotating && !(currentTask != null && HasTrulyArrived()))
                {
                    // Weniger aggressive Ausweichmanöver
                    if (Random.value < 0.3f) // Nur 30% Chance für Ausweichmanöver
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

            // Sehr sanfte seitliche Korrektur
            Vector3 moveDirection = (currentDestination - currentPos).normalized;
            Vector3 rightDirection = Vector3.Cross(Vector3.up, moveDirection);

            float dotRight = Vector3.Dot(avoidanceDirection, rightDirection);
            Vector3 sideDirection = dotRight > 0 ? rightDirection : -rightDirection;

            // Kleinere Ausweichkorrektur
            Vector3 avoidancePoint = currentPos + sideDirection * 0.8f + moveDirection * 0.3f;

            if (UnityEngine.AI.NavMesh.SamplePosition(avoidancePoint, out UnityEngine.AI.NavMeshHit hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                // Sanfte Richtungskorrektur ohne Coroutine
                agent.SetDestination(hit.position);

                // Nach kurzer Zeit zurück zum ursprünglichen Ziel
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

            // Berechne seitliche Ausweichrichtung
            Vector3 moveDirection = (currentDestination - currentPos).normalized;
            Vector3 rightDirection = Vector3.Cross(Vector3.up, moveDirection);

            // Wähle die Seite, die mehr in Richtung der Ausweichrichtung zeigt
            float dotRight = Vector3.Dot(avoidanceDirection, rightDirection);
            Vector3 sideDirection = dotRight > 0 ? rightDirection : -rightDirection;

            // Berechne Ausweichpunkt
            Vector3 avoidancePoint = currentPos + sideDirection * 1.5f + moveDirection * 0.5f;

            // Prüfe ob Ausweichpunkt auf NavMesh ist
            if (UnityEngine.AI.NavMesh.SamplePosition(avoidancePoint, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            {
                // Temporär zum Ausweichpunkt navigieren, dann zum ursprünglichen Ziel zurück
                StartCoroutine(AvoidanceCoroutine(hit.position, currentDestination));
            }
        }

        void ApplyGentlePush(Vector3 pushDirection)
        {
            // Sanftes Wegschubsen für Imps die an fester Position arbeiten
            Vector3 pushPosition = transform.position + pushDirection * 0.3f;

            if (UnityEngine.AI.NavMesh.SamplePosition(pushPosition, out UnityEngine.AI.NavMeshHit hit, 1f, UnityEngine.AI.NavMesh.AllAreas))
            {
                transform.position = Vector3.Lerp(transform.position, hit.position, pushForce * Time.deltaTime);
            }
        }

        System.Collections.IEnumerator AvoidanceCoroutine(Vector3 avoidancePoint, Vector3 originalDestination)
        {
            // Zum Ausweichpunkt navigieren
            agent.SetDestination(avoidancePoint);

            // Warten bis Ausweichpunkt erreicht
            while (!HasTrulyArrived() && Vector3.Distance(transform.position, avoidancePoint) > 1f)
            {
                yield return null;
            }

            // Kurz warten
            yield return new WaitForSeconds(0.2f);

            // Zurück zum ursprünglichen Ziel
            agent.SetDestination(originalDestination);
        }

        void HandleRotation()
        {
            // Mesh-Rotation korrigieren falls durch Kollision verfälscht
            if (meshTransform != null)
            {
                meshTransform.localRotation = originalMeshRotation;
            }

            // NEUE ZEILE: Task-spezifische Rotation hat ABSOLUTE Priorität
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

                return; // WICHTIG: Verhindert normale Bewegungs-Rotation
            }

            // Normale Bewegungs-Rotation nur wenn KEINE Task-Rotation aktiv ist
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

        // In UnitAI.cs überschreibe die EnqueueTask Methode:
        public void EnqueueTask(ITask task, TaskPriority priority)
        {
            // SICHERHEITSPRÜFUNG: Verhindere Task-Assignment während aktueller Task-Ausführung
            if (currentTask != null)
            {
                Debug.LogWarning($"[{name}] Task-Assignment verweigert - bereits beschäftigt mit: {currentTask.GetType().Name}");
                return;
            }

            // Original-Logik
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

        // In UnitAI.cs - erweitere die MoveTo Methode:
        // In UnitAI.cs - erweitere die MoveTo Methode:
        public void MoveTo(Vector3 worldPosition)
        {
            if (agent == null) return;

            // NEUE VALIDIERUNG: Prüfe NavMesh-Status
            if (!agent.isOnNavMesh)
            {
                Debug.LogWarning($"[{name}] Agent ist nicht auf NavMesh! Position: {transform.position}");

                // Versuche Agent auf NavMesh zu repositionieren
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

            // ROBUSTERE Destination-Setzung
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

        // NEUE METHODE: NavMesh-Status prüfen
        public bool IsNavMeshHealthy()
        {
            return agent != null &&
                   agent.isActiveAndEnabled &&
                   agent.isOnNavMesh &&
                   !agent.pathPending;
        }

        public bool IsAvailable()
            => currentTask == null;

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
            // LateUpdate stellt sicher, dass Mesh-Korrekturen nach allen anderen Updates passieren
            if (meshTransform != null)
            {
                // Mesh-Rotation komplett zurücksetzen
                meshTransform.localRotation = originalMeshRotation;
                meshTransform.localPosition = Vector3.zero; // Falls auch Position verfälscht wird
            }
        }

        /// <summary>
        /// Lässt den Imp in eine bestimmte Richtung schauen (für Tasks wie Digging)
        /// </summary>
        /// <param name="direction">Die Richtung, in die geschaut werden soll</param>
        public void LookAtDirection(Vector3 direction)
        {
            targetLookDirection = direction.normalized;
            isTaskRotating = true;
        }

        /// <summary>
        /// Lässt den Imp zu einem bestimmten Punkt schauen (für Tasks wie Digging)
        /// </summary>
        /// <param name="targetPoint">Der Punkt, zu dem geschaut werden soll</param>
        public void LookAtPoint(Vector3 targetPoint)
        {
            Vector3 direction = (targetPoint - transform.position).normalized;
            LookAtDirection(direction);
        }

        /// <summary>
        /// Stoppt die Task-spezifische Rotation
        /// </summary>
        public void StopTaskRotation()
        {
            isTaskRotating = false;
            targetLookDirection = null;
        }

        /// <summary>
        /// Prüft ob der Imp korrekt ausgerichtet ist
        /// </summary>
        /// <returns>True wenn die Rotation abgeschlossen ist</returns>
        public bool IsCorrectlyOriented()
        {
            bool result = !isTaskRotating;
            if (Time.frameCount % 30 == 0) // Nur alle halbe Sekunde debuggen
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

            // Debug nur gelegentlich um Spam zu vermeiden
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"[{name}] HasTrulyArrived: {result} " +
                         $"(PathPending: {pathPending}, RemDist: {remainingDistance:F2}, " +
                         $"StopDist: {stoppingDistance:F2}, HasPath: {hasPath}, Vel: {velocityMagnitude:F2})");
            }

            return result;
        }
    }

    public enum TaskPriority { High, Normal, Low }
}