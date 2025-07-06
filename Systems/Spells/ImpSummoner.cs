using System.Collections;
using UnityEngine;

public class ImpSummoner : MonoBehaviour
{
    [Header("Prefabs und Einstellungen")]
    [Tooltip("Prefab für den Imp (Worker)")]
    public GameObject impPrefab;

    [Tooltip("Partikel- oder Licht-VFX, das kurz gezeigt wird, bevor der Imp erscheint")]
    public GameObject spellVFXPrefab;

    [Tooltip("LayerMask, die nur die Boden-Objekte (RuleTile-Floors) enthält")]
    public LayerMask floorLayerMask;

    [Tooltip("Zeit in Sekunden, die das VFX laufen soll, bevor der Imp wirklich spawnt")]
    public float delayBeforeSpawn = 0.8f;

    [Tooltip("Anzahl der Imps pro Klick")]
    public int numberToSpawn = 1;

    [Tooltip("Versatz zwischen mehreren Imps, falls numberToSpawn > 1")]
    public Vector3 spawnOffset = new Vector3(1.2f, 0f, 0f);

    [Tooltip("Maximale Distanz des Raycasts (z.B. Höhe der Kamera zur Bodenebene)")]
    public float raycastMaxDistance = 100f;

    // Interner Zustand: true, solange der Spieler im "Imp-Beschwörungsmodus" ist.
    private bool isSummonMode = false;

    private Camera mainCamera;

    private void Awake()
    {
        // Hole die Hauptkamera (falls nicht im Inspector zugewiesen)
        if (Camera.main != null)
        {
            mainCamera = Camera.main;
        }
        else
        {
            Debug.LogError("ImpSummoner: Keine Kamera mit Tag 'MainCamera' gefunden.");
        }
    }

    private void Update()
    {
        // Wenn wir gerade im Summon-Modus sind, reagieren wir auf Linksklick und Rechtsklick
        if (isSummonMode)
        {
            // Linksklick: versuche zu beschwören
            if (Input.GetMouseButtonDown(0))
            {
                TrySummonAtCursor();
            }

            // Rechtsklick: Summon-Modus abbrechen
            if (Input.GetMouseButtonDown(1))
            {
                CancelSummonMode();
            }
        }
    }

    /// <summary>
    /// Öffentliche Methode, um über UI-Button in den Summon-Modus zu wechseln.
    /// </summary>
    public void EnterSummonMode()
    {
        isSummonMode = true;
        // Optional: Hier könntest du das Aussehen des Cursors ändern oder eine UI-Einblendung starten
        Debug.Log("Imp-Beschwörungsmodus aktiviert. Linksklick = beschwören, Rechtsklick = abbrechen.");
    }

    /// <summary>
    /// Bricht den Summon-Modus ab (z.B. wenn Rechtsklick).
    /// </summary>
    private void CancelSummonMode()
    {
        isSummonMode = false;
        // Optional: Cursor-Rücksetzung oder UI-Hinweis
        Debug.Log("Imp-Beschwörungsmodus abgebrochen.");
    }

    /// <summary>
    /// Schießt einen Ray von der Kamera zur Mausposition.
    /// Wenn er ein Boden-Objekt (floorLayerMask) trifft, startet die Spawn-Coroutine.
    /// </summary>
    private void TrySummonAtCursor()
    {
        // 1) Ray von der Kamera in Richtung Mauszeiger
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hitInfo;

        // 2) Strecke prüfen: Nur Layer "Floor" beachten
        if (Physics.Raycast(ray, out hitInfo, raycastMaxDistance, floorLayerMask))
        {
            Vector3 spawnPosition = hitInfo.point;
            StartCoroutine(CastSpawnSpellAtPosition(spawnPosition));
            // Optional: Modus nach erstem Spawn beenden
            // isSummonMode = false;
        }
        else
        {
            // Kein Boden getroffen – kein Imp. (Optional: Feedback)
            Debug.Log("Kein Boden getroffen. Imp kann nur auf begehbarem Boden erscheinen.");
        }
    }

    /// <summary>
    /// Coroutine: Erzeugt das VFX an der gewünschten Position, wartet kurz und spawnt dann die Imps.
    /// </summary>
    private IEnumerator CastSpawnSpellAtPosition(Vector3 position)
    {
        // 1) Erzeuge das Spell-VFX am Zielpunkt
        if (spellVFXPrefab != null)
        {
            GameObject vfxInstance = Instantiate(spellVFXPrefab, position, Quaternion.identity);

            // Wenn das VFX ein Partikelsystem hat, zerstöre es automatisch nach Ende
            ParticleSystem ps = vfxInstance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                float totalDuration = ps.main.duration + ps.main.startLifetime.constantMax;
                Destroy(vfxInstance, totalDuration);
            }
            else
            {
                Destroy(vfxInstance, delayBeforeSpawn + 0.5f);
            }
        }

        // 2) Warte, bis das VFX sichtbar war
        yield return new WaitForSeconds(delayBeforeSpawn);

        // 3) Spawne die Imps – bei mehreren mit kleinem Versatz
        for (int i = 0; i < numberToSpawn; i++)
        {
            Vector3 offset = spawnOffset * i;
            Instantiate(impPrefab, position + offset, Quaternion.identity);
        }
    }

    /// <summary>
    /// Öffentliche Methode, falls du über einen UI-Button (OnClick) beschwören möchtest.
    /// Ruft EnterSummonMode() auf.
    /// </summary>
    public void OnSummonButtonPressed()
    {
        EnterSummonMode();
    }
}
