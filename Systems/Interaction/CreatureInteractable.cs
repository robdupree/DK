using UnityEngine;
using UnityEngine.AI;
using DK;
using System.Collections.Generic;

/// <summary>
/// Komponente die eine Kreatur interagierbar macht
/// Wird auf alle Kreaturen (Imps, Gegner, etc.) angewendet
/// </summary>
[RequireComponent(typeof(Collider))]
public class CreatureInteractable : MonoBehaviour
{

    [Header("Slap Animation")]
    [Tooltip("Name des Animator-Trigger-Parameters für Slap")]
    public string slapTriggerName = "SlapTrigger";

    [Tooltip("Optional: Partikeleffekt beim Slap")]
    public GameObject slapEffectPrefab;

    [Header("Interaction Settings")]
    [Tooltip("Kann diese Kreatur aufgehoben werden?")]
    public bool canBePickedUp = true;

    [Tooltip("Kann diese Kreatur geschlagen werden?")]
    public bool canBeSlapped = true;

    [Tooltip("Ist die Kreatur aktuell interagierbar?")]
    public bool isInteractable = true;

    [Header("Visual Settings")]
    [Tooltip("Outline-Shader für Highlight")]
    public Shader outlineShader;

    [Tooltip("Farbe des Highlights")]
    public Color highlightColor = Color.yellow;

    [Tooltip("Dicke des Outlines")]
    public float outlineThickness = 0.05f;

    [Header("Slap Settings")]
    [Tooltip("Partikel-Effekt beim Schlagen")]
    public GameObject slapParticles;

    [Tooltip("Animation die beim Schlagen abgespielt wird")]
    public string slapAnimationTrigger = "Slapped";

    // Private Komponenten
    private UnitAI unitAI;
    private NavMeshAgent navAgent;
    private Animator animator;
    private Renderer[] renderers;
    private Material outlineMaterial;
    private Rigidbody rb;

    // Status
    private bool isHighlighted = false;
    private bool isBeingHeld = false;
    private bool isSlapped = false;
    private float slapEndTime = 0f;
    private float originalSpeed = 0f;
    private float originalAnimSpeed = 1f;

    // Original-Material-Speicher
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

    void Awake()
    {
        // Komponenten holen
        unitAI = GetComponent<UnitAI>();
        navAgent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        renderers = GetComponentsInChildren<Renderer>();
        rb = GetComponent<Rigidbody>();

        // Rigidbody konfigurieren falls nicht vorhanden
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
        }

        // Outline-Material erstellen
        if (outlineShader != null)
        {
            outlineMaterial = new Material(outlineShader);
            outlineMaterial.SetColor("_OutlineColor", highlightColor);
            outlineMaterial.SetFloat("_OutlineWidth", outlineThickness);
        }

        // Original-Materialien speichern
        foreach (var renderer in renderers)
        {
            originalMaterials[renderer] = renderer.materials;
        }

        // Original-Speed speichern
        if (navAgent != null)
        {
            originalSpeed = navAgent.speed;
        }

        // Collider als Trigger setzen wenn nötig
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = false; // Sollte solid sein für Raycast
        }
    }

    void Update()
    {
        // Slap-Boost beenden wenn Zeit abgelaufen
        if (isSlapped && Time.time > slapEndTime)
        {
            EndSlapBoost();
        }
    }

    /// <summary>
    /// Prüft ob die Kreatur interagiert werden kann
    /// </summary>
    public bool CanBeInteracted()
    {
        return isInteractable && !isBeingHeld;
    }

    /// <summary>
    /// Prüft ob die Kreatur aufgehoben werden kann
    /// </summary>
    public bool CanBePickedUp()
    {
        return canBePickedUp && isInteractable && !isBeingHeld;
    }

    /// <summary>
    /// Prüft ob die Kreatur geschlagen werden kann
    /// </summary>
    public bool CanBeSlapped()
    {
        return canBeSlapped && isInteractable && !isBeingHeld;
    }

    /// <summary>
    /// Aktiviert/Deaktiviert das Highlight
    /// </summary>
    public void SetHighlight(bool highlighted)
    {
        if (isHighlighted == highlighted)
            return;

        isHighlighted = highlighted;

        if (highlighted)
        {
            // Outline hinzufügen
            AddOutline();

            // Optional: Kreatur stoppen
            if (unitAI != null)
            {
                unitAI.ClearTasks();
            }
            else if (navAgent != null)
            {
                navAgent.isStopped = true;
            }
        }
        else
        {
            // Outline entfernen
            RemoveOutline();

            // Optional: Kreatur wieder laufen lassen
            if (navAgent != null && !isBeingHeld)
            {
                navAgent.isStopped = false;
            }
        }
    }

    /// <summary>
    /// Wird aufgerufen wenn die Kreatur aufgehoben wird
    /// </summary>
    public void OnPickedUp()
    {
        isBeingHeld = true;

        // AI/Navigation deaktivieren
        if (unitAI != null)
        {
            unitAI.ClearTasks();
            unitAI.enabled = false;
        }

        if (navAgent != null)
        {
            navAgent.enabled = false;
        }

        // Physik deaktivieren
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // Kollisionen deaktivieren
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        // Animation
        if (animator != null)
        {
            animator.SetBool("IsHeld", true);
            animator.SetFloat("Speed", 0);
        }

        // Highlight entfernen
        SetHighlight(false);
    }

    /// <summary>
    /// Wird aufgerufen wenn die Kreatur abgelegt wird
    /// </summary>
    public void OnDropped(Vector3 dropPosition)
    {
        isBeingHeld = false;

        // NavMesh-Position finden
        if (NavMesh.SamplePosition(dropPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
        }
        else
        {
            transform.position = dropPosition;
        }

        // Komponenten wieder aktivieren
        ReenableComponents();

        // Animation
        if (animator != null)
        {
            animator.SetBool("IsHeld", false);
            animator.SetTrigger("Dropped");
        }
    }

    /// <summary>
    /// Coroutine für sanftes Fallen zum Boden
    /// </summary>
    System.Collections.IEnumerator FallToGround(Vector3 targetPosition)
    {
        float fallSpeed = 5f;
        float startY = transform.position.y;
        float targetY = targetPosition.y;

        while (transform.position.y > targetY + 0.1f)
        {
            Vector3 newPos = transform.position;
            newPos.y = Mathf.MoveTowards(newPos.y, targetY, fallSpeed * Time.deltaTime);
            transform.position = newPos;

            // Beschleunigung
            fallSpeed += 9.81f * Time.deltaTime;

            yield return null;
        }

        // Finale Position
        transform.position = new Vector3(transform.position.x, targetY, transform.position.z);

        // Aufprall-Effekt
        if (animator != null)
        {
            animator.SetTrigger("Land");
        }
    }

    /// <summary>
    /// Sofortiges Fallenlassen (Notfall)
    /// </summary>
    public void OnDroppedImmediately()
    {
        isBeingHeld = false;

        // Komponenten sofort aktivieren
        ReenableComponents();

        // Physics-Impuls für realistisches Fallen
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
            StartCoroutine(ResetPhysicsAfterDrop());
        }
    }

    /// <summary>
    /// Reaktiviert alle Komponenten nach dem Ablegen
    /// </summary>
    void ReenableComponents()
    {
        // Kollision
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = true;
        }

        // Navigation
        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.isStopped = false;
        }

        // AI
        if (unitAI != null)
        {
            unitAI.enabled = true;
        }

        // Physik
        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    /// <summary>
    /// Wird aufgerufen wenn die Kreatur geschlagen wird
    /// </summary>
    public void OnSlapped(float speedBoost, float duration)
    {
        // Slap-Status setzen
        isSlapped = true;
        slapEndTime = Time.time + duration;

        // Speed erhöhen
        if (navAgent != null)
        {
            navAgent.speed = originalSpeed * speedBoost;
        }

        // Animations-Geschwindigkeit erhöhen
        if (animator != null)
        {
            originalAnimSpeed = animator.speed;
            animator.speed = speedBoost;
            animator.SetTrigger(slapAnimationTrigger);
        }

        // Partikel-Effekt
        if (slapParticles != null)
        {
            var particles = Instantiate(slapParticles, transform.position + Vector3.up, Quaternion.identity);
            particles.transform.SetParent(transform);
            Destroy(particles, duration);
        }

        // Optional: Kreatur-spezifische Reaktion
        SendMessage("OnSlappedCustom", SendMessageOptions.DontRequireReceiver);
    }

    /// <summary>
    /// Beendet den Slap-Boost
    /// </summary>
    void EndSlapBoost()
    {
        isSlapped = false;

        // Speed zurücksetzen
        if (navAgent != null)
        {
            navAgent.speed = originalSpeed;
        }

        // Animations-Geschwindigkeit zurücksetzen
        if (animator != null)
        {
            animator.speed = originalAnimSpeed;
        }
    }

    /// <summary>
    /// Aktualisiert die Position während die Kreatur gehalten wird
    /// </summary>
    public void UpdateHeldPosition(Vector3 targetPosition, float moveSpeed)
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, moveSpeed * Time.deltaTime);

        // Leichte Rotation für visuellen Effekt
        transform.Rotate(Vector3.up * 30f * Time.deltaTime);
    }

    /// <summary>
    /// Fügt Outline-Effekt hinzu
    /// </summary>
    void AddOutline()
    {
        if (outlineMaterial == null)
            return;

        foreach (var renderer in renderers)
        {
            var mats = new Material[renderer.materials.Length + 1];
            for (int i = 0; i < renderer.materials.Length; i++)
            {
                mats[i] = renderer.materials[i];
            }
            mats[mats.Length - 1] = outlineMaterial;
            renderer.materials = mats;
        }
    }

    /// <summary>
    /// Entfernt Outline-Effekt
    /// </summary>
    void RemoveOutline()
    {
        foreach (var renderer in renderers)
        {
            if (originalMaterials.ContainsKey(renderer))
            {
                renderer.materials = originalMaterials[renderer];
            }
        }
    }

    /// <summary>
    /// Setzt Physik nach Drop zurück
    /// </summary>
    System.Collections.IEnumerator ResetPhysicsAfterDrop()
    {
        yield return new WaitForSeconds(1f);
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
        }
    }

    void OnDestroy()
    {
        // Aufräumen
        if (outlineMaterial != null)
        {
            Destroy(outlineMaterial);
        }
    }

    /// <summary>
    /// Spielt Slap-Animation (und Effekt) auf der Kreatur ab.
    /// </summary>
    public void PlaySlapAnimation()
    {
        // Animator triggern
        if (animator != null && !string.IsNullOrEmpty(slapTriggerName))
            animator.SetTrigger(slapTriggerName);

        // Partikeleffekt instanziieren
        if (slapEffectPrefab != null)
            Instantiate(slapEffectPrefab, transform.position, Quaternion.identity);
    }


}