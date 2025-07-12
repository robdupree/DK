using UnityEngine;
using System.Collections;

/// <summary>
/// Visueller Effekt-Controller für Slap-Aktionen
/// </summary>
public class CreatureSlapEffect : MonoBehaviour
{
    [Header("Effect Settings")]
    [Tooltip("Sterne die um die Kreatur kreisen")]
    public GameObject starPrefab;

    [Tooltip("Anzahl der Sterne")]
    public int starCount = 5;

    [Tooltip("Radius der Sternbahn")]
    public float orbitRadius = 1f;

    [Tooltip("Rotationsgeschwindigkeit")]
    public float rotationSpeed = 360f;

    [Tooltip("Auf- und Ab-Bewegung")]
    public float bobAmount = 0.3f;

    [Tooltip("Bob-Geschwindigkeit")]
    public float bobSpeed = 2f;

    [Header("Impact Effects")]
    [Tooltip("Staub-Partikel beim Aufprall")]
    public ParticleSystem dustParticles;

    [Tooltip("Schockwellen-Effekt")]
    public GameObject shockwavePrefab;

    [Header("Speed Lines")]
    [Tooltip("Speed-Linien die der Kreatur folgen")]
    public GameObject speedLinesPrefab;

    [Tooltip("Dauer der Speed-Linien")]
    public float speedLinesDuration = 10f;

    // Private Variablen
    private Transform target;
    private GameObject[] stars;
    private float startTime;
    private GameObject speedLinesInstance;

    void Start()
    {
        startTime = Time.time;

        // Sterne erstellen
        if (starPrefab != null)
        {
            CreateStars();
        }

        // Staub-Effekt
        if (dustParticles != null)
        {
            dustParticles.Play();
        }

        // Schockwelle
        if (shockwavePrefab != null)
        {
            var shockwave = Instantiate(shockwavePrefab, transform.position, Quaternion.identity);
            Destroy(shockwave, 2f);
        }
    }

    /// <summary>
    /// Setzt das Ziel für den Effekt
    /// </summary>
    public void SetTarget(Transform creature, float duration)
    {
        target = creature;

        // Speed-Linien anhängen
        if (speedLinesPrefab != null && target != null)
        {
            speedLinesInstance = Instantiate(speedLinesPrefab, target.position, Quaternion.identity);
            speedLinesInstance.transform.SetParent(target);
            Destroy(speedLinesInstance, speedLinesDuration);
        }

        // Selbstzerstörung nach Dauer
        Destroy(gameObject, duration);
    }

    void CreateStars()
    {
        stars = new GameObject[starCount];

        for (int i = 0; i < starCount; i++)
        {
            stars[i] = Instantiate(starPrefab, transform);

            // Initiale Position
            float angle = (360f / starCount) * i;
            UpdateStarPosition(stars[i].transform, angle, 0);

            // Zufällige Rotation für Variation
            stars[i].transform.localRotation = Quaternion.Euler(
                Random.Range(0, 360),
                Random.Range(0, 360),
                Random.Range(0, 360)
            );
        }
    }

    void Update()
    {
        if (target != null)
        {
            // Effekt folgt dem Ziel
            transform.position = target.position + Vector3.up * 1.5f;
        }

        // Sterne animieren
        if (stars != null)
        {
            float elapsed = Time.time - startTime;

            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] != null)
                {
                    float angle = (360f / starCount) * i + (elapsed * rotationSpeed);
                    float bob = Mathf.Sin(elapsed * bobSpeed + i) * bobAmount;

                    UpdateStarPosition(stars[i].transform, angle, bob);

                    // Sterne rotieren lassen
                    stars[i].transform.Rotate(Vector3.up * 180f * Time.deltaTime);
                }
            }
        }
    }

    void UpdateStarPosition(Transform star, float angle, float bobOffset)
    {
        float rad = angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(
            Mathf.Cos(rad) * orbitRadius,
            bobOffset,
            Mathf.Sin(rad) * orbitRadius
        );

        star.localPosition = offset;
    }

    void OnDestroy()
    {
        // Aufräumen
        if (speedLinesInstance != null)
        {
            Destroy(speedLinesInstance);
        }
    }
}

/// <summary>
/// Speed-Boost Visualisierung für geschlagene Kreaturen
/// </summary>
public class CreatureSpeedBoostVisual : MonoBehaviour
{
    [Header("Trail Settings")]
    [Tooltip("Trail Renderer für Geschwindigkeits-Effekt")]
    public TrailRenderer speedTrail;

    [Tooltip("Farbe des Trails bei normalem Speed")]
    public Color normalTrailColor = new Color(1, 1, 1, 0.3f);

    [Tooltip("Farbe des Trails bei Speed-Boost")]
    public Color boostTrailColor = new Color(1, 0.5f, 0, 0.8f);

    [Header("Particle Settings")]
    [Tooltip("Partikel die bei Bewegung entstehen")]
    public ParticleSystem movementParticles;

    [Tooltip("Emission-Rate bei normalem Speed")]
    public float normalEmissionRate = 5f;

    [Tooltip("Emission-Rate bei Speed-Boost")]
    public float boostEmissionRate = 20f;

    [Header("Glow Effect")]
    [Tooltip("Glow-Material für Speed-Boost")]
    public Material glowMaterial;

    [Tooltip("Normale Materialien")]
    public Material[] normalMaterials;

    private Renderer[] renderers;
    private bool isBoosted = false;
    private ParticleSystem.EmissionModule emissionModule;

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();

        if (movementParticles != null)
        {
            emissionModule = movementParticles.emission;
        }

        // Trail initial konfigurieren
        if (speedTrail != null)
        {
            speedTrail.startColor = normalTrailColor;
            speedTrail.endColor = new Color(normalTrailColor.r, normalTrailColor.g, normalTrailColor.b, 0);
        }
    }

    /// <summary>
    /// Aktiviert den Speed-Boost Effekt
    /// </summary>
    public void EnableSpeedBoost()
    {
        if (isBoosted) return;
        isBoosted = true;

        // Trail anpassen
        if (speedTrail != null)
        {
            speedTrail.startColor = boostTrailColor;
            speedTrail.endColor = new Color(boostTrailColor.r, boostTrailColor.g, boostTrailColor.b, 0);
            speedTrail.time = 0.5f; // Längerer Trail
            speedTrail.startWidth = 0.5f; // Breiterer Trail
        }

        // Partikel verstärken
        if (movementParticles != null)
        {
            emissionModule.rateOverTime = boostEmissionRate;

            var main = movementParticles.main;
            main.startSpeed = 5f;
            main.startLifetime = 1f;
        }

        // Glow-Effekt
        if (glowMaterial != null && renderers.Length > 0)
        {
            foreach (var renderer in renderers)
            {
                var mats = renderer.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = glowMaterial;
                }
                renderer.materials = mats;
            }
        }
    }

    /// <summary>
    /// Deaktiviert den Speed-Boost Effekt
    /// </summary>
    public void DisableSpeedBoost()
    {
        if (!isBoosted) return;
        isBoosted = false;

        // Trail zurücksetzen
        if (speedTrail != null)
        {
            speedTrail.startColor = normalTrailColor;
            speedTrail.endColor = new Color(normalTrailColor.r, normalTrailColor.g, normalTrailColor.b, 0);
            speedTrail.time = 0.2f;
            speedTrail.startWidth = 0.2f;
        }

        // Partikel normalisieren
        if (movementParticles != null)
        {
            emissionModule.rateOverTime = normalEmissionRate;

            var main = movementParticles.main;
            main.startSpeed = 2f;
            main.startLifetime = 0.5f;
        }

        // Normale Materialien wiederherstellen
        if (normalMaterials != null && normalMaterials.Length > 0 && renderers.Length > 0)
        {
            foreach (var renderer in renderers)
            {
                renderer.materials = normalMaterials;
            }
        }
    }
}