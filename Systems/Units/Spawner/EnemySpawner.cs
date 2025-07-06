using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Fliegen-Einstellungen")]
    public GameObject flyPrefab;       // Prefab für die Fliege
    public int numberToSpawn = 5;      // Anzahl Fliegen, die beim Start gespawnt werden
    public Vector3 spawnPosition;      // Zentrum der Spawn-Positionen
    public float spawnRadius = 3f;     // Radius um die spawnPosition herum, in dem gestreut wird

    private void Start()
    {
        for (int i = 0; i < numberToSpawn; i++)
        {
            Vector3 randomOffset = new Vector3(
                Random.Range(-spawnRadius, spawnRadius),
                0f,
                Random.Range(-spawnRadius, spawnRadius)
            );
            Vector3 pos = spawnPosition + randomOffset;
            Instantiate(flyPrefab, pos, Quaternion.identity);
        }
    }
}
