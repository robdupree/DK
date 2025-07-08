using UnityEngine;
using DK;

namespace DK.Tasks
{
    public class DigTask : ITask
    {
        private readonly Task dataTask;
        private DungeonTileData tileData;
        private Vector3Int tilePos;
        private Vector3 slotWorldPos;
        private Vector3Int slotDir;
        private bool slotReserved = false;
        private bool arrivedAtSlot = false;
        private bool rotatedToWall = false;

        // Animation-Kontrolle
        private bool isReadyToStartCycle = false;
        private bool isWaitingForDamage = false;
        private bool hasCycleCompleted = false;
        private float lastNormalizedTime = 0f;
        private int completedCycles = 0;
        private float cycleStartTime = 0f;
        private bool animationJustStarted = false;

        // Fallback Timer
        private float fallbackCycleTimer = 0f;
        private readonly float fallbackCycleDuration = 2f;

        // MINIMALE ÄNDERUNG: Nur Arrival-Validierung verkürzt
        private bool hasValidatedArrival = false;
        private float arrivalValidationTime = 0f;
        private const float ARRIVAL_VALIDATION_DURATION = 0.1f; // NUR das verkürzt von 0.5s

        public DigTask(Task taskData, float duration)
        {
            dataTask = taskData;
            tilePos = new Vector3Int(taskData.location.x, taskData.location.y, 0);

            // Alle Flags zurücksetzen
            slotReserved = false;
            arrivedAtSlot = false;
            rotatedToWall = false;
            isReadyToStartCycle = false;
            isWaitingForDamage = false;
            hasCycleCompleted = false;
            lastNormalizedTime = 0f;
            completedCycles = 0;
            animationJustStarted = false;
            fallbackCycleTimer = 0f;
            hasValidatedArrival = false;
            arrivalValidationTime = 0f;

            Debug.Log($"Neuer DigTask erstellt für Tile {tilePos} - Mit verkürzter Arrival-Validierung");
        }

        public void OnEnter(UnitAI ai)
        {
            Debug.Log($"[{ai.name}] DigTask.OnEnter für Tile {tilePos}");

            ai.StopTaskRotation();

            // NEUE SEKTION: AGGRESSIVE ANIMATION-BEREINIGUNG
            ForceStopAllIdleAnimations(ai);

            // Animator sofort zurücksetzen
            if (ai.animator != null)
            {
                ai.animator.SetBool("KeepDigging", false);
                for (int i = 0; i < 3; i++)
                {
                    ai.animator.ResetTrigger("DigTrigger");
                }
                Debug.Log($"[{ai.name}] Animator beim Enter zurückgesetzt");
            }

            if (!DigManager.Instance.tileMap.TryGetValue(tilePos, out tileData))
            {
                Debug.LogError($"[{ai.name}] Kein TileData für Position {tilePos} gefunden! Task wird abgebrochen.");
                TaskManager.Instance.CompleteTask(dataTask);
                return;
            }

            // VALIDIERUNG: Prüfe ob Tile noch grabbar ist
            if (tileData.State != TileState.Wall_Marked && tileData.State != TileState.Wall_BeingDug)
            {
                Debug.LogWarning($"[{ai.name}] Tile {tilePos} ist nicht mehr markiert (State: {tileData.State}). Task wird abgebrochen.");
                TaskManager.Instance.CompleteTask(dataTask);
                return;
            }

            // Slot-Reservierung
            var availableDirections = tileData.GetAvailableDirections();
            if (availableDirections.Count == 0)
            {
                Debug.LogWarning($"[{ai.name}] Tile {tilePos} hat keine verfügbaren Richtungen. Task wird abgebrochen.");
                TaskManager.Instance.CompleteTask(dataTask);
                return;
            }

            if (tileData.TryReserveBestSlot(ai.transform.position, out var bestDir, out var bestWorldPos))
            {
                slotDir = bestDir;
                slotWorldPos = bestWorldPos;
                tileData.AssignWorker(ai.gameObject);
                slotReserved = true;

                // WICHTIG: Bewegung zum Slot starten
                Debug.Log($"[{ai.name}] Starte Bewegung zu Slot: {slotWorldPos}");
                ai.MoveTo(slotWorldPos);

                // Tile als "wird bearbeitet" markieren
                if (tileData.State == TileState.Wall_Marked)
                {
                    tileData.State = TileState.Wall_BeingDug;
                    DebugTileMonitor.LogStateChange(tilePos, TileState.Wall_Marked, TileState.Wall_BeingDug, "DigTask.OnEnter");
                }

                Debug.Log($"[{ai.name}] OPTIMALER Slot reserviert: {slotWorldPos}, Richtung: {slotDir}");
            }
            else
            {
                Debug.LogWarning($"[{ai.name}] Konnte keinen freien Dig-Slot für Tile {tilePos} finden! Task wird abgebrochen.");
                TaskManager.Instance.CompleteTask(dataTask);
                return;
            }
        }

        private void ForceStopAllIdleAnimations(UnitAI ai)
        {
            if (ai.animator == null) return;

            Debug.Log($"[{ai.name}] FORCE-STOP aller Idle-Animationen für Dig-Task");

            // 1. Alle bekannten Idle-Parameter aggressiv deaktivieren
            string[] commonIdleParams = {
        "Idle1", "Idle2", "Idle3", "IdleLookAround",
        "IdleStretch", "IdleBored", "IsIdle", "IdleIndex"
    };

            foreach (string param in commonIdleParams)
            {
                if (HasAnimatorParameter(ai.animator, param, AnimatorControllerParameterType.Bool))
                {
                    ai.animator.SetBool(param, false);
                }
                else if (HasAnimatorParameter(ai.animator, param, AnimatorControllerParameterType.Int))
                {
                    ai.animator.SetInteger(param, 0);
                }
            }

            // 2. Konfigurierte Idle-Animationen deaktivieren
            if (ai.availableIdleAnimations != null)
            {
                foreach (string idleAnim in ai.availableIdleAnimations)
                {
                    if (HasAnimatorParameter(ai.animator, idleAnim, AnimatorControllerParameterType.Bool))
                    {
                        ai.animator.SetBool(idleAnim, false);
                    }
                }
            }

            // 3. Speed explizit setzen
            ai.animator.SetFloat("Speed", 0f);

            // 4. MEHRFACH-UPDATE: Forciere sofortige Anwendung
            ai.animator.Update(0f);
            ai.animator.Update(0.01f);  // Zwei Updates um sicherzustellen

            // 5. Zusätzlich: Play direkt zum Base State wenn möglich
            try
            {
                ai.animator.Play("Idle", 0, 0f); // Spiele Standard-Idle ab
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[{ai.name}] Konnte nicht zu Base-Idle wechseln: {e.Message}");
            }

            Debug.Log($"[{ai.name}] Idle-Animationen FORCE-GESTOPPT");
        }


        private bool HasAnimatorParameter(Animator animator, string paramName, AnimatorControllerParameterType paramType)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return false;

            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == paramName && param.type == paramType)
                    return true;
            }
            return false;
        }

        public bool UpdateTask(UnitAI ai)
        {
            if (!slotReserved || tileData == null || !tileData.AssignedWorkers.Contains(ai.gameObject))
            {
                Debug.LogWarning($"[{ai.name}] DigTask ungültig - Task wird beendet");
                return true;
            }

            // KONTINUIERLICHE VALIDIERUNG
            if (!DigManager.Instance.tileMap.ContainsKey(tilePos))
            {
                Debug.LogWarning($"[{ai.name}] Tile {tilePos} existiert nicht mehr! Task wird beendet.");
                return true;
            }

            if (tileData.State != TileState.Wall_BeingDug && tileData.State != TileState.Wall_Marked)
            {
                Debug.LogWarning($"[{ai.name}] Tile {tilePos} ist nicht mehr grabbar (State: {tileData.State}). Task wird beendet.");
                return true;
            }

            // Phase 1: Zum Slot bewegen (MIT Validierung)
            if (!arrivedAtSlot)
            {
                return HandleMovementPhase(ai);
            }

            // Phase 2: Zur Wand rotieren  
            if (!rotatedToWall)
            {
                return HandleRotationPhase(ai);
            }

            // Phase 3: Graben (NUR wenn wirklich angekommen)
            return HandleDigCycles(ai);
        }

        /// <summary>
        /// Behandelt die Bewegung zum Slot mit robuster Ankunfts-Validierung
        /// </summary>
        private bool HandleMovementPhase(UnitAI ai)
        {
            // Prüfe ob Imp angekommen ist
            bool hasArrived = ai.HasTrulyArrived();
            float distanceToSlot = Vector3.Distance(ai.transform.position, slotWorldPos);

            // Debug nur alle 60 Frames um Spam zu vermeiden
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"[{ai.name}] Bewegung zu Slot: HasArrived={hasArrived}, " +
                         $"DistanceToSlot={distanceToSlot:F2}m, RemainingDistance={ai.GetComponent<UnityEngine.AI.NavMeshAgent>().remainingDistance:F2}");
            }

            // EINZIGER FIX: Nur die finale Distanz-Prüfung anpassen
            if (!hasArrived && distanceToSlot < 0.15f)
            {
                Debug.Log($"[{ai.name}] Fallback: Imp ist nah genug am Slot ({distanceToSlot:F2}m)");
                hasArrived = true;
            }

            if (hasArrived)
            {
                if (!hasValidatedArrival)
                {
                    // WICHTIG: NavMeshAgent SOFORT stoppen um weitere Bewegung zu verhindern
                    var navAgent = ai.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (navAgent != null)
                    {
                        navAgent.isStopped = true;
                        navAgent.velocity = Vector3.zero; // Sofortiger Stopp
                    }

                    // Starte Arrival-Validierung
                    hasValidatedArrival = true;
                    arrivalValidationTime = Time.time;
                    Debug.Log($"[{ai.name}] NavMeshAgent gestoppt - Starte Arrival-Validierung für {ARRIVAL_VALIDATION_DURATION}s");
                    return false;
                }
                else
                {
                    // Prüfe ob Validierungszeit abgelaufen ist
                    if (Time.time - arrivalValidationTime >= ARRIVAL_VALIDATION_DURATION)
                    {
                        // EINZIGER FIX: Grosszügigere finale Distanz
                        float finalDistance = Vector3.Distance(ai.transform.position, slotWorldPos);
                        if (finalDistance < 0.8f) // ANGEPASST: 0.8m statt 0.3m (für 0.37m Problem)
                        {
                            arrivedAtSlot = true;

                            Debug.Log($"[{ai.name}] ✅ ARRIVAL VALIDIERT nach {Time.time - arrivalValidationTime:F1}s - beginne Rotation zur Wand");
                            Debug.Log($"[{ai.name}] Finale Position: {ai.transform.position}, Slot: {slotWorldPos}, Distanz: {finalDistance:F2}m");

                            // URSPRÜNGLICHE ROTATION-LOGIK (KEIN TELEPORTING)
                            if (ai.tileController != null && ai.tileController.tilemap != null)
                            {
                                Vector3 wallWorldPos = ai.tileController.tilemap.GetCellCenterWorld(tilePos);
                                ai.LookAtPoint(wallWorldPos);
                            }
                            else
                            {
                                Debug.LogError($"[{ai.name}] ai.tileController oder tilemap ist null!");
                            }
                            return false;
                        }
                        else
                        {
                            // Zu weit weg - reaktiviere NavMeshAgent und versuche nochmal
                            Debug.LogWarning($"[{ai.name}] Zu weit vom Slot ({finalDistance:F2}m) - reaktiviere Navigation");
                            var navAgent = ai.GetComponent<UnityEngine.AI.NavMeshAgent>();
                            if (navAgent != null)
                            {
                                navAgent.isStopped = false;
                            }
                            hasValidatedArrival = false;

                            // Präzise Bewegung zum exakten Slot
                            ai.MoveTo(slotWorldPos);
                            return false;
                        }
                    }
                    else
                    {
                        // Noch in Validierungsphase - warten ohne Bewegung
                        float remaining = ARRIVAL_VALIDATION_DURATION - (Time.time - arrivalValidationTime);
                        if (Time.frameCount % 60 == 0)
                        {
                            Debug.Log($"[{ai.name}] Arrival-Validierung läuft... {remaining:F1}s verbleibend (gestoppt)");
                        }
                        return false;
                    }
                }
            }
            else
            {
                // Noch nicht angekommen - Validierung zurücksetzen falls aktiv
                if (hasValidatedArrival)
                {
                    Debug.Log($"[{ai.name}] Reset Arrival-Validierung - Imp noch nicht angekommen");
                    hasValidatedArrival = false;

                    // NavMeshAgent reaktivieren falls gestoppt
                    var navAgent = ai.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (navAgent != null && navAgent.isStopped)
                    {
                        navAgent.isStopped = false;
                    }
                }

                // MINIMALE ÄNDERUNG: Nur bei größerer Distanz MoveTo aufrufen
                if (distanceToSlot > 0.2f)
                {
                    ai.MoveTo(slotWorldPos);
                }
                return false;
            }
        }

        /// <summary>
        /// EINFACHE Wand-Rotation ohne komplexe Fallbacks
        /// </summary>
        private void StartSimpleWallRotation(UnitAI ai)
        {
            // EINFACHER ANSATZ: Verwende DigManager tilemap direkt
            if (DigManager.Instance?.tileController?.tilemap != null)
            {
                try
                {
                    Vector3 wallWorldPos = DigManager.Instance.tileController.tilemap.GetCellCenterWorld(tilePos);
                    ai.LookAtPoint(wallWorldPos);
                    Debug.Log($"[{ai.name}] Einfache Rotation zur Wand gestartet");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[{ai.name}] Rotation-Fehler: {e.Message} - überspringe Rotation");
                }
            }
            else
            {
                Debug.LogWarning($"[{ai.name}] Tilemap nicht verfügbar - überspringe Rotation");
            }

            // Markiere Rotation immer als abgeschlossen
            rotatedToWall = true;
        }
        /// <summary>
        /// NEUE METHODE: Verzögerte Bewegung nach NavMesh-Reset
        /// </summary>
        private System.Collections.IEnumerator DelayedMoveTo(UnitAI ai, Vector3 targetPos)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame(); // 2 Frames warten für NavMesh-Stabilisierung

            Debug.Log($"[{ai.name}] Verzögerte Bewegung zu {targetPos} nach NavMesh-Reset");
            ai.MoveTo(targetPos);
        }

        /// <summary>
        /// ROBUSTE Wand-Rotation mit mehreren Fallbacks
        /// </summary>
        private void StartWallRotation(UnitAI ai)
        {
            Vector3 wallWorldPos = Vector3.zero;
            bool rotationStarted = false;

            // FALLBACK 1: ai.tileController.tilemap
            if (ai.tileController != null && ai.tileController.tilemap != null)
            {
                try
                {
                    wallWorldPos = ai.tileController.tilemap.GetCellCenterWorld(tilePos);
                    ai.LookAtPoint(wallWorldPos);
                    rotationStarted = true;
                    Debug.Log($"[{ai.name}] Rotation gestartet via ai.tileController.tilemap");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[{ai.name}] ai.tileController.tilemap Fehler: {e.Message}");
                }
            }

            // FALLBACK 2: DigManager.Instance.tileController.tilemap
            if (!rotationStarted && DigManager.Instance?.tileController?.tilemap != null)
            {
                try
                {
                    wallWorldPos = DigManager.Instance.tileController.tilemap.GetCellCenterWorld(tilePos);
                    ai.LookAtPoint(wallWorldPos);
                    rotationStarted = true;
                    Debug.Log($"[{ai.name}] Rotation gestartet via DigManager.tileController.tilemap");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[{ai.name}] DigManager.tileController.tilemap Fehler: {e.Message}");
                }
            }

            // FALLBACK 3: Direkte Berechnung basierend auf Tile-Position
            if (!rotationStarted)
            {
                // Berechne Wand-Position basierend auf Tile-Koordinaten
                wallWorldPos = new Vector3(tilePos.x + 0.5f, 0f, tilePos.y + 0.5f);

                Vector3 directionToWall = (wallWorldPos - ai.transform.position).normalized;
                directionToWall.y = 0; // Keine Y-Rotation

                if (directionToWall != Vector3.zero)
                {
                    ai.transform.rotation = Quaternion.LookRotation(directionToWall);
                    rotationStarted = true;
                    Debug.Log($"[{ai.name}] Direkte Rotation zur berechneten Wand-Position: {wallWorldPos}");
                }
            }

            // FALLBACK 4: Rotation basierend auf Slot-Richtung
            if (!rotationStarted)
            {
                Vector3 slotDirection = Vector3.zero;

                if (slotDir == Vector3Int.right) slotDirection = Vector3.right;
                else if (slotDir == Vector3Int.left) slotDirection = Vector3.left;
                else if (slotDir == Vector3Int.up) slotDirection = Vector3.forward; // Y+ -> Z+ wegen Swizzle
                else if (slotDir == Vector3Int.down) slotDirection = Vector3.back; // Y- -> Z- wegen Swizzle

                if (slotDirection != Vector3.zero)
                {
                    ai.transform.rotation = Quaternion.LookRotation(slotDirection);
                    rotationStarted = true;
                    Debug.Log($"[{ai.name}] Fallback-Rotation basierend auf Slot-Richtung: {slotDir} -> {slotDirection}");
                }
            }

            // LAST RESORT: Überspringe Rotation komplett
            if (!rotationStarted)
            {
                Debug.LogError($"[{ai.name}] ALLE Rotations-Fallbacks fehlgeschlagen - überspringe Rotation!");
            }

            // Markiere Rotation als abgeschlossen (auch bei Fehlern)
            rotatedToWall = true;
        }

        /// <summary>
        /// Behandelt die Rotation zur Wand
        /// </summary>
        private bool HandleRotationPhase(UnitAI ai)
        {
            // Stelle sicher dass NavMeshAgent gestoppt bleibt während Rotation
            var navAgent = ai.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navAgent != null && !navAgent.isStopped)
            {
                navAgent.isStopped = true;
                Debug.Log($"[{ai.name}] NavMeshAgent gestoppt für Rotation");
            }

            if (ai.IsCorrectlyOriented())
            {
                rotatedToWall = true;
                Debug.Log($"[{ai.name}] ✅ ROTATION ABGESCHLOSSEN - starte Graben nach kurzer Pause");

                // MINIMALE ÄNDERUNG: Verkürzte Pause vor dem ersten Graben
                StartCoroutine(ai, DelayedDigStart());
            }
            else
            {
                // Debug für Rotation-Probleme
                if (Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[{ai.name}] Rotiere zur Wand... isTaskRotating: {ai.isTaskRotating}");
                }
            }
            return false;
        }

        private bool HandleDigCycles(UnitAI ai)
        {
            if (ai.animator == null)
            {
                return HandleFallbackDigging(ai);
            }

            // Neuen Zyklus starten wenn bereit
            if (isReadyToStartCycle && !isWaitingForDamage)
            {
                StartNewDigCycle(ai);
                return false;
            }

            // Warten auf Zyklus-Abschluss
            if (isWaitingForDamage)
            {
                bool cycleComplete = CheckForCompletedCycle(ai);

                if (cycleComplete)
                {
                    return ProcessCompletedCycle(ai);
                }
            }

            return false;
        }

        private void StartNewDigCycle(UnitAI ai)
        {
            Debug.Log($"[{ai.name}] 🔨 STARTE NEUEN GRAB-ZYKLUS #{completedCycles + 1} (Tile Health: {tileData.Health})");

            // Flags für neuen Zyklus setzen
            isReadyToStartCycle = false;
            isWaitingForDamage = true;
            hasCycleCompleted = false;
            animationJustStarted = true;
            cycleStartTime = Time.time;
            fallbackCycleTimer = fallbackCycleDuration;

            // Animation starten
            if (completedCycles == 0)
            {
                // Erstes Mal: Erst KeepDigging, dann Trigger
                ai.animator.SetBool("KeepDigging", true);
                Debug.Log($"[{ai.name}] KeepDigging = TRUE gesetzt (erster Zyklus)");
                ai.StartCoroutine(DelayedFirstTrigger(ai));
            }
            else
            {
                // Folgezyklen: Trigger direkt setzen
                ai.animator.SetTrigger("DigTrigger");
                Debug.Log($"[{ai.name}] DigTrigger gesetzt für Zyklus #{completedCycles + 1}");
            }

            // Baseline für Cycle-Detection setzen
            if (completedCycles > 0)
            {
                lastNormalizedTime = GetCurrentNormalizedTime(ai);
            }
        }

        private System.Collections.IEnumerator DelayedFirstTrigger(UnitAI ai)
        {
            yield return new WaitForSeconds(0.05f);
            ai.animator.ResetTrigger("DigTrigger");
            ai.animator.SetTrigger("DigTrigger");
            Debug.Log($"[{ai.name}] DigTrigger gesetzt für ersten Zyklus");

            yield return new WaitForSeconds(0.05f);
            lastNormalizedTime = GetCurrentNormalizedTime(ai);
            Debug.Log($"[{ai.name}] Baseline NormalizedTime: {lastNormalizedTime:F2}");
        }

        private bool CheckForCompletedCycle(UnitAI ai)
        {
            // Fallback-Timer
            fallbackCycleTimer -= Time.deltaTime;
            if (fallbackCycleTimer <= 0f)
            {
                Debug.Log($"[{ai.name}] ⏰ FALLBACK-TIMER abgelaufen - Zyklus #{completedCycles + 1} als abgeschlossen betrachtet");
                return true;
            }

            float currentNormalizedTime = GetCurrentNormalizedTime(ai);

            // Minimale Wartezeit nach Start
            float waitTime = 0.15f;
            if (animationJustStarted && Time.time - cycleStartTime < waitTime)
            {
                return false;
            }
            animationJustStarted = false;

            // Zyklus-Abschluss-Detection
            bool cycleWrapped = currentNormalizedTime < lastNormalizedTime && lastNormalizedTime > 0.9f && currentNormalizedTime < 0.1f;
            bool cycleEnded = currentNormalizedTime >= 0.98f && !hasCycleCompleted;
            bool leftDigState = !IsCurrentlyInDigState(ai) && lastNormalizedTime > 0.2f && Time.time - cycleStartTime > 0.4f;

            // Debug nur alle 60 Frames
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"[{ai.name}] Zyklus #{completedCycles + 1} Check - NormTime: {currentNormalizedTime:F2}, " +
                         $"InDigState: {IsCurrentlyInDigState(ai)}, Elapsed: {Time.time - cycleStartTime:F2}s, " +
                         $"LastNorm: {lastNormalizedTime:F2}");
            }

            if (cycleWrapped || cycleEnded || leftDigState)
            {
                hasCycleCompleted = true;
                string reason = cycleWrapped ? "WRAP" : cycleEnded ? "END" : "STATE_EXIT";
                Debug.Log($"[{ai.name}] ✅ ZYKLUS #{completedCycles + 1} ABGESCHLOSSEN ({reason}) - " +
                         $"NormTime: {lastNormalizedTime:F2} → {currentNormalizedTime:F2}, " +
                         $"Dauer: {Time.time - cycleStartTime:F2}s");
                return true;
            }

            lastNormalizedTime = currentNormalizedTime;
            return false;
        }

        private bool ProcessCompletedCycle(UnitAI ai)
        {
            completedCycles++;
            Debug.Log($"[{ai.name}] 💥 VERARBEITE ZYKLUS #{completedCycles} - Health vorher: {tileData.Health}");

            bool destroyed = tileData.ReduceHealth();
            Debug.Log($"[{ai.name}] Health nach Schaden: {tileData.Health}, Zerstört: {destroyed}");

            if (destroyed)
            {
                Debug.Log($"[{ai.name}] 🎯 WAND ZERSTÖRT nach {completedCycles} Zyklen!");
                StopAllDigging(ai);
                DigManager.Instance.CompleteDig(tilePos);
                SpawnResources(ai);
                return true;
            }
            else
            {
                Debug.Log($"[{ai.name}] 🔄 TILE HAT NOCH {tileData.Health} HEALTH - bereite nächsten Zyklus vor");
                isWaitingForDamage = false;
                // MINIMALE ÄNDERUNG: Verkürzte Pause zwischen Zyklen
                StartCoroutine(ai, DelayedCycleRestart());
                return false;
            }
        }

        private System.Collections.IEnumerator DelayedCycleRestart()
        {
            yield return new WaitForSeconds(0.05f); // VERKÜRZT: 0.05s statt 0.1s
            isReadyToStartCycle = true;
            Debug.Log($"Nächster Grab-Zyklus bereit nach verkürzter Pause");
        }

        private System.Collections.IEnumerator DelayedDigStart()
        {
            yield return new WaitForSeconds(0.05f); // VERKÜRZT: 0.05s statt 0.1s
            isReadyToStartCycle = true;
            Debug.Log($"Grab-Zyklus bereit nach verkürzter Rotation");
        }

        private float GetCurrentNormalizedTime(UnitAI ai)
        {
            AnimatorStateInfo currentState = ai.animator.GetCurrentAnimatorStateInfo(0);
            return currentState.normalizedTime % 1f;
        }

        private bool IsCurrentlyInDigState(UnitAI ai)
        {
            AnimatorStateInfo stateInfo = ai.animator.GetCurrentAnimatorStateInfo(0);
            int digHash = Animator.StringToHash("dig");
            int attackHash = Animator.StringToHash("attack");
            int mineHash = Animator.StringToHash("mine");
            int workHash = Animator.StringToHash("work");

            return stateInfo.shortNameHash == digHash ||
                   stateInfo.shortNameHash == attackHash ||
                   stateInfo.shortNameHash == mineHash ||
                   stateInfo.shortNameHash == workHash;
        }

        private bool HandleFallbackDigging(UnitAI ai)
        {
            Debug.LogWarning($"[{ai.name}] Kein Animator - verwende Timer-basiertes Graben");

            fallbackCycleTimer -= Time.deltaTime;

            if (fallbackCycleTimer <= 0f)
            {
                fallbackCycleTimer = fallbackCycleDuration;
                completedCycles++;

                bool destroyed = tileData.ReduceHealth();
                Debug.Log($"[{ai.name}] Fallback-Schaden #{completedCycles}: Health = {tileData.Health}, Zerstört = {destroyed}");

                if (destroyed)
                {
                    DigManager.Instance.CompleteDig(tilePos);
                    SpawnResources(ai);
                    return true;
                }
            }

            return false;
        }

        private void StopAllDigging(UnitAI ai)
        {
            if (ai.animator != null)
            {
                ai.animator.SetBool("KeepDigging", false);
                for (int i = 0; i < 5; i++)
                {
                    ai.animator.ResetTrigger("DigTrigger");
                }
                Debug.Log($"[{ai.name}] Graben GESTOPPT - KeepDigging = FALSE, DigTrigger 5x RESET");
            }
        }

        private void SpawnResources(UnitAI ai)
        {
            if (tileData.OriginalState == TileState.Wall_Gold || tileData.OriginalState == TileState.Wall_JewelVein)
            {
                var prefab = tileData.OriginalState == TileState.Wall_Gold ? ai.groundGoldPrefab : ai.goldPilePrefab;
                if (prefab != null)
                {
                    var center = ai.tileController.tilemap.GetCellCenterWorld(tilePos);
                    Object.Instantiate(prefab, center, Quaternion.identity);
                    Debug.Log($"[{ai.name}] {tileData.OriginalState} Ressourcen gespawnt");
                }
            }
        }

        private void StartCoroutine(UnitAI ai, System.Collections.IEnumerator routine)
        {
            ai.StartCoroutine(routine);
        }

        public void OnExit(UnitAI ai)
        {
            Debug.Log($"[{ai.name}] DigTask.OnExit nach {completedCycles} Zyklen");

            StopAllDigging(ai);

            // NavMeshAgent korrekt zurücksetzen ohne Weiterbewegung
            var navAgent = ai.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navAgent != null)
            {
                if (navAgent.isStopped)
                {
                    // WICHTIG: Destination auf aktuelle Position setzen BEVOR Reaktivierung
                    navAgent.SetDestination(ai.transform.position);
                    navAgent.isStopped = false;

                    // Zusätzlich: Velocity auf 0 setzen um sofortigen Stopp zu gewährleisten
                    navAgent.velocity = Vector3.zero;

                    Debug.Log($"[{ai.name}] NavMeshAgent reaktiviert mit aktueller Position als Destination");
                }
            }

            if (slotReserved && tileData != null)
            {
                tileData.ReleaseSlot(slotDir);
                tileData.UnassignWorker(ai.gameObject);
                Debug.Log($"[{ai.name}] Dig-Slot und Worker-Zuordnung freigegeben");
            }

            ai.StopTaskRotation();

            // NEUE ZEILE: Sofortige Prüfung auf neue Tasks
            ai.CheckForNewTasksImmediately();
        }
    }
}