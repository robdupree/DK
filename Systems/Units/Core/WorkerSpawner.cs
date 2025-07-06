using UnityEngine;

public class WorkerSpawner : MonoBehaviour
{
    public GameObject workerPrefab;
    public int numberToSpawn = 3;
    public Vector3 spawnPosition;

    private void Start()
    {
        for (int i = 0; i < numberToSpawn; i++)
        {
            Vector3 offset = new Vector3(i * 1.5f, 0, 0);
            Instantiate(workerPrefab, spawnPosition + offset, Quaternion.identity);
        }
    }
}
