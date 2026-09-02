using UnityEngine;
using UnityEditor;
using System.Threading;
using Cysharp.Threading.Tasks;
#if UNITY_EDITOR
public class RemoveMeshCollider
{
    [MenuItem("Tools/Remove All MeshColliders")]
    public static void RemoveAllMeshColliders()
    {
        MeshCollider[] colliders =
            Object.FindObjectsByType<MeshCollider>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        int count = 0;

        foreach (MeshCollider collider in colliders)
        {
            Undo.DestroyObjectImmediate(collider);
            count++;
        }

        Debug.Log($"MeshCollider {count}개 삭제 완료!");
        
    }
}
#endif