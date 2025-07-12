using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;

public class Custom3DCursor : MonoBehaviour
{
    [Header("Cursor Prefab")]
    [Tooltip("Das 3D-Modell das als Cursor verwendet werden soll")]
    public GameObject cursorPrefab;

    [Header("Slap Animation")]
    [Tooltip("Einmaliger Slap-Clip für den Cursor")]
    public AnimationClip slapAnimationClip;

    [Tooltip("Trigger-Name im Animator (falls Animator statt Animation genutzt wird)")]
    public string slapTriggerName = "SlapTrigger";

    [Header("References")]
    [Tooltip("Das Tilemap des Dungeons")]
    public Tilemap dungeonTilemap;

    [Tooltip("Kamera (falls leer wird automatisch gesucht)")]
    public Camera playerCamera;

    [Header("Cursor Settings")]
    [Tooltip("Höhe des Cursors über dem Boden")]
    public float cursorHeight = 0.2f;

    [Tooltip("Zusätzlicher Offset in Kamera-Richtung (für Hand-Position)")]
    [Range(-5f, 5f)]
    public float cameraDirectionOffset = 0f;

    [Tooltip("Größe des Cursor-Modells")]
    public float cursorScale = 1f;

    [Tooltip("Rotation des Cursor-Modells")]
    public Vector3 cursorRotation = Vector3.zero;

    [Header("Movement Settings")]
    [Tooltip("Geschwindigkeit der Cursor-Bewegung")]
    [Range(1f, 50f)]
    public float followSpeed = 25f;

    [Tooltip("Minimale Distanz für Bewegung")]
    [Range(0.01f, 1f)]
    public float minMoveDistance = 0.05f;

    [Tooltip("Smooth Movement verwenden?")]
    public bool useSmoothMovement = true;

    [Tooltip("Snap zu Tile-Center?")]
    public bool snapToTileCenter = false;

    [Header("Animation Settings")]
    [Tooltip("Soll der Cursor animiert werden?")]
    public bool enableAnimation = true;

    [Tooltip("Hover-Animation (auf und ab)")]
    public bool enableHoverAnimation = true;
    [Range(0.1f, 3f)]
    public float hoverSpeed = 1.5f;
    [Range(0.01f, 0.5f)]
    public float hoverAmplitude = 0.1f;

    [Tooltip("Rotation-Animation")]
    public bool enableRotationAnimation = false;
    [Range(10f, 360f)]
    public float rotationSpeed = 90f;
    public Vector3 rotationAxis = Vector3.up;

    [Header("Loop Animation")]
    [Tooltip("Kontinuierliche Animation abspielen?")]
    public bool enableLoopAnimation = true;

    [Tooltip("Animation Clip für Loop (falls vorhanden)")]
    public AnimationClip loopAnimationClip;

    [Tooltip("Animations-Geschwindigkeit")]
    [Range(0.1f, 3f)]
    public float animationSpeed = 1f;

    [Tooltip("Animation automatisch beim Start beginnen?")]
    public bool autoStartAnimation = true;

    [Header("Raycast Settings")]
    [Tooltip("Layer für Raycast")]
    public LayerMask tilemapLayerMask = -1;

    [Tooltip("Fallback-Höhe wenn kein Boden getroffen")]
    public float fallbackHeight = 0f;

    [Header("Cursor Behavior")]
    [Tooltip("Cursor verstecken wenn außerhalb des Dungeons?")]
    public bool hideCursorOutsideDungeon = true;

    [Tooltip("Standard Mauszeiger verstecken?")]
    public bool hideSystemCursor = true;

    // Private Variablen
    private GameObject cursorObject;
    private Vector3 targetPosition;
    private Vector3 currentVelocity;
    private Vector3 basePosition;
    private float hoverOffset = 0f;
    private bool isOverValidArea = false;

    // Animation Variablen
    public Animator cursorAnimator;
    public Animation cursorAnimation;

    void Start()
    {
        InitializeReferences();
        CreateCursor();

        // System-Cursor verstecken
        if (hideSystemCursor)
        {
            Cursor.visible = false;
        }

        // Initiale Position
        UpdateTargetPosition();
        if (cursorObject != null)
        {
            cursorObject.transform.position = targetPosition;
        }
    }

    void InitializeReferences()
    {
        if (playerCamera == null)
            playerCamera = Camera.main ?? FindObjectOfType<Camera>();

        if (dungeonTilemap == null)
            dungeonTilemap = FindObjectOfType<Tilemap>();

        if (playerCamera == null)
            Debug.LogError("[Custom3DCursor] Keine Kamera gefunden!");
    }

    void CreateCursor()
    {
        if (cursorPrefab != null)
        {
            // Spawne das Cursor-Prefab
            cursorObject = Instantiate(cursorPrefab);
            cursorObject.name = "3D Cursor";

            Debug.Log($"[Custom3DCursor] 3D Cursor erstellt: {cursorPrefab.name}");
        }
        else
        {
            // Fallback: Erstelle einen Standard-Cursor
            CreateDefaultCursor();
        }

        // Konfiguriere Cursor
        if (cursorObject != null)
        {
            cursorObject.transform.localScale = Vector3.one * cursorScale;
            cursorObject.transform.rotation = Quaternion.Euler(cursorRotation);

            // Animation Setup
            SetupCursorAnimation();

            // Optional: Collider entfernen falls vorhanden
            Collider[] colliders = cursorObject.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false; // Deaktiviere statt zerstören für bessere Performance
            }
        }

        targetPosition = Vector3.zero;
    }

    void CreateDefaultCursor()
    {
        cursorObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cursorObject.name = "Default 3D Cursor";

        // Material für bessere Sichtbarkeit
        Material cursorMat = new Material(Shader.Find("Standard"));
        cursorMat.color = Color.cyan;
        cursorMat.EnableKeyword("_EMISSION");
        cursorMat.SetColor("_EmissionColor", Color.cyan * 0.3f);
        cursorMat.SetFloat("_Metallic", 0.7f);
        cursorMat.SetFloat("_Smoothness", 0.9f);

        cursorObject.GetComponent<Renderer>().material = cursorMat;

        // Collider entfernen
        DestroyImmediate(cursorObject.GetComponent<Collider>());

        Debug.Log("[Custom3DCursor] Standard 3D Cursor erstellt");
    }

    void SetupCursorAnimation()
    {
        if (!enableLoopAnimation || cursorObject == null) return;

        // Versuche Animator zu finden
        cursorAnimator = cursorObject.GetComponent<Animator>();
        if (cursorAnimator == null)
        {
            cursorAnimator = cursorObject.GetComponentInChildren<Animator>();
        }

        // Versuche Animation-Komponente zu finden
        cursorAnimation = cursorObject.GetComponent<Animation>();
        if (cursorAnimation == null)
        {
            cursorAnimation = cursorObject.GetComponentInChildren<Animation>();
        }

        // Setup basierend auf verfügbaren Komponenten
        if (cursorAnimator != null)
        {
            SetupAnimator();
        }
        else if (cursorAnimation != null)
        {
            SetupLegacyAnimation();
        }
        else if (loopAnimationClip != null)
        {
            CreateAnimationComponent();
        }
        else
        {
            Debug.Log("[Custom3DCursor] Keine Animation-Komponente gefunden und kein Clip gesetzt");
        }
    }

    void SetupAnimator()
    {
        if (cursorAnimator == null) return;

        // Animator Speed setzen
        cursorAnimator.speed = animationSpeed;

        // Auto-Start wenn gewünscht
        if (autoStartAnimation)
        {
            cursorAnimator.enabled = true;
            Debug.Log("[Custom3DCursor] Animator gestartet");
        }
    }

    void SetupLegacyAnimation()
    {
        if (cursorAnimation == null) return;

        // Wenn ein spezifischer Clip gesetzt ist, verwende diesen
        if (loopAnimationClip != null)
        {
            cursorAnimation.AddClip(loopAnimationClip, "CursorLoop");

            // Animation-State konfigurieren
            AnimationState state = cursorAnimation["CursorLoop"];
            if (state != null)
            {
                state.wrapMode = WrapMode.Loop;
                state.speed = animationSpeed;
            }

            // Auto-Start
            if (autoStartAnimation)
            {
                cursorAnimation.Play("CursorLoop");
                Debug.Log("[Custom3DCursor] Loop Animation gestartet");
            }
        }
        else
        {
            // Verwende die erste verfügbare Animation
            if (cursorAnimation.GetClipCount() > 0)
            {
                AnimationClip firstClip = null;
                foreach (AnimationState state in cursorAnimation)
                {
                    firstClip = state.clip;
                    state.wrapMode = WrapMode.Loop;
                    state.speed = animationSpeed;
                    break;
                }

                if (firstClip != null && autoStartAnimation)
                {
                    cursorAnimation.Play(firstClip.name);
                    Debug.Log($"[Custom3DCursor] Standard Animation gestartet: {firstClip.name}");
                }
            }
        }
    }

    void CreateAnimationComponent()
    {
        if (loopAnimationClip == null) return;

        // Erstelle Animation-Komponente falls noch nicht vorhanden
        cursorAnimation = cursorObject.AddComponent<Animation>();

        // Clip hinzufügen
        cursorAnimation.AddClip(loopAnimationClip, "CursorLoop");

        // Konfigurieren
        AnimationState state = cursorAnimation["CursorLoop"];
        if (state != null)
        {
            state.wrapMode = WrapMode.Loop;
            state.speed = animationSpeed;
        }

        // Auto-Start
        if (autoStartAnimation)
        {
            cursorAnimation.Play("CursorLoop");
            Debug.Log("[Custom3DCursor] Animation-Komponente erstellt und gestartet");
        }
    }

    void Update()
    {
        if (cursorObject == null) return;

        UpdateTargetPosition();
        MoveCursorToTarget();

        if (enableAnimation)
        {
            UpdateAnimations();
        }

        if (!enableRotationAnimation) // Nur rotieren wenn explizit aktiviert
        {
            transform.rotation = Quaternion.Euler(cursorRotation);
        }

        UpdateCursorVisibility();
        UpdateLoopAnimation();
    }



    void UpdateTargetPosition()
    {
        if (playerCamera == null) return;

        Vector3? worldPosition = snapToTileCenter ?
            GetTilemapPositionUnderMouse() :
            GetWorldPositionUnderMouse();

        isOverValidArea = worldPosition.HasValue;

        if (isOverValidArea)
        {
            Vector3 newTarget = worldPosition.Value + Vector3.up * cursorHeight;

            if (Vector3.Distance(targetPosition, newTarget) > minMoveDistance)
            {
                targetPosition = newTarget;
                basePosition = newTarget; // Für Hover-Animation
            }
        }
        else if (!hideCursorOutsideDungeon)
        {
            // Fallback: Cursor in der Luft an Mausposition
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            Vector3 fallbackPos = ray.origin + ray.direction * 10f;
            fallbackPos.y = fallbackHeight;

            if (Vector3.Distance(targetPosition, fallbackPos) > minMoveDistance)
            {
                targetPosition = fallbackPos;
                basePosition = fallbackPos;
            }
        }
    }

    void MoveCursorToTarget()
    {
        Vector3 currentPos = cursorObject.transform.position;
        Vector3 finalTarget = targetPosition;

        // Camera Direction Offset anwenden
        if (cameraDirectionOffset != 0f && playerCamera != null)
        {
            // Berechne die Richtung von der Cursor-Position zur Kamera
            Vector3 cursorToCamera = (playerCamera.transform.position - targetPosition).normalized;

            // Verschiebe den Cursor in Kamera-Richtung
            finalTarget += cursorToCamera * cameraDirectionOffset;
        }

        // Hover-Animation hinzufügen
        if (enableHoverAnimation && enableAnimation)
        {
            finalTarget.y += hoverOffset;
        }

        if (useSmoothMovement)
        {
            cursorObject.transform.position = Vector3.SmoothDamp(
                currentPos, finalTarget, ref currentVelocity, 1f / followSpeed);
        }
        else
        {
            cursorObject.transform.position = Vector3.Lerp(
                currentPos, finalTarget, followSpeed * Time.deltaTime);
        }
    }

    void UpdateAnimations()
    {
        // Hover-Animation (auf und ab)
        if (enableHoverAnimation)
        {
            hoverOffset = Mathf.Sin(Time.time * hoverSpeed * 5f) * hoverAmplitude;
        }

        // Rotation-Animation
        if (enableRotationAnimation && cursorObject != null)
        {
            Vector3 currentRotation = cursorObject.transform.rotation.eulerAngles;
            Vector3 rotationDelta = rotationAxis * rotationSpeed * Time.deltaTime;
            cursorObject.transform.rotation = Quaternion.Euler(currentRotation + rotationDelta);
        }
    }

    void UpdateCursorVisibility()
    {
        if (cursorObject == null) return;

        bool shouldShow = !hideCursorOutsideDungeon || isOverValidArea;

        if (cursorObject.activeSelf != shouldShow)
        {
            cursorObject.SetActive(shouldShow);
        }
    }

    void UpdateLoopAnimation()
    {
        if (!enableLoopAnimation)
        {
            StopLoopAnimation();
            return;
        }

        // Animator Speed aktualisieren
        if (cursorAnimator != null)
        {
            cursorAnimator.speed = animationSpeed;

            // Sicherstellen dass Animator läuft
            if (!cursorAnimator.enabled && autoStartAnimation)
            {
                cursorAnimator.enabled = true;
            }
        }

        // Legacy Animation Speed aktualisieren
        if (cursorAnimation != null && cursorAnimation.isPlaying)
        {
            foreach (AnimationState state in cursorAnimation)
            {
                state.speed = animationSpeed;
            }
        }
        else if (cursorAnimation != null && autoStartAnimation && !cursorAnimation.isPlaying)
        {
            // Restart Animation falls gestoppt
            if (loopAnimationClip != null)
            {
                cursorAnimation.Play("CursorLoop");
            }
            else if (cursorAnimation.GetClipCount() > 0)
            {
                cursorAnimation.Play();
            }
        }
    }

    Vector3? GetWorldPositionUnderMouse()
    {
        if (playerCamera == null) return null;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, tilemapLayerMask);

        if (hits.Length == 0) return null;

        RaycastHit closestHit = hits[0];
        for (int i = 1; i < hits.Length; i++)
        {
            if (hits[i].distance < closestHit.distance)
                closestHit = hits[i];
        }

        return closestHit.point;
    }

    Vector3? GetTilemapPositionUnderMouse()
    {
        Vector3? worldPos = GetWorldPositionUnderMouse();
        if (!worldPos.HasValue || dungeonTilemap == null) return worldPos;

        Vector3Int tilePos = dungeonTilemap.WorldToCell(worldPos.Value);
        return dungeonTilemap.GetCellCenterWorld(tilePos);
    }

    #region Public Methods

    /// <summary>
    /// Startet die Loop-Animation
    /// </summary>
    public void StartLoopAnimation()
    {
        enableLoopAnimation = true;

        if (cursorAnimator != null)
        {
            cursorAnimator.enabled = true;
        }
        else if (cursorAnimation != null)
        {
            if (loopAnimationClip != null)
            {
                cursorAnimation.Play("CursorLoop");
            }
            else if (cursorAnimation.GetClipCount() > 0)
            {
                cursorAnimation.Play();
            }
        }

        Debug.Log("[Custom3DCursor] Loop Animation gestartet");
    }

    /// <summary>
    /// Stoppt die Loop-Animation
    /// </summary>
    public void StopLoopAnimation()
    {
        if (cursorAnimator != null)
        {
            cursorAnimator.enabled = false;
        }

        if (cursorAnimation != null && cursorAnimation.isPlaying)
        {
            cursorAnimation.Stop();
        }
    }

    /// <summary>
    /// Ändert die Animations-Geschwindigkeit
    /// </summary>
    public void SetAnimationSpeed(float speed)
    {
        animationSpeed = Mathf.Clamp(speed, 0.1f, 3f);

        if (cursorAnimator != null)
        {
            cursorAnimator.speed = animationSpeed;
        }

        if (cursorAnimation != null)
        {
            foreach (AnimationState state in cursorAnimation)
            {
                state.speed = animationSpeed;
            }
        }
    }

    /// <summary>
    /// Wechselt zu einer neuen Loop-Animation
    /// </summary>
    public void ChangeLoopAnimation(AnimationClip newClip)
    {
        if (newClip == null) return;

        loopAnimationClip = newClip;

        // Stoppe aktuelle Animation
        StopLoopAnimation();

        // Setup neue Animation
        if (cursorAnimation != null)
        {
            cursorAnimation.RemoveClip("CursorLoop");
            cursorAnimation.AddClip(newClip, "CursorLoop");

            AnimationState state = cursorAnimation["CursorLoop"];
            if (state != null)
            {
                state.wrapMode = WrapMode.Loop;
                state.speed = animationSpeed;
            }
        }

        // Starte neue Animation
        if (enableLoopAnimation)
        {
            StartLoopAnimation();
        }
    }

    /// <summary>
    /// Ändert den Kamera-Richtungs-Offset zur Laufzeit
    /// </summary>
    public void SetCameraDirectionOffset(float offset)
    {
        cameraDirectionOffset = Mathf.Clamp(offset, -5f, 5f);
    }

    /// <summary>
    /// Ändert das Cursor-Prefab zur Laufzeit
    /// </summary>
    public void ChangeCursorPrefab(GameObject newPrefab)
    {
        if (cursorObject != null)
        {
            Vector3 currentPos = cursorObject.transform.position;
            DestroyImmediate(cursorObject);

            cursorPrefab = newPrefab;
            CreateCursor();

            if (cursorObject != null)
            {
                cursorObject.transform.position = currentPos;

                // Animation neu setup nach Prefab-Wechsel
                SetupCursorAnimation();
            }
        }
    }

    /// <summary>
    /// Cursor ein-/ausblenden
    /// </summary>
    public void SetCursorVisible(bool visible)
    {
        if (cursorObject != null)
        {
            cursorObject.SetActive(visible);
        }
    }

    /// <summary>
    /// Cursor-Größe ändern
    /// </summary>
    public void SetCursorScale(float scale)
    {
        cursorScale = scale;
        if (cursorObject != null)
        {
            cursorObject.transform.localScale = Vector3.one * scale;
        }
    }

    /// <summary>
    /// Animation ein-/ausschalten
    /// </summary>
    public void SetAnimationEnabled(bool enabled)
    {
        enableAnimation = enabled;

        if (!enabled)
        {
            hoverOffset = 0f;
            StopLoopAnimation();
        }
        else if (enableLoopAnimation)
        {
            StartLoopAnimation();
        }
    }

    /// <summary>
    /// Loop-Animation ein-/ausschalten
    /// </summary>
    public void SetLoopAnimationEnabled(bool enabled)
    {
        enableLoopAnimation = enabled;

        if (enabled)
        {
            StartLoopAnimation();
        }
        else
        {
            StopLoopAnimation();
        }
    }

    /// <summary>
    /// System-Cursor ein-/ausblenden
    /// </summary>
    public void SetSystemCursorVisible(bool visible)
    {
        Cursor.visible = visible;
        hideSystemCursor = !visible;
    }

    #endregion

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // Mouse Ray
        if (playerCamera != null)
        {
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(ray.origin, ray.direction * 25f);
        }

        // Target Position (Boden-Projektion)
        Gizmos.color = isOverValidArea ? Color.green : Color.red;
        Gizmos.DrawWireSphere(targetPosition, 0.2f);

        // Cursor Position mit Camera Offset
        if (cursorObject != null && playerCamera != null)
        {
            Vector3 offsetTarget = targetPosition;
            if (cameraDirectionOffset != 0f)
            {
                Vector3 cursorToCamera = (playerCamera.transform.position - targetPosition).normalized;
                offsetTarget += cursorToCamera * cameraDirectionOffset;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(offsetTarget, cursorScale * 0.3f);

            // Linie zwischen Boden-Position und Cursor-Position
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(targetPosition, offsetTarget);
        }

        // Hit Point
        Vector3? hitPoint = GetWorldPositionUnderMouse();
        if (hitPoint.HasValue)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(hitPoint.Value, Vector3.one * 0.1f);
        }

        // Cursor Position
        if (cursorObject != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(cursorObject.transform.position, cursorScale * 0.5f);

            // Camera Direction Vector
            if (playerCamera != null && cameraDirectionOffset != 0f)
            {
                Vector3 cursorToCamera = (playerCamera.transform.position - targetPosition).normalized;
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(targetPosition, cursorToCamera * 2f);
            }
        }
    }

    void OnDestroy()
    {
        // System-Cursor wieder einblenden
        if (hideSystemCursor)
        {
            Cursor.visible = true;
        }

        // Cursor-Objekt aufräumen
        if (cursorObject != null)
        {
            DestroyImmediate(cursorObject);
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        // System-Cursor Status bei Fokus-Wechsel korrekt setzen
        if (hideSystemCursor)
        {
            Cursor.visible = !hasFocus;
        }
    }
    // Füge diese Properties und Methoden am Ende deiner Custom3DCursor Klasse ein (vor der letzten schließenden Klammer })

    #region Public Properties für CreatureInteractionSystem

    /// <summary>
    /// Gibt die aktuelle Weltposition des Cursors zurück
    /// </summary>
    public Vector3 CursorWorldPosition => cursorObject != null ? cursorObject.transform.position : targetPosition;

    /// <summary>
    /// Gibt das Cursor GameObject zurück (für erweiterte Kontrolle)
    /// </summary>
    public GameObject CursorGameObject => cursorObject;

    /// <summary>
    /// Gibt den Animator des Cursors zurück (falls vorhanden)
    /// </summary>
    public Animator CursorAnimator => cursorObject?.GetComponentInChildren<Animator>();

    /// <summary>
    /// Gibt die Animation-Komponente zurück (Legacy)
    /// </summary>
    public Animation CursorAnimation => cursorAnimation;

    #endregion

    #region Animator Control Methods

    /// <summary>
    /// Setzt einen Integer-Parameter im Animator
    /// </summary>
    public void SetAnimatorParameter(string paramName, int value)
    {
        Animator anim = CursorAnimator;
        if (anim != null && !string.IsNullOrEmpty(paramName))
        {
            anim.SetInteger(paramName, value);
        }
    }

    /// <summary>
    /// Setzt einen Float-Parameter im Animator
    /// </summary>
    public void SetAnimatorFloat(string paramName, float value)
    {
        Animator anim = CursorAnimator;
        if (anim != null && !string.IsNullOrEmpty(paramName))
        {
            anim.SetFloat(paramName, value);
        }
    }

    /// <summary>
    /// Setzt einen Bool-Parameter im Animator
    /// </summary>
    public void SetAnimatorBool(string paramName, bool value)
    {
        Animator anim = CursorAnimator;
        if (anim != null && !string.IsNullOrEmpty(paramName))
        {
            anim.SetBool(paramName, value);
        }
    }

    /// <summary>
    /// Triggert eine Animation
    /// </summary>
    public void TriggerAnimation(string triggerName)
    {
        Animator anim = CursorAnimator;
        if (anim != null && !string.IsNullOrEmpty(triggerName))
        {
            anim.SetTrigger(triggerName);
        }
    }

    /// <summary>
    /// Spielt eine spezifische Animation ab (für Legacy Animation Component)
    /// </summary>
    public void PlayAnimation(string animationName, bool loop = false)
    {
        if (cursorAnimation != null && cursorAnimation.GetClip(animationName) != null)
        {
            if (loop)
            {
                cursorAnimation[animationName].wrapMode = WrapMode.Loop;
            }
            else
            {
                cursorAnimation[animationName].wrapMode = WrapMode.Once;
            }

            cursorAnimation.Play(animationName);
        }
    }

    #endregion

    #region State Management

    /// <summary>
    /// Setzt den visuellen State des Cursors (für CreatureInteractionSystem)
    /// </summary>
    public void SetCursorState(int stateIndex)
    {
        // Wenn Animator vorhanden, setze State
        SetAnimatorParameter("CursorState", stateIndex);

        // Optional: Trigger state-spezifische Effekte
        switch (stateIndex)
        {
            case 0: // Default
                SetCursorScale(1f);
                enableHoverAnimation = true;
                hoverAmplitude = 0.1f;
                break;
            case 1: // Hover
                SetCursorScale(1.2f);
                enableHoverAnimation = true;
                hoverAmplitude = 0.15f;
                break;
            case 2: // Grab
                SetCursorScale(0.8f);
                enableHoverAnimation = true;
                hoverAmplitude = 0.05f;
                break;
            case 3: // Slap
                    // 1) Scale
                SetCursorScale(1.5f);
                enableHoverAnimation = false;

                // 2a) Alte Animation-Komponente nutzen
                if (cursorAnimation != null && slapAnimationClip != null)
                {
                    cursorAnimation.Stop();
                    cursorAnimation.clip = slapAnimationClip;
                    cursorAnimation.wrapMode = WrapMode.Once;
                    cursorAnimation.Play();
                    // nach dem Clip in die Loop-Animation zurückwechseln
                    StartCoroutine(RestartLoopAfter(slapAnimationClip.length));
                }
                // 2b) Oder Animator-Component
                else if (cursorAnimator != null && !string.IsNullOrEmpty(slapTriggerName))
                {
                    cursorAnimator.SetTrigger(slapTriggerName);
                }
                break;
        }
    }

    private IEnumerator RestartLoopAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (enableHoverAnimation && cursorAnimation != null && loopAnimationClip != null)
        {
            cursorAnimation.clip = loopAnimationClip;
            cursorAnimation.wrapMode = WrapMode.Loop;
            cursorAnimation.Play();
        }
        else if (enableHoverAnimation && cursorAnimator != null)
        {
            // hier kannst du deine Idle-State wieder starten
            cursorAnimator.Play("Idle", 0, 0f);
        }
    }

    #endregion
}