using UnityEngine;

public class DigReactionSystem : MonoBehaviour
{
    public GameObject enemyPrefab;

    public void OnTileDug(Vector3Int pos)
    {
        if (Random.value < 0.1f)
        {
            Vector3 worldPos = pos + Vector3.up * 0.5f;
            Instantiate(enemyPrefab, worldPos, Quaternion.identity);
            Debug.Log("Slime spawned!");
        }
    }
}
