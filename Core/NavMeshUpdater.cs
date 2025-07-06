using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class NavMeshUpdater : MonoBehaviour
{
    public static NavMeshUpdater Instance;
    public NavMeshSurface surface;

    private void Awake()
    {
        Instance = this;
    }

    public void RebuildNavMesh()
    {
        surface.BuildNavMesh();
    }
}
