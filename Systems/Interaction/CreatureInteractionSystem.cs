using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DK;
using UnityEngine.UIElements;
using System.Collections;
using UnityEngine.AI;

/// <summary>
/// Kreatur-Interaktionssystem das mit dem bestehenden Custom3DCursor arbeitet
/// </summary>
[RequireComponent(typeof(Custom3DCursor))]
public class CreatureInteractionSystem : MonoBehaviour
{
    [Header("Cursor Integration")]
    [Tooltip("Referenz zum Custom3DCursor (automatisch gefunden wenn auf gleichem GameObject)")]
    public Custom3DCursor cursor3D;

    [Header("Cursor States")]
    [Tooltip("Verschiedene Cursor-Prefabs für verschiedene States (Optional - kann auch ein Prefab mit verschiedenen Animationen sein)")]
    public GameObject defaultCursorPrefab;
    public GameObject hoverCursorPrefab;
    public GameObject grabCursorPrefab;
    public GameObject slapCursorPrefab;

    [Header("Single Prefab Mode")]
    [Tooltip("Nutze nur ein Prefab mit verschiedenen Animationen?")]
    public bool useSinglePrefab = true;

    [Tooltip("Animator Parameter Namen für States (wenn useSinglePrefab = true)")]
    public string animatorStateParameter = "CursorState";
    public string animatorTriggerSlap = "Slap";

    [Header("Cursor Animations")]
    [Tooltip("Animation-Clips für verschiedene States")]
    public AnimationClip idleAnimationClip;
    public AnimationClip hoverAnimationClip;
    public AnimationClip grabAnimationClip;
    public AnimationClip slapAnimationClip;

    [Header("Cursor Scale Settings")]
    [Tooltip("Skalierung beim Hovern")]
    public float hoverScale = 1.2f;

    [Tooltip("Skalierung beim Greifen")]
    public float grabScale = 0.8f;

    [Tooltip("Skalierung beim Schlagen")]
    public float slapScale = 1.5f;

    [Header("Interaction Settings")]
    [Tooltip("Maximale Anzahl Kreaturen die gleichzeitig gehalten werden können")]
    public int maxCreaturesHeld = 10;

    [Tooltip("Radius für Multi-Pickup")]
    public float multiPickupRadius = 2f;

    [Tooltip("Layer auf dem Kreaturen sind")]
    public LayerMask creatureLayer = -1;

    [Tooltip("Höhe über dem Boden wenn Kreaturen gehalten werden")]
    public float holdHeight = 3f;

    [Header("Detection Settings")]
    [Tooltip("Radius für Kreatur-Erkennung (Hover)")]
    public float detectionRadius = 1.5f;

    [Header("Screen Selection Settings")]
    [Tooltip("Maximaler Abstand in Bildschirm-Pixeln zum Selektieren")]
    public float screenSelectRadius = 50f;


    [Tooltip("Nutze Sphere-Cast statt Ray-Cast für bessere Erkennung")]
    public bool useSphereCast = true;

    [Header("Slap Settings")]
    [Tooltip("Geschwindigkeitsboost durch Schlagen")]
    public float slapSpeedBoost = 2f;

    [Tooltip("Dauer des Speed-Boosts in Sekunden")]
    public float slapBoostDuration = 10f;

    [Tooltip("Slap-Effekt Prefab")]
    public GameObject slapEffectPrefab;

    [Tooltip("Slap-Sound")]
    public AudioClip slapSound;

    [Header("Visual Feedback")]
    [Tooltip("Material für Highlight-Effekt")]
    public Material highlightMaterial;

    [Tooltip("Outline-Dicke beim Hovern")]
    public float outlineWidth = 0.1f;

    [Tooltip("Outline-Farbe")]
    public Color outlineColor = Color.yellow;

    [Header("UI")]
    [Tooltip("UI Text für gehaltene Kreaturen-Anzahl")]
    public Text heldCreaturesText;

    [Tooltip("Container für Kreatur-Icons")]
    public Transform creatureIconContainer;

    [Tooltip("Kreatur-Icon Prefab")]
    public GameObject creatureIconPrefab;

    [Header("Pickup Animation")]
    [Tooltip("Geschwindigkeit mit der die Hand zur Kreatur fliegt")]
    public float handFlySpeed = 15f;

    [Tooltip("Kurve für die Flugbewegung")]
    public AnimationCurve pickupCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Höhe der Flugkurve")]
    public float pickupArcHeight = 2f;

    [Tooltip("Dauer der Greif-Animation am Ziel")]
    public float grabDuration = 0.3f;
    [Header("Hand Slap Animation")]

    [Tooltip("Name des Animator-Trigger-Parameters für die Hand-Slap-Animation")]
    public string handSlapTriggerName = "HandSlap";



    // Private Variablen
    private Camera mainCamera;
    private CreatureInteractable hoveredCreature;
    private List<CreatureInteractable> heldCreatures = new List<CreatureInteractable>();
    private Dictionary<CreatureInteractable, Vector3> originalPositions = new Dictionary<CreatureInteractable, Vector3>();
    private AudioSource audioSource;
    private bool isHoldingCreatures = false;

    // Private Variablen für Pickup-Animation
    private bool isPerformingPickup = false;
    private Vector3 pickupStartPosition;
    private Queue<CreatureInteractable> pickupQueue = new Queue<CreatureInteractable>();

    // Cursor States
    private enum CursorState
    {
        Default,
        Hovering,
        Grabbing,
        Slapping
    }

    private CursorState currentCursorState = CursorState.Default;
    private GameObject currentCursorPrefab;

    void Awake()
    {
        mainCamera = Camera.main;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Custom3DCursor Integration
        if (cursor3D == null)
            cursor3D = GetComponent<Custom3DCursor>();

        if (cursor3D == null)
        {
            Debug.LogError("CreatureInteractionSystem benötigt Custom3DCursor auf dem gleichen GameObject!");
            return;
        }

        // Initial Cursor setzen
        SetCursorState(CursorState.Default);
    }

    private IEnumerator DoSlap(CreatureInteractable creature)
    {
        // 1) Cursor-Slap-Animation abspielen

        yield return StartCoroutine(SlapAnimation());

        // 2) Imp-Slap-Animation triggern
        creature.PlaySlapAnimation();

        // 3) ggf. Speed-Boost o.ä. – je nach Original:
        var agent = creature.GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.speed *= slapSpeedBoost;

        // 4) Nach kurzer Zeit wieder zum Hover/Idle zurückkehren
        yield return StartCoroutine(ResetFromSlapState());
    }


    void Update()
    {
        HandleHovering();
        HandleInput();
        UpdateHeldCreatures();
        UpdateUI();
    }

    /// <summary>
    /// Behandelt das Hovern über Kreaturen
    /// </summary>
    private void HandleHovering()
    {
        if (isHoldingCreatures || isPerformingPickup)
            return;

        // Mausposition im Screen-Space
        Vector2 mousePos = Input.mousePosition;
        CreatureInteractable closest = null;
        float minDistSq = screenSelectRadius * screenSelectRadius;

        // Alle aktiven Creatures im Szenen-Cache (oder über FindObjectsOfType)
        foreach (var ci in FindObjectsOfType<CreatureInteractable>())
        {
            // Nur solche, die interagierbar sind
            if (!ci.canBePickedUp && !ci.canBeSlapped)
                continue;

            Vector3 screenPoint = mainCamera.WorldToScreenPoint(ci.transform.position);
            // Hinter der Kamera ausblenden
            if (screenPoint.z < 0f)
                continue;

            Vector2 screen2D = new Vector2(screenPoint.x, screenPoint.y);
            float distSq = (screen2D - mousePos).sqrMagnitude;
            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                closest = ci;
            }
        }

        // Highlight-Wechsel nur bei Änderung
        if (closest != hoveredCreature)
        {
            if (hoveredCreature != null)
                hoveredCreature.SetHighlight(false);

            hoveredCreature = closest;

            if (hoveredCreature != null)
            {
                hoveredCreature.SetHighlight(true);
                SetCursorState(CursorState.Hovering);
            }
            else
            {
                SetCursorState(CursorState.Default);
            }
        }
    }



    /// <summary>
    /// Verarbeitet Eingaben
    /// </summary>
    private void HandleInput()
    {
        // Linksklick
        if (Input.GetMouseButtonDown(0))
        {
            if (isHoldingCreatures)
            {
                DropCreatures();
            }
            else if (hoveredCreature != null && hoveredCreature.canBePickedUp)
            {
                // Shift für Multi-Pickup
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    PickupMultipleCreatures();
                else
                    PickupCreature(hoveredCreature);
            }
        }

        // Rechtsklick für Slap
        if (Input.GetMouseButtonDown(1) && hoveredCreature != null && hoveredCreature.canBeSlapped && !isHoldingCreatures)
        {
            SlapCreature(hoveredCreature);
        }

        // Mittelklick: sofort fallenlassen
        if (Input.GetMouseButtonDown(2) && isHoldingCreatures)
        {
            DropAllCreaturesImmediately();
        }
    }


    /// <summary>
    /// Ändert den Cursor-State und wechselt Prefab/Animation
    /// </summary>
    void SetCursorState(CursorState newState)
    {
        if (currentCursorState == newState) return;

        Debug.Log($"Cursor State Change: {currentCursorState} -> {newState}");
        currentCursorState = newState;

        if (useSinglePrefab)
        {
            // Nutze Animator States statt Prefab-Wechsel
            UpdateCursorAnimatorState(newState);
        }
        else
        {
            // Wechsle zwischen verschiedenen Prefabs
            UpdateCursorPrefab(newState);
        }

        // Update Cursor-Eigenschaften basierend auf State
        UpdateCursorProperties(newState);
    }

    /// <summary>
    /// Aktualisiert den Animator State des Cursors
    /// </summary>
    void UpdateCursorAnimatorState(CursorState state)
    {
        // Wir müssen auf das instanziierte Cursor-Objekt zugreifen
        // Da es private ist, nutzen wir Animation-Clips stattdessen

        switch (state)
        {
            case CursorState.Default:
                if (idleAnimationClip != null)
                    cursor3D.ChangeLoopAnimation(idleAnimationClip);
                break;

            case CursorState.Hovering:
                if (hoverAnimationClip != null)
                    cursor3D.ChangeLoopAnimation(hoverAnimationClip);
                break;

            case CursorState.Grabbing:
                if (grabAnimationClip != null)
                    cursor3D.ChangeLoopAnimation(grabAnimationClip);
                break;

            case CursorState.Slapping:
                if (slapAnimationClip != null)
                {
                    cursor3D.ChangeLoopAnimation(slapAnimationClip);
                    StartCoroutine(ResetFromSlapState());
                }
                break;
        }
    }

    /// <summary>
    /// Wechselt das Cursor-Prefab
    /// </summary>
    void UpdateCursorPrefab(CursorState state)
    {
        GameObject targetPrefab = null;

        switch (state)
        {
            case CursorState.Default:
                targetPrefab = defaultCursorPrefab;
                break;
            case CursorState.Hovering:
                targetPrefab = hoverCursorPrefab;
                break;
            case CursorState.Grabbing:
                targetPrefab = grabCursorPrefab;
                break;
            case CursorState.Slapping:
                targetPrefab = slapCursorPrefab;
                break;
        }

        ChangeCursorPrefab(targetPrefab);
    }

    /// <summary>
    /// Aktualisiert Cursor-Eigenschaften basierend auf State
    /// </summary>
    void UpdateCursorProperties(CursorState state)
    {
        switch (state)
        {
            case CursorState.Default:
                cursor3D.SetCursorScale(1f);
                // Hover-Animation komplett deaktiviert
                cursor3D.enableHoverAnimation = false;
                cursor3D.rotationSpeed = 90f;
                // Animation-Clip zurücksetzen
                if (idleAnimationClip != null)
                    cursor3D.ChangeLoopAnimation(idleAnimationClip);
                break;

            case CursorState.Hovering:
                cursor3D.SetCursorScale(hoverScale);
                // Hover-Animation komplett deaktiviert
                cursor3D.enableHoverAnimation = false;
                cursor3D.rotationSpeed = 120f;
                if (hoverAnimationClip != null)
                    cursor3D.ChangeLoopAnimation(hoverAnimationClip);
                break;

            case CursorState.Grabbing:
                cursor3D.SetCursorScale(grabScale);
                // Hover-Animation komplett deaktiviert
                cursor3D.enableHoverAnimation = false;
                cursor3D.rotationSpeed = 60f;
                if (grabAnimationClip != null)
                    cursor3D.ChangeLoopAnimation(grabAnimationClip);
                break;

            case CursorState.Slapping:
                cursor3D.SetCursorScale(slapScale);
                cursor3D.enableHoverAnimation = false;
                StartCoroutine(SlapAnimation());
                break;
        }
    }

    /// <summary>
    /// Spezielle Animation für Slap
    /// </summary>
    /// <summary>
    /// Slap-Animation: nur Scale-Puls, Rotation komplett aus
    /// </summary>
    private IEnumerator SlapAnimation()
    {
        // Rotation definitiv aus
        cursor3D.enableRotationAnimation = false;

        // Scale-Puls
        float time = 0f;
        float duration = 0.2f;
        float startScale = cursor3D.cursorScale;
        float targetScale = grabScale; // oder ein anderer Endwert, je nach Wunsch

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            float scale = Mathf.Lerp(slapScale, targetScale, Mathf.PingPong(t * 2f, 1f));
            cursor3D.SetCursorScale(scale);
            yield return null;
        }

        // Zurück auf Normal-Scale
        cursor3D.SetCursorScale(1f);
    }



    /// <summary>
    /// Wechselt das Cursor-Prefab nur wenn es sich geändert hat
    /// </summary>
    void ChangeCursorPrefab(GameObject newPrefab)
    {
        if (newPrefab != null && newPrefab != currentCursorPrefab)
        {
            currentCursorPrefab = newPrefab;
            cursor3D.ChangeCursorPrefab(newPrefab);
        }
    }

    /// <summary>
    /// Hebt eine einzelne Kreatur auf
    /// </summary>
    void PickupCreature(CreatureInteractable creature)
    {
        if (heldCreatures.Count >= maxCreaturesHeld)
        {
            Debug.Log($"Maximale Anzahl von {maxCreaturesHeld} Kreaturen bereits gehalten!");
            return;
        }

        if (!creature.CanBePickedUp())
            return;

        // Füge Kreatur zur Pickup-Queue hinzu
        pickupQueue.Enqueue(creature);

        // Starte Pickup-Animation wenn nicht bereits läuft
        if (!isPerformingPickup)
        {
            StartCoroutine(PerformPickupAnimation());
        }
    }

    /// <summary>
    /// Animiert die Hand zur Kreatur und zurück
    /// </summary>
    System.Collections.IEnumerator PerformPickupAnimation()
    {
        isPerformingPickup = true;

        // Unhighlight die Kreatur während Pickup
        if (hoveredCreature != null)
        {
            hoveredCreature.SetHighlight(false);
            hoveredCreature = null;
        }

        while (pickupQueue.Count > 0)
        {
            CreatureInteractable creature = pickupQueue.Dequeue();

            if (creature == null || !creature.CanBePickedUp())
                continue;

            // Speichere Start-Position
            pickupStartPosition = cursor3D.CursorWorldPosition;
            Vector3 targetPosition = creature.transform.position + Vector3.up * 0.5f;

            // Temporär Cursor-Movement deaktivieren
            float originalFollowSpeed = cursor3D.followSpeed;
            cursor3D.followSpeed = 0f;

            // Hand öffnen Animation
            SetCursorState(CursorState.Hovering);
            cursor3D.TriggerAnimation("HandOpen");

            // Fliege zur Kreatur
            yield return FlyToPosition(targetPosition, true);

            // Greif-Animation
            SetCursorState(CursorState.Grabbing);
            cursor3D.TriggerAnimation("HandGrab");

            // Warte kurz für Greif-Animation
            yield return new WaitForSeconds(grabDuration);

            // Jetzt tatsächlich die Kreatur aufnehmen
            ActuallyPickupCreature(creature);

            // Fliege zurück zur Original-Position
            yield return FlyToPosition(pickupStartPosition, false);

            // Cursor-Movement wieder aktivieren
            cursor3D.followSpeed = originalFollowSpeed;
        }

        isPerformingPickup = false;
        yield break;
    }

    /// <summary>
    /// Bewegt die Hand mit einer Kurve zu einer Position
    /// </summary>
    System.Collections.IEnumerator FlyToPosition(Vector3 targetPos, bool isPickingUp)
    {
        Vector3 startPos = cursor3D.CursorWorldPosition;
        float distance = Vector3.Distance(startPos, targetPos);
        float duration = distance / handFlySpeed;
        float elapsedTime = 0;

        // Speichere original Rotation
        Quaternion originalRotation = cursor3D.CursorGameObject.transform.rotation;

        // Berechne Kontrollpunkt für Bezier-Kurve
        Vector3 midPoint = (startPos + targetPos) / 2f;
        midPoint.y += pickupArcHeight;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = pickupCurve.Evaluate(elapsedTime / duration);

            // Quadratische Bezier-Kurve für smoothe Flugbahn
            Vector3 position = CalculateBezierPoint(t, startPos, midPoint, targetPos);

            // Setze Cursor-Position direkt
            if (cursor3D.CursorGameObject != null)
            {
                cursor3D.CursorGameObject.transform.position = position;

                // KEINE Rotation - behalte original Rotation
                cursor3D.CursorGameObject.transform.rotation = originalRotation;
            }

            yield return null;
        }

        // Stelle sicher dass wir am Ziel sind
        if (cursor3D.CursorGameObject != null)
        {
            cursor3D.CursorGameObject.transform.position = targetPos;
            cursor3D.CursorGameObject.transform.rotation = originalRotation;
        }
    }

    /// <summary>
    /// Berechnet einen Punkt auf einer quadratischen Bezier-Kurve
    /// </summary>
    Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        return (u * u * p0) + (2 * u * t * p1) + (t * t * p2);
    }

    /// <summary>
    /// Führt das tatsächliche Aufnehmen der Kreatur durch
    /// </summary>
    void ActuallyPickupCreature(CreatureInteractable creature)
    {
        // Kreatur zur Liste hinzufügen
        heldCreatures.Add(creature);
        originalPositions[creature] = creature.transform.position;

        // Kreatur-Status ändern
        creature.OnPickedUp();

        // Kreatur bleibt sichtbar aber wird child der Hand
        if (cursor3D.CursorGameObject != null)
        {
            creature.transform.SetParent(cursor3D.CursorGameObject.transform);
            creature.transform.localPosition = new Vector3(0, -0.5f, 0); // Unter der Hand
            creature.transform.localRotation = Quaternion.identity;
            creature.transform.localScale = Vector3.one;
        }

        // Flags setzen
        isHoldingCreatures = true;

        // Sound abspielen
        PlayPickupSound();

        // UI-Icon hinzufügen
        AddCreatureIcon(creature);

        Debug.Log($"Kreatur aufgehoben: {creature.name}. Halte jetzt {heldCreatures.Count} Kreaturen.");
    }

    /// <summary>
    /// Hebt mehrere Kreaturen in der Nähe auf
    /// </summary>
    void PickupMultipleCreatures()
    {
        Vector3 pickupCenter = hoveredCreature.transform.position;

        // Visueller Effekt für Multi-Pickup Radius
        ShowMultiPickupEffect(pickupCenter);

        // Finde alle Kreaturen im Radius
        Collider[] nearbyColliders = Physics.OverlapSphere(pickupCenter, multiPickupRadius, creatureLayer);

        // Sortiere nach Distanz für sequentielles Aufnehmen
        List<CreatureInteractable> creaturesInRange = new List<CreatureInteractable>();

        foreach (var collider in nearbyColliders)
        {
            if (heldCreatures.Count + pickupQueue.Count >= maxCreaturesHeld)
                break;

            var creature = collider.GetComponent<CreatureInteractable>();
            if (creature != null && creature.CanBePickedUp() && !heldCreatures.Contains(creature))
            {
                creaturesInRange.Add(creature);
            }
        }

        // Sortiere nach Distanz zum Cursor
        creaturesInRange.Sort((a, b) =>
            Vector3.Distance(cursor3D.CursorWorldPosition, a.transform.position)
            .CompareTo(Vector3.Distance(cursor3D.CursorWorldPosition, b.transform.position))
        );

        // Füge alle zur Queue hinzu
        foreach (var creature in creaturesInRange)
        {
            if (heldCreatures.Count + pickupQueue.Count < maxCreaturesHeld)
            {
                pickupQueue.Enqueue(creature);
            }
        }

        // Starte Pickup-Animation wenn nicht bereits läuft
        if (!isPerformingPickup && pickupQueue.Count > 0)
        {
            StartCoroutine(PerformPickupAnimation());
        }
    }

    /// <summary>
    /// Lässt alle Kreaturen sanft fallen
    /// </summary>
    void DropCreatures()
    {
        if (heldCreatures.Count == 0)
            return;

        StartCoroutine(PerformDropAnimation());
    }

    /// <summary>
    /// Animiert das Ablegen der Kreaturen
    /// </summary>
    System.Collections.IEnumerator PerformDropAnimation()
    {
        // Hole aktuelle Cursor-Position (in der Luft)
        Vector3 dropPositionInAir = cursor3D.CursorWorldPosition;

        // Berechne Boden-Position unter dem Cursor
        Vector3 groundPosition = dropPositionInAir;
        Ray downRay = new Ray(dropPositionInAir + Vector3.up * 10f, Vector3.down);
        if (Physics.Raycast(downRay, out RaycastHit hit, 20f, cursor3D.tilemapLayerMask))
        {
            groundPosition = hit.point;
        }
        else
        {
            groundPosition.y = 0; // Fallback auf Y=0
        }

        // Hand öffnen Animation
        SetCursorState(CursorState.Hovering);
        cursor3D.TriggerAnimation("HandOpen");

        yield return new WaitForSeconds(0.2f);

        // Lasse alle Kreaturen von der Handhöhe fallen
        foreach (var creature in heldCreatures)
        {
            if (creature != null)
            {
                // Parent entfernen
                creature.transform.SetParent(null);

                // Position auf Handhöhe setzen (nicht auf Boden)
                creature.transform.position = dropPositionInAir;
                creature.transform.localScale = Vector3.one;

                // Kreatur mitteilen dass sie fallen soll (zur groundPosition)
                creature.OnDropped(groundPosition);

                // Kleiner horizontaler Versatz für jede Kreatur
                dropPositionInAir += new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));
                groundPosition += new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));
            }
        }

        // Listen leeren
        heldCreatures.Clear();
        originalPositions.Clear();

        // Flags zurücksetzen
        isHoldingCreatures = false;

        // WICHTIG: State basierend auf aktuellem Hover-Status setzen
        SetCursorState(hoveredCreature != null ? CursorState.Hovering : CursorState.Default);

        // UI zurücksetzen
        ClearCreatureIcons();

        // Sound abspielen
        PlayDropSound();

        Debug.Log("Alle Kreaturen abgelegt.");
    }

    /// <summary>
    /// Lässt alle Kreaturen sofort fallen (Notfall)
    /// </summary>
    void DropAllCreaturesImmediately()
    {
        // Hole aktuelle Cursor-Position (in der Luft)
        Vector3 dropPositionInAir = cursor3D.CursorWorldPosition;

        foreach (var creature in heldCreatures)
        {
            if (creature != null)
            {
                creature.transform.SetParent(null);
                creature.transform.position = dropPositionInAir; // Setze auf Handhöhe
                creature.transform.localScale = Vector3.one;
                creature.OnDroppedImmediately(); // Kreatur fällt mit Physik

                // Kleiner Versatz für nächste Kreatur
                dropPositionInAir += new Vector3(Random.Range(-0.3f, 0.3f), 0, Random.Range(-0.3f, 0.3f));
            }
        }

        heldCreatures.Clear();
        originalPositions.Clear();
        isHoldingCreatures = false;

        // WICHTIG: Prüfe ob wir noch über einer Kreatur sind
        hoveredCreature = null; // Reset hover
        SetCursorState(CursorState.Default);

        ClearCreatureIcons();
    }

    /// <summary>
    /// Schlägt eine Kreatur für Speed-Boost
    /// </summary>
    void SlapCreature(CreatureInteractable creature)
    {
        if (!creature.canBeSlapped)
            return;

        // 1) Hand-Slap-Animation triggern
        var handAnim = cursor3D.CursorAnimator;
        if (handAnim != null && !string.IsNullOrEmpty(handSlapTriggerName))
            handAnim.SetTrigger(handSlapTriggerName);

        // 2) Kreatur-Slap-Logik
        creature.OnSlapped(slapSpeedBoost, slapBoostDuration);
        creature.PlaySlapAnimation();

        // 3) Effekte & Sound
        if (slapEffectPrefab != null)
            Instantiate(slapEffectPrefab, creature.transform.position, Quaternion.identity);
        if (slapSound != null)
            audioSource.PlayOneShot(slapSound);

        // 4) Cursor kurz wackeln
        StartCoroutine(ShakeCursor());

        // 5) Speed-Boost & Reset-Flow
        StartCoroutine(DoSlap(creature));

        Debug.Log($"Kreatur geschlagen: {creature.name}");
    }


    /// <summary>
    /// Reset nach Slap-Animation
    /// </summary>
    System.Collections.IEnumerator ResetFromSlapState()
    {
        yield return new WaitForSeconds(0.3f);

        if (currentCursorState == CursorState.Slapping)
        {
            SetCursorState(hoveredCreature != null ? CursorState.Hovering : CursorState.Default);
        }
    }

    /// <summary>
    /// Schüttelt den Cursor beim Schlagen
    /// </summary>
    /// <summary>
    /// Cursor-Wackeln: ohne jegliche Rotation
    /// </summary>
    private IEnumerator ShakeCursor()
    {
        // Optionales Wackeln beibehalten, aber keine Rotation setzen
        yield return new WaitForSeconds(0.2f);
        // Hier keine Änderung an enableRotationAnimation oder rotationSpeed
    }

    /// <summary>
    /// Aktualisiert die Positionen der gehaltenen Kreaturen
    /// </summary>
    void UpdateHeldCreatures()
    {
        // Während Pickup-Animation nichts tun
        if (isPerformingPickup)
            return;

        if (!isHoldingCreatures || heldCreatures.Count == 0)
            return;

        // Arrangiere multiple Kreaturen in einem Kreis unter der Hand
        if (heldCreatures.Count > 1)
        {
            float angleStep = 360f / heldCreatures.Count;
            float radius = 0.3f * Mathf.Min(1f, heldCreatures.Count * 0.3f);

            for (int i = 0; i < heldCreatures.Count; i++)
            {
                if (heldCreatures[i] == null) continue;

                float angle = i * angleStep * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;

                heldCreatures[i].transform.localPosition = new Vector3(x, -0.5f, z);
            }
        }
    }

    /// <summary>
    /// Holt die aktuelle Cursor-Position in der Welt
    /// </summary>
    Vector3 GetCursorWorldPosition()
    {
        // Nutze Raycast um die Maus-Position in der Welt zu bekommen
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        // Erst auf Kreaturen prüfen
        if (Physics.Raycast(ray, out RaycastHit creatureHit, Mathf.Infinity, creatureLayer))
        {
            return creatureHit.point + Vector3.up * cursor3D.cursorHeight;
        }

        // Dann auf Boden/Tilemap
        if (Physics.Raycast(ray, out RaycastHit groundHit, Mathf.Infinity, cursor3D.tilemapLayerMask))
        {
            return groundHit.point + Vector3.up * cursor3D.cursorHeight;
        }

        // Fallback
        return ray.origin + ray.direction * 10f;
    }

    /// <summary>
    /// Zeigt einen visuellen Effekt für Multi-Pickup
    /// </summary>
    void ShowMultiPickupEffect(Vector3 center)
    {
        // Hier könnte ein Kreis-Effekt oder ähnliches angezeigt werden
        Debug.DrawRay(center, Vector3.up * multiPickupRadius, Color.green, 1f);
    }

    /// <summary>
    /// Aktualisiert die UI
    /// </summary>
    void UpdateUI()
    {
        if (heldCreaturesText != null)
        {
            if (isHoldingCreatures)
            {
                heldCreaturesText.text = $"Gehaltene Kreaturen: {heldCreatures.Count}/{maxCreaturesHeld}";
                heldCreaturesText.gameObject.SetActive(true);
            }
            else
            {
                heldCreaturesText.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Fügt ein Icon für eine gehaltene Kreatur hinzu
    /// </summary>
    void AddCreatureIcon(CreatureInteractable creature)
    {
        if (creatureIconPrefab != null && creatureIconContainer != null)
        {
            var icon = Instantiate(creatureIconPrefab, creatureIconContainer);
            // Icon könnte das Kreatur-Portrait anzeigen
        }
    }

    /// <summary>
    /// Entfernt alle Kreatur-Icons
    /// </summary>
    void ClearCreatureIcons()
    {
        if (creatureIconContainer != null)
        {
            foreach (Transform child in creatureIconContainer)
            {
                Destroy(child.gameObject);
            }
        }
    }

    void PlayPickupSound()
    {
        // Implementiere Pickup-Sound
    }

    void PlayDropSound()
    {
        // Implementiere Drop-Sound
    }

    void OnDisable()
    {
        // Cleanup beim Deaktivieren
        if (hoveredCreature != null)
        {
            hoveredCreature.SetHighlight(false);
            hoveredCreature = null;
        }

        // Alle gehaltenen Kreaturen fallen lassen
        if (isHoldingCreatures)
        {
            DropAllCreaturesImmediately();
        }

        // Cursor State zurücksetzen
        SetCursorState(CursorState.Default);
    }

    void OnDrawGizmosSelected()
    {
        // Zeige Detection Radius
        if (Application.isPlaying && mainCamera != null)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit groundHit, Mathf.Infinity, ~creatureLayer))
            {
                Gizmos.color = new Color(1, 1, 0, 0.3f);
                Gizmos.DrawWireSphere(groundHit.point, detectionRadius);
            }
        }

        // Zeige Multi-Pickup Radius
        if (hoveredCreature != null)
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(hoveredCreature.transform.position, multiPickupRadius);
        }

        // Zeige gehaltene Kreaturen
        if (isHoldingCreatures && cursor3D != null)
        {
            Gizmos.color = Color.yellow;
            foreach (var creature in heldCreatures)
            {
                if (creature != null)
                {
                    Gizmos.DrawLine(GetCursorWorldPosition(), creature.transform.position);
                }
            }
        }
    }
}