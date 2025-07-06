using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TileClickHandler : MonoBehaviour
{
    public Camera mainCam;
    public Tilemap tilemap;

    private bool isDragging = false;
    private bool deselectMode = false;
    private Vector3Int? lastModifiedTile = null;

    [Header("Konfiguration")]
    [Tooltip("Layer, auf dem die 3D-Tile-Volumen-Collider liegen.")]
    public LayerMask tileVolumeLayer;

    // Ein kleiner Offset, um 'hit.point' einen winzigen Schritt INSIDE des getroffenen Colliders zu verschieben.
    private const float epsilon = 0.01f;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3Int? clickedTile = GetTileUnderMouse();
            if (clickedTile.HasValue && DigManager.Instance.IsTileMarked(clickedTile.Value))
            {
                deselectMode = true;
            }
            else
            {
                deselectMode = false;
            }

            isDragging = true;
            TryModifyTileUnderMouse();
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            lastModifiedTile = null;
        }

        if (isDragging)
        {
            TryModifyTileUnderMouse();
        }
    }

    void TryModifyTileUnderMouse()
    {
        Vector3Int? gridPosOpt = GetTileUnderMouse();
        if (!gridPosOpt.HasValue) return;

        Vector3Int gridPos = gridPosOpt.Value;

        if (lastModifiedTile.HasValue && lastModifiedTile.Value == gridPos)
            return;

        bool isMarked = DigManager.Instance.IsTileMarked(gridPos);

        if (deselectMode && isMarked)
        {
            DigManager.Instance.ToggleMarking(gridPos);
            lastModifiedTile = gridPos;
        }
        else if (!deselectMode && !isMarked)
        {
            DigManager.Instance.ToggleMarking(gridPos);
            lastModifiedTile = gridPos;
        }
    }

    /// <summary>
    /// Führt einen RaycastAll durch, sortiert Treffer nach Distanz, sucht den ersten Treffer
    /// im Layer 'tileVolumeLayer' und verschiebt den Hit‐Punkt um epsilon entlang der Normalen,
    /// um sicher INSIDE des getroffenen Tile‐Volumes zu landen. Dann wird 'WorldToCell' aufgerufen.
    /// </summary>
    Vector3Int? GetTileUnderMouse()
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        // RaycastAll nur auf den Layer, der die Tile-Volumen enthält
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, tileVolumeLayer);

        if (hits.Length == 0)
            return null;

        // Den nächstliegenden Treffer (kleinste .distance) wählen
        RaycastHit hit = hits.OrderBy(h => h.distance).First();

        // Verschiebe den Weltpunkt leicht INSIDE des Volumens
        Vector3 insidePoint = hit.point + hit.normal * epsilon;

        // Jetzt in Tilemap-Zellkoordinaten umwandeln
        Vector3Int cellPos = tilemap.WorldToCell(insidePoint);
        return cellPos;
    }

    #region Debug-Gizmos
    private void OnDrawGizmosSelected()
    {
        // Hilfsmittel: Zeichne einen kleinen Würfel am InsidePoint jedes Hit, wenn Debugger aktiv ist
        Ray ray = mainCam != null ? mainCam.ScreenPointToRay(Input.mousePosition) : default;
        if (mainCam == null) return;

        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, tileVolumeLayer);
        if (hits.Length == 0) return;

        RaycastHit hit = hits.OrderBy(h => h.distance).First();
        Vector3 insidePoint = hit.point + hit.normal * epsilon;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(insidePoint, 0.05f);
    }
    #endregion
}
