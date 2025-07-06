using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RoomPlacer : MonoBehaviour
{
    [Header("Scene References")]
    public Camera mainCamera;
    public Tilemap tilemap;
    public GameObject tileClickHandler;

    [Header("Room Tile Prefabs")]
    public TileBase treasuryTile;
    public TileBase lairTile;
    public TileBase trainingTile;

    [Header("Ghost Prefabs")]
    public GameObject ghostTreasuryPrefab;
    public GameObject ghostLairPrefab;
    public GameObject ghostTrainingPrefab;

    [Header("Effects")]
    public AudioClip placementSound;
    public ParticleSystem placementEffectPrefab;
    public AudioSource audioSource;

    [Header("Layer Configuration")]
    [Tooltip("LayerMask für die Tilemap‐Collider, damit Fliegen etc. ignoriert werden.")]
    public LayerMask tilemapLayerMask;

    private GameObject currentGhost;
    private TileState currentRoom = TileState.Floor_Dug;
    private bool isPlacing = false;
    private Vector3Int? lastTilePos = null;

    void Update()
    {
        if (!isPlacing) return;

        if (GetGridTileUnderMouse(out Vector3Int tilePos, out Vector3 worldPos))
        {
            if (lastTilePos != tilePos)
            {
                lastTilePos = tilePos;
                UpdateGhostPosition(worldPos);
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (DigManager.Instance.tileMap.TryGetValue(tilePos, out var tileData) &&
                    tileData.State == TileState.Floor_Dug)
                {
                    tileData.State = currentRoom;
                    tilemap.SetTile(tilePos, GetTileForRoom(currentRoom));
                    PlayPlacementFeedback(tilePos);

                    // Ghost bleibt erhalten – Update auf aktuelle Position
                    UpdateGhostPosition(worldPos);
                }
            }

            if (Input.GetMouseButtonDown(1))
            {
                CancelPlacement();
            }
        }
        else if (currentGhost)
        {
            currentGhost.SetActive(false);
        }
    }

    // UI Callbacks
    public void StartPlacement_Treasury() => StartPlacement(TileState.Room_Treasury, ghostTreasuryPrefab);
    public void StartPlacement_Lair() => StartPlacement(TileState.Room_Lair, ghostLairPrefab);
    public void StartPlacement_Training() => StartPlacement(TileState.Room_Training, ghostTrainingPrefab);

    void StartPlacement(TileState roomType, GameObject ghostPrefab)
    {
        CancelPlacement();

        currentRoom = roomType;
        currentGhost = Instantiate(ghostPrefab);
        SetLayerRecursively(currentGhost, LayerMask.NameToLayer("Ignore Raycast"));

        isPlacing = true;
        tileClickHandler.SetActive(false);
        GameModeManager.Instance.SetMode(GameMode.RoomPlacing);
    }

    void CancelPlacement()
    {
        if (currentGhost)
            Destroy(currentGhost);

        isPlacing = false;
        currentGhost = null;
        tileClickHandler.SetActive(true);
        GameModeManager.Instance.SetMode(GameMode.Default);
    }

    void UpdateGhostPosition(Vector3 worldPos)
    {
        if (!currentGhost) return;

        Vector3 cellWorldPos = tilemap.GetCellCenterWorld(tilemap.WorldToCell(worldPos));
        currentGhost.SetActive(true);
        currentGhost.transform.position = new Vector3(cellWorldPos.x, 0f, cellWorldPos.z);
    }

    void PlayPlacementFeedback(Vector3Int tilePos)
    {
        // Sound
        if (audioSource != null && placementSound != null)
            audioSource.PlayOneShot(placementSound);

        // Partikel
        if (placementEffectPrefab != null)
        {
            Vector3 world = tilemap.GetCellCenterWorld(tilePos);
            Instantiate(placementEffectPrefab, world, Quaternion.identity);
        }
    }

    TileBase GetTileForRoom(TileState state)
    {
        return state switch
        {
            TileState.Room_Treasury => treasuryTile,
            TileState.Room_Lair => lairTile,
            TileState.Room_Training => trainingTile,
            _ => null
        };
    }

    /// <summary>
    /// Projiziert den Maus‐Ray und greift nur Collider im tilemapLayerMask ab.
    /// Sortiert alle Treffer nach Entfernung und wählt den ersten, um worldPos zu bestimmen.
    /// </summary>
    bool GetGridTileUnderMouse(out Vector3Int tilePos, out Vector3 worldPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        // RaycastAll nur gegen die Tilemap‐Layer (Fliegen und andere Kollider liegen nicht auf diesem Layer)
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, tilemapLayerMask);

        if (hits.Length == 0)
        {
            tilePos = default;
            worldPos = default;
            return false;
        }

        // Nächstliegenden Treffer wählen
        RaycastHit nearest = hits.OrderBy(h => h.distance).First();
        worldPos = nearest.point;
        tilePos = tilemap.WorldToCell(worldPos);
        return true;
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
