using UnityEngine;
using UnityEngine.Tilemaps;

public class TileController : MonoBehaviour
{
    [Header("Basic Settings")]
    public Tilemap tilemap;
    public TileBase floorTile;

    [Header("Legacy Dig Effect (Simple)")]
    [Tooltip("Einfacher Partikel-Effekt (Legacy)")]
    public ParticleSystem digEffectPrefab;

    [Header("New Destruction Effect (Advanced)")]
    [Tooltip("Neuer detaillierter Zerstörungseffekt mit Debris")]
    public GameObject destructionEffectPrefab;

    [Tooltip("Fallback-Effekt falls destructionEffectPrefab nicht gesetzt")]
    public bool useAdvancedEffects = true;

    /// <summary>
    /// Ersetzt eine Wand mit einem Floor-Tile
    /// </summary>
    /// <param name="pos">Grid-Position des Tiles</param>
    public void ReplaceWallWithFloor(Vector3Int pos)
    {
        tilemap.SetTile(pos, floorTile);
        PlayDigEffect(pos);
    }

    /// <summary>
    /// Spielt den Grab-Effekt ab (Legacy-Methode für Rückwärtskompatibilität)
    /// </summary>
    /// <param name="pos">Grid-Position des Tiles</param>
    public void PlayDigEffect(Vector3Int pos)
    {
        Vector3 worldPos = tilemap.CellToWorld(pos) + Vector3.up * 0.5f;

        if (useAdvancedEffects && destructionEffectPrefab != null)
        {
            PlayAdvancedDestructionEffect(pos);
        }
        else if (digEffectPrefab != null)
        {
            // Legacy simple particle effect
            Instantiate(digEffectPrefab, worldPos, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("Kein Dig-Effekt konfiguriert! Setze entweder destructionEffectPrefab oder digEffectPrefab.");
        }
    }

    public void PlayAdvancedDestructionEffect(Vector3Int pos, TileState tileType = TileState.Wall_Intact)
    {
        if (destructionEffectPrefab == null)
        {
            Debug.LogWarning("destructionEffectPrefab nicht gesetzt!");
            return;
        }

        Vector3 worldPos = tilemap.CellToWorld(pos) + Vector3.up * 0.5f;

        GameObject effectInstance = Instantiate(destructionEffectPrefab, worldPos, Quaternion.identity);

        // Versuche zuerst TileDestructionEffect
        TileDestructionEffect destructionEffect = effectInstance.GetComponent<TileDestructionEffect>();
        if (destructionEffect != null)
        {
            destructionEffect.PlayDestructionEffect(worldPos, tileType);
            return;
        }

        // Fallback zu SimpleDestructionEffect
        SimpleDestructionEffect simpleEffect = effectInstance.GetComponent<SimpleDestructionEffect>();
        if (simpleEffect != null)
        {
            simpleEffect.PlaySimpleDestructionEffect(worldPos, tileType);
            return;
        }

        Debug.LogError("Destruction Prefab hat weder TileDestructionEffect noch SimpleDestructionEffect!");
    }
    /// <summary>
    /// Spielt den Zerstörungseffekt basierend auf DungeonTileData ab
    /// </summary>
    /// <param name="pos">Grid-Position des Tiles</param>
    /// <param name="tileData">Tile-Daten für kontextspezifische Effekte</param>
    public void PlayDestructionEffectForTileData(Vector3Int pos, DungeonTileData tileData)
    {
        TileState effectType = tileData?.OriginalState ?? TileState.Wall_Intact;
        PlayAdvancedDestructionEffect(pos, effectType);
    }

    /// <summary>
    /// Erstellt einen Test-Zerstörungseffekt an der angegebenen Position
    /// </summary>
    /// <param name="worldPos">Weltposition für den Test</param>
    [ContextMenu("Test Destruction Effect")]
    public void TestDestructionEffect()
    {
        Vector3 testPos = transform.position;
        PlayAdvancedDestructionEffect(Vector3Int.zero, TileState.Wall_Intact);
    }

    /// <summary>
    /// Konfiguriert die Effekt-Einstellungen zur Laufzeit
    /// </summary>
    /// <param name="useAdvanced">Soll der erweiterte Effekt verwendet werden?</param>
    public void SetEffectMode(bool useAdvanced)
    {
        useAdvancedEffects = useAdvanced;
        Debug.Log($"Effekt-Modus geändert zu: {(useAdvanced ? "Advanced" : "Legacy")}");
    }
}