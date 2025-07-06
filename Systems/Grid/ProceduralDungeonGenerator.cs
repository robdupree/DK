using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ProceduralDungeonGenerator : MonoBehaviour
{
    [Header("Tilemap Setup")]
    public Tilemap tilemap;
    public TileBase floorTile;
    public TileBase voidTile;

    [Header("Dungeon Parameters")]
    [Range(10, 100)]
    public int dungeonWidth = 50;
    [Range(10, 100)]
    public int dungeonHeight = 50;

    [Header("Room Generation")]
    [Range(5, 20)]
    public int minRoomCount = 8;
    [Range(10, 30)]
    public int maxRoomCount = 15;
    [Range(4, 12)]
    public int minRoomSize = 6;
    [Range(8, 20)]
    public int maxRoomSize = 12;
    [Range(1, 5)]
    public int minRoomBuffer = 1;
    [Range(2, 8)]
    public int maxRoomBuffer = 3;

    [Header("Corridor Settings")]
    [Range(1, 3)]
    public int corridorWidth = 2;

    [Header("Dungeon Heart")]
    public GameObject dungeonHeartPrefab;
    public bool generateHeartRoom = true;
    public Transform objectParent;

    [Header("Grid Settings")]
    public bool useXZYSwizzle = true;
    public float objectHeightOffset = 0.5f;

    [Header("Active Chunk Visualization")]
    public bool showActiveChunk = true;
    public Color activeChunkColor = Color.yellow;

    // Private variables
    private GameObject currentDungeonHeart;
    private TileType[,] dungeonGrid;
    private List<Room> rooms = new List<Room>();
    private System.Random random;

    // Chunk-based tracking
    private Vector2Int activeChunkPosition = Vector2Int.zero; // Position des aktiven Chunks
    private HashSet<Vector2Int> generatedChunks = new HashSet<Vector2Int>(); // Alle generierten Chunk-Positionen

    enum TileType { Void, Floor, HeartRoom }

    struct Room
    {
        public int x, y, width, height;
        public Vector2Int center;
        public bool isHeartRoom;

        public Room(int x, int y, int width, int height, bool isHeartRoom = false)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            this.center = new Vector2Int(x + width / 2, y + height / 2);
            this.isHeartRoom = isHeartRoom;
        }

        public bool Overlaps(Room other, int buffer)
        {
            return x < other.x + other.width + buffer &&
                   x + width + buffer > other.x &&
                   y < other.y + other.height + buffer &&
                   y + height + buffer > other.y;
        }
    }

    [ContextMenu("Generate New Dungeon")]
    public void GenerateDungeon()
    {
        Debug.Log("=== STARTING INITIAL DUNGEON GENERATION ===");

        random = new System.Random();

        // Reset chunk tracking
        activeChunkPosition = Vector2Int.zero;
        generatedChunks.Clear();
        generatedChunks.Add(activeChunkPosition);

        InitializeDungeon();
        GenerateRooms();
        ConnectRooms();
        ApplyTiles();
        SpawnHeart();

        Debug.Log($"=== INITIAL DUNGEON GENERATED AT CHUNK {activeChunkPosition} ===");
    }

    public void ExpandDungeon(string direction)
    {
        Debug.Log($"=== EXPANDING FROM ACTIVE CHUNK {activeChunkPosition} TO {direction.ToUpper()} ===");

        random = new System.Random();

        // Calculate new chunk position based on ACTIVE chunk
        Vector2Int newChunkPosition = GetNewChunkPosition(direction);

        // Check if chunk already exists
        if (generatedChunks.Contains(newChunkPosition))
        {
            Debug.LogWarning($"Chunk at {newChunkPosition} already exists! Skipping expansion.");
            return;
        }

        // Calculate world offset for the new chunk
        Vector2Int worldOffset = new Vector2Int(
            newChunkPosition.x * dungeonWidth,
            newChunkPosition.y * dungeonHeight
        );

        // Generate the new chunk
        FillChunkWithVoid(worldOffset);
        GenerateChunkRooms(worldOffset);
        ConnectChunkToExisting(worldOffset, newChunkPosition);

        // Update tracking - NEW CHUNK BECOMES ACTIVE!
        generatedChunks.Add(newChunkPosition);
        activeChunkPosition = newChunkPosition;

        Debug.Log($"=== CHUNK EXPANSION COMPLETED! NEW ACTIVE CHUNK: {activeChunkPosition} ===");
    }

    Vector2Int GetNewChunkPosition(string direction)
    {
        Vector2Int newPos = activeChunkPosition;

        switch (direction.ToLower())
        {
            case "left":
                newPos.x -= 1;
                break;
            case "right":
                newPos.x += 1;
                break;
            case "up":
                newPos.y += 1;
                break;
            case "down":
                newPos.y -= 1;
                break;
        }

        return newPos;
    }

    void FillChunkWithVoid(Vector2Int worldOffset)
    {
        // Fill the ENTIRE chunk area with void tiles
        BoundsInt chunkArea = new BoundsInt(worldOffset.x, worldOffset.y, 0, dungeonWidth, dungeonHeight, 1);
        TileBase[] voidArray = new TileBase[dungeonWidth * dungeonHeight];

        for (int i = 0; i < voidArray.Length; i++)
        {
            voidArray[i] = voidTile;
        }

        tilemap.SetTilesBlock(chunkArea, voidArray);
        Debug.Log($"Filled chunk at world offset {worldOffset} with void tiles");
    }

    void GenerateChunkRooms(Vector2Int worldOffset)
    {
        List<Room> newRooms = new List<Room>();
        int roomCount = random.Next(minRoomCount, maxRoomCount + 1);

        for (int attempt = 0; attempt < roomCount * 20 && newRooms.Count < roomCount; attempt++)
        {
            int w = random.Next(minRoomSize, maxRoomSize + 1);
            int h = random.Next(minRoomSize, maxRoomSize + 1);

            // Generate room within this chunk's boundaries
            int x = random.Next(1, dungeonWidth - w - 1) + worldOffset.x;
            int y = random.Next(1, dungeonHeight - h - 1) + worldOffset.y;

            Room newRoom = new Room(x, y, w, h, false);
            int buffer = random.Next(minRoomBuffer, maxRoomBuffer + 1);

            bool canPlace = true;

            // Check against ALL existing rooms
            foreach (Room existingRoom in rooms)
            {
                if (newRoom.Overlaps(existingRoom, buffer))
                {
                    canPlace = false;
                    break;
                }
            }

            // Check against other new rooms in this chunk
            foreach (Room otherNewRoom in newRooms)
            {
                if (newRoom.Overlaps(otherNewRoom, buffer))
                {
                    canPlace = false;
                    break;
                }
            }

            if (canPlace)
            {
                newRooms.Add(newRoom);

                // Apply room tiles immediately
                for (int rx = newRoom.x; rx < newRoom.x + newRoom.width; rx++)
                {
                    for (int ry = newRoom.y; ry < newRoom.y + newRoom.height; ry++)
                    {
                        Vector3Int position = new Vector3Int(rx, ry, 0);
                        tilemap.SetTile(position, floorTile);
                    }
                }
            }
        }

        // Add new rooms to main list
        rooms.AddRange(newRooms);
        Debug.Log($"Generated {newRooms.Count} rooms in new chunk");
    }

    void ConnectChunkToExisting(Vector2Int worldOffset, Vector2Int chunkPosition)
    {
        if (rooms.Count == 0) return;

        // Find rooms in the new chunk
        List<Room> newChunkRooms = new List<Room>();
        foreach (Room room in rooms)
        {
            if (IsRoomInChunk(room, worldOffset))
            {
                newChunkRooms.Add(room);
            }
        }

        if (newChunkRooms.Count == 0) return;

        // Find closest room in an adjacent existing chunk
        Room closestExistingRoom = new Room();
        Room closestNewRoom = new Room();
        float shortestDistance = float.MaxValue;
        bool foundConnection = false;

        // Check all adjacent chunk positions
        Vector2Int[] adjacentChunks = {
            chunkPosition + Vector2Int.left,
            chunkPosition + Vector2Int.right,
            chunkPosition + Vector2Int.up,
            chunkPosition + Vector2Int.down
        };

        foreach (Vector2Int adjacentChunk in adjacentChunks)
        {
            if (!generatedChunks.Contains(adjacentChunk)) continue;

            Vector2Int adjacentWorldOffset = new Vector2Int(
                adjacentChunk.x * dungeonWidth,
                adjacentChunk.y * dungeonHeight
            );

            // Find rooms in this adjacent chunk
            foreach (Room existingRoom in rooms)
            {
                if (!IsRoomInChunk(existingRoom, adjacentWorldOffset)) continue;

                // Find closest room in new chunk
                foreach (Room newRoom in newChunkRooms)
                {
                    float distance = Vector2.Distance(existingRoom.center, newRoom.center);
                    if (distance < shortestDistance)
                    {
                        shortestDistance = distance;
                        closestExistingRoom = existingRoom;
                        closestNewRoom = newRoom;
                        foundConnection = true;
                    }
                }
            }
        }

        // Create connection
        if (foundConnection)
        {
            ConnectTwoRoomsGlobal(closestExistingRoom, closestNewRoom);
        }

        // Connect rooms within the new chunk
        for (int i = 0; i < newChunkRooms.Count - 1; i++)
        {
            ConnectTwoRoomsGlobal(newChunkRooms[i], newChunkRooms[i + 1]);
        }
    }

    bool IsRoomInChunk(Room room, Vector2Int worldOffset)
    {
        return room.x >= worldOffset.x && room.x < worldOffset.x + dungeonWidth &&
               room.y >= worldOffset.y && room.y < worldOffset.y + dungeonHeight;
    }

    void ConnectTwoRoomsGlobal(Room roomA, Room roomB)
    {
        Vector2Int pointA = roomA.center;
        Vector2Int pointB = roomB.center;

        if (random.Next(0, 2) == 0)
        {
            CreateHorizontalCorridorGlobal(pointA.x, pointB.x, pointA.y);
            CreateVerticalCorridorGlobal(pointA.y, pointB.y, pointB.x);
        }
        else
        {
            CreateVerticalCorridorGlobal(pointA.y, pointB.y, pointA.x);
            CreateHorizontalCorridorGlobal(pointA.x, pointB.x, pointB.y);
        }
    }

    void CreateHorizontalCorridorGlobal(int x1, int x2, int y)
    {
        int startX = Mathf.Min(x1, x2);
        int endX = Mathf.Max(x1, x2);

        for (int x = startX; x <= endX; x++)
        {
            for (int w = 0; w < corridorWidth; w++)
            {
                int corridorY = y + w - corridorWidth / 2;
                Vector3Int position = new Vector3Int(x, corridorY, 0);
                tilemap.SetTile(position, floorTile);
            }
        }
    }

    void CreateVerticalCorridorGlobal(int y1, int y2, int x)
    {
        int startY = Mathf.Min(y1, y2);
        int endY = Mathf.Max(y1, y2);

        for (int y = startY; y <= endY; y++)
        {
            for (int w = 0; w < corridorWidth; w++)
            {
                int corridorX = x + w - corridorWidth / 2;
                Vector3Int position = new Vector3Int(corridorX, y, 0);
                tilemap.SetTile(position, floorTile);
            }
        }
    }

    void InitializeDungeon()
    {
        dungeonGrid = new TileType[dungeonWidth, dungeonHeight];
        rooms.Clear();

        for (int x = 0; x < dungeonWidth; x++)
            for (int y = 0; y < dungeonHeight; y++)
                dungeonGrid[x, y] = TileType.Void;
    }

    void GenerateRooms()
    {
        int roomCount = random.Next(minRoomCount, maxRoomCount + 1);

        if (generateHeartRoom)
            GenerateHeartRoom();

        for (int attempt = 0; attempt < roomCount * 20 && rooms.Count < roomCount; attempt++)
        {
            int w = random.Next(minRoomSize, maxRoomSize + 1);
            int h = random.Next(minRoomSize, maxRoomSize + 1);
            int x = random.Next(1, dungeonWidth - w - 1);
            int y = random.Next(1, dungeonHeight - h - 1);

            Room newRoom = new Room(x, y, w, h, false);
            int buffer = random.Next(minRoomBuffer, maxRoomBuffer + 1);

            bool canPlace = true;
            foreach (Room existingRoom in rooms)
            {
                if (newRoom.Overlaps(existingRoom, buffer))
                {
                    canPlace = false;
                    break;
                }
            }

            if (canPlace)
            {
                rooms.Add(newRoom);
                CarveRoom(newRoom);
            }
        }

        Debug.Log($"Generated {rooms.Count} rooms in initial chunk");
    }

    void GenerateHeartRoom()
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            int x = random.Next(dungeonWidth / 4, dungeonWidth * 3 / 4 - 5);
            int y = random.Next(dungeonHeight / 4, dungeonHeight * 3 / 4 - 5);

            Room heartRoom = new Room(x, y, 5, 5, true);

            bool canPlace = true;
            foreach (Room existingRoom in rooms)
            {
                if (heartRoom.Overlaps(existingRoom, 2))
                {
                    canPlace = false;
                    break;
                }
            }

            if (canPlace)
            {
                rooms.Insert(0, heartRoom);
                CarveRoom(heartRoom);
                break;
            }
        }
    }

    void CarveRoom(Room room)
    {
        TileType tileType = room.isHeartRoom ? TileType.HeartRoom : TileType.Floor;

        for (int x = room.x; x < room.x + room.width; x++)
            for (int y = room.y; y < room.y + room.height; y++)
                if (x >= 0 && x < dungeonWidth && y >= 0 && y < dungeonHeight)
                    dungeonGrid[x, y] = tileType;
    }

    void ConnectRooms()
    {
        for (int i = 0; i < rooms.Count - 1; i++)
            ConnectTwoRooms(rooms[i], rooms[i + 1]);

        if (rooms.Count > 2)
            ConnectTwoRooms(rooms[rooms.Count - 1], rooms[0]);
    }

    void ConnectTwoRooms(Room roomA, Room roomB)
    {
        Vector2Int pointA = roomA.center;
        Vector2Int pointB = roomB.center;

        if (random.Next(0, 2) == 0)
        {
            CreateHorizontalCorridor(pointA.x, pointB.x, pointA.y);
            CreateVerticalCorridor(pointA.y, pointB.y, pointB.x);
        }
        else
        {
            CreateVerticalCorridor(pointA.y, pointB.y, pointA.x);
            CreateHorizontalCorridor(pointA.x, pointB.x, pointB.y);
        }
    }

    void CreateHorizontalCorridor(int x1, int x2, int y)
    {
        int startX = Mathf.Min(x1, x2);
        int endX = Mathf.Max(x1, x2);

        for (int x = startX; x <= endX; x++)
            for (int w = 0; w < corridorWidth; w++)
            {
                int corridorY = y + w - corridorWidth / 2;
                if (corridorY >= 0 && corridorY < dungeonHeight && x >= 0 && x < dungeonWidth)
                    dungeonGrid[x, corridorY] = TileType.Floor;
            }
    }

    void CreateVerticalCorridor(int y1, int y2, int x)
    {
        int startY = Mathf.Min(y1, y2);
        int endY = Mathf.Max(y1, y2);

        for (int y = startY; y <= endY; y++)
            for (int w = 0; w < corridorWidth; w++)
            {
                int corridorX = x + w - corridorWidth / 2;
                if (corridorX >= 0 && corridorX < dungeonWidth && y >= 0 && y < dungeonHeight)
                    dungeonGrid[corridorX, y] = TileType.Floor;
            }
    }

    void ApplyTiles()
    {
        if (tilemap == null) return;

        BoundsInt area = new BoundsInt(0, 0, 0, dungeonWidth, dungeonHeight, 1);
        TileBase[] tileArray = new TileBase[dungeonWidth * dungeonHeight];

        for (int x = 0; x < dungeonWidth; x++)
        {
            for (int y = 0; y < dungeonHeight; y++)
            {
                TileBase tile = dungeonGrid[x, y] == TileType.Void ? voidTile : floorTile;
                tileArray[x + y * dungeonWidth] = tile;
            }
        }

        tilemap.SetTilesBlock(area, tileArray);
    }

    void SpawnHeart()
    {
        if (dungeonHeartPrefab == null || !generateHeartRoom) return;

        // Lösche altes Heart
        if (currentDungeonHeart != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(currentDungeonHeart);
#else
            Destroy(currentDungeonHeart);
#endif
        }

        // Finde Heart Room
        foreach (Room room in rooms)
        {
            if (room.isHeartRoom)
            {
                Vector3 pos = tilemap.CellToWorld(new Vector3Int(room.center.x, room.center.y, 0));

                if (useXZYSwizzle)
                    pos = new Vector3(pos.x + 0.5f, objectHeightOffset, pos.z + 0.5f);
                else
                    pos = new Vector3(pos.x + 0.5f, pos.y + objectHeightOffset, pos.z + 0.5f);

#if UNITY_EDITOR
                currentDungeonHeart = (GameObject)PrefabUtility.InstantiatePrefab(dungeonHeartPrefab, objectParent);
                currentDungeonHeart.transform.position = pos;
#else
                currentDungeonHeart = Instantiate(dungeonHeartPrefab, pos, Quaternion.identity, objectParent);
#endif
                currentDungeonHeart.name = "DungeonHeart";
                break;
            }
        }
    }

    // Helper method to get current active chunk info
    public Vector2Int GetActiveChunkPosition()
    {
        return activeChunkPosition;
    }

    public int GetGeneratedChunkCount()
    {
        return generatedChunks.Count;
    }

    // Visualization in Scene View
    void OnDrawGizmosSelected()
    {
        if (!showActiveChunk) return;

        // Draw active chunk bounds
        Vector2Int worldOffset = new Vector2Int(
            activeChunkPosition.x * dungeonWidth,
            activeChunkPosition.y * dungeonHeight
        );

        Vector3 chunkCenter = new Vector3(
            worldOffset.x + dungeonWidth * 0.5f,
            worldOffset.y + dungeonHeight * 0.5f,
            0
        );

        Gizmos.color = activeChunkColor;
        Gizmos.DrawWireCube(chunkCenter, new Vector3(dungeonWidth, dungeonHeight, 0));

        // Draw all generated chunks
        Gizmos.color = Color.gray;
        foreach (Vector2Int chunkPos in generatedChunks)
        {
            if (chunkPos == activeChunkPosition) continue; // Skip active chunk

            Vector2Int offset = new Vector2Int(
                chunkPos.x * dungeonWidth,
                chunkPos.y * dungeonHeight
            );

            Vector3 center = new Vector3(
                offset.x + dungeonWidth * 0.5f,
                offset.y + dungeonHeight * 0.5f,
                0
            );

            Gizmos.DrawWireCube(center, new Vector3(dungeonWidth, dungeonHeight, 0));
        }
    }
}