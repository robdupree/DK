using UnityEngine;

public class AdvancedCameraCursor : MonoBehaviour
{
    [Header("Cursor Prefab")]
    public GameObject cursorPrefab;

    [Header("References")]
    public Camera playerCamera;
    public LayerMask groundLayerMask = -1;

    [Header("Position Settings")]
    [Tooltip("Basis-Höhe über dem Boden")]
    public float baseHeight = 0.2f;

    [Tooltip("Zusätzlicher Offset in Kamera-Richtung")]
    [Range(-10f, 10f)]
    public float cameraDirectionOffset = 0f;

    [Tooltip("Horizontaler Offset (rechts/links relativ zur Kamera)")]
    [Range(-5f, 5f)]
    public float horizontalOffset = 0f;

    [Tooltip("Vertikaler Offset (zusätzlich zur baseHeight)")]
    [Range(-2f, 5f)]
    public float verticalOffset = 0f;

    [Header("Advanced Positioning")]
    [Tooltip("Verwende Kamera-relative Koordinaten?")]
    public bool useCameraRelativePositioning = true;

    [Tooltip("Kamera-Winkel kompensieren? (für isometrische Ansicht)")]
    public bool compensateCameraAngle = true;

    [Tooltip("Manuelle Winkel-Kompensation (falls automatik nicht funktioniert)")]
    [Range(0f, 90f)]
    public float manualAngleCompensation = 45f;

    [Header("Movement")]
    public float followSpeed = 20f;
    public bool useSmoothMovement = true;

    [Header("Input Controls")]
    [Tooltip("Taste zum Erhöhen des Camera Direction Offsets")]
    public KeyCode increaseOffsetKey = KeyCode.Plus;
    [Tooltip("Taste zum Verringern des Camera Direction Offsets")]
    public KeyCode decreaseOffsetKey = KeyCode.Minus;
    [Tooltip("Schrittweite für Tastatur-Kontrolle")]
    public float offsetStep = 0.1f;

    private GameObject cursorInstance;
    private Vector3 groundPosition;
    private Vector3 targetPosition;
    private Vector3 velocity;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        CreateCursor();
        Cursor.visible = false;
    }

    void CreateCursor()
    {
        if (cursorPrefab != null)
        {
            cursorInstance = Instantiate(cursorPrefab);
            cursorInstance.name = "Advanced Camera Cursor";

            // Collider deaktivieren
            Collider[] colliders = cursorInstance.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
                col.enabled = false;
        }
    }

    void Update()
    {
        HandleInput();
        UpdateCursorPosition();
    }

    void HandleInput()
    {
        // Keyboard-Kontrolle für Camera Direction Offset
        if (Input.GetKeyDown(increaseOffsetKey))
        {
            cameraDirectionOffset += offsetStep;
            cameraDirectionOffset = Mathf.Clamp(cameraDirectionOffset, -10f, 10f);
            Debug.Log($"Camera Direction Offset: {cameraDirectionOffset:F2}");
        }

        if (Input.GetKeyDown(decreaseOffsetKey))
        {
            cameraDirectionOffset -= offsetStep;
            cameraDirectionOffset = Mathf.Clamp(cameraDirectionOffset, -10f, 10f);
            Debug.Log($"Camera Direction Offset: {cameraDirectionOffset:F2}");
        }
    }

    void UpdateCursorPosition()
    {
        if (cursorInstance == null || playerCamera == null) return;

        // 1. Finde Boden-Position unter der Maus
        if (GetGroundPositionUnderMouse(out Vector3 groundPos))
        {
            groundPosition = groundPos;

            // 2. Berechne finale Cursor-Position mit allen Offsets
            Vector3 finalPosition = CalculateFinalCursorPosition(groundPos);

            // 3. Bewege Cursor zur finalen Position
            MoveCursorToPosition(finalPosition);

            // Cursor sichtbar machen
            if (!cursorInstance.activeSelf)
                cursorInstance.SetActive(true);
        }
        else
        {
            // Cursor verstecken wenn kein Boden getroffen
            if (cursorInstance.activeSelf)
                cursorInstance.SetActive(false);
        }
    }

    Vector3 CalculateFinalCursorPosition(Vector3 groundPos)
    {
        Vector3 finalPos = groundPos;

        // Basis-Höhe hinzufügen
        finalPos.y += baseHeight;

        // Vertikaler Offset
        finalPos.y += verticalOffset;

        if (useCameraRelativePositioning)
        {
            // Kamera-Richtungs-Offset
            if (cameraDirectionOffset != 0f)
            {
                Vector3 cameraDirection = GetCameraDirectionToGround(groundPos);
                finalPos += cameraDirection * cameraDirectionOffset;
            }

            // Horizontaler Offset (relativ zur Kamera)
            if (horizontalOffset != 0f)
            {
                Vector3 cameraRight = GetCameraRightDirection();
                finalPos += cameraRight * horizontalOffset;
            }
        }
        else
        {
            // Absolute Offsets
            finalPos += Vector3.forward * cameraDirectionOffset;
            finalPos += Vector3.right * horizontalOffset;
        }

        return finalPos;
    }

    Vector3 GetCameraDirectionToGround(Vector3 groundPos)
    {
        Vector3 cameraToGround = (playerCamera.transform.position - groundPos).normalized;

        if (compensateCameraAngle)
        {
            // Automatische Winkel-Kompensation basierend auf Kamera-Rotation
            float cameraAngle = playerCamera.transform.eulerAngles.x;
            if (cameraAngle > 180f) cameraAngle -= 360f; // Normalisiere auf -180 bis 180

            // Kompensiere den Winkel
            float compensationFactor = Mathf.Abs(cameraAngle) / 90f;
            cameraToGround.y *= compensationFactor;
        }
        else
        {
            // Manuelle Winkel-Kompensation
            float radians = manualAngleCompensation * Mathf.Deg2Rad;
            float compensationFactor = Mathf.Sin(radians);
            cameraToGround.y *= compensationFactor;
        }

        return cameraToGround.normalized;
    }

    Vector3 GetCameraRightDirection()
    {
        // Kamera-rechts-Richtung, projiziert auf horizontale Ebene
        Vector3 cameraRight = playerCamera.transform.right;
        cameraRight.y = 0; // Entferne Y-Komponente für horizontale Bewegung
        return cameraRight.normalized;
    }

    bool GetGroundPositionUnderMouse(out Vector3 groundPos)
    {
        groundPos = Vector3.zero;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundLayerMask))
        {
            groundPos = hit.point;
            return true;
        }

        return false;
    }

    void MoveCursorToPosition(Vector3 targetPos)
    {
        if (useSmoothMovement)
        {
            cursorInstance.transform.position = Vector3.SmoothDamp(
                cursorInstance.transform.position,
                targetPos,
                ref velocity,
                1f / followSpeed
            );
        }
        else
        {
            cursorInstance.transform.position = Vector3.Lerp(
                cursorInstance.transform.position,
                targetPos,
                followSpeed * Time.deltaTime
            );
        }
    }

    #region Public Methods

    public void SetCameraDirectionOffset(float offset)
    {
        cameraDirectionOffset = Mathf.Clamp(offset, -10f, 10f);
    }

    public void SetHorizontalOffset(float offset)
    {
        horizontalOffset = Mathf.Clamp(offset, -5f, 5f);
    }

    public void SetVerticalOffset(float offset)
    {
        verticalOffset = Mathf.Clamp(offset, -2f, 5f);
    }

    public void ResetAllOffsets()
    {
        cameraDirectionOffset = 0f;
        horizontalOffset = 0f;
        verticalOffset = 0f;
    }

    #endregion

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || cursorInstance == null) return;

        // Boden-Position (grün)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundPosition, 0.15f);

        // Basis-Position mit baseHeight (gelb)
        Vector3 basePos = groundPosition + Vector3.up * baseHeight;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(basePos, 0.12f);

        // Finale Cursor-Position (cyan)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(cursorInstance.transform.position, 0.1f);

        // Verbindungslinien
        Gizmos.color = Color.white;
        Gizmos.DrawLine(groundPosition, basePos);
        Gizmos.DrawLine(basePos, cursorInstance.transform.position);

        // Kamera-Richtungs-Vektor
        if (cameraDirectionOffset != 0f && playerCamera != null)
        {
            Vector3 cameraDir = GetCameraDirectionToGround(groundPosition);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(groundPosition, cameraDir * 2f);
        }

        // Horizontaler Offset-Vektor
        if (horizontalOffset != 0f)
        {
            Vector3 rightDir = GetCameraRightDirection();
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(groundPosition, rightDir * 2f);
        }
    }

    void OnDestroy()
    {
        Cursor.visible = true;

        if (cursorInstance != null)
            DestroyImmediate(cursorInstance);
    }
}