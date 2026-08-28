using Cysharp.Threading.Tasks;
using UnityEngine;

/*///////////////////////////////////////////
                SceneController
목적 : 씬 시작 시 SOSceneData가 들고 있는 SOPoolData 목록을 ObjectPoolManager에
       넘겨 프리워밍을 시작한다(ObjectPoolManager.cs 자체 설계 의도).
 *///////////////////////////////////////////

public sealed class SceneController : MonoBehaviour
{
    [SerializeField] private SOSceneData m_refSceneData;

    private void Start()
    {
        ObjectPoolManager.m_Instance.LoadPoolAsync(m_refSceneData.PoolDataList, this.GetCancellationTokenOnDestroy()).Forget();
    }
}
