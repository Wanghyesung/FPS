using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                SOSceneData
목적 : 씬 하나가 필요로 하는 SOPoolData 목록을 묶어두는 컨테이너.
       SceneController가 이 자산 하나만 들고 있으면 ObjectPoolManager에
       풀 목록 전체를 넘길 수 있다(ObjectPoolManager.cs가 원래 의도한
       "SOSceneData -> SOPoolData 목록" 설계).
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_SceneData", menuName = "Game/Load/SceneData")]
public class SOSceneData : ScriptableObject
{
    public List<SOPoolData> PoolDataList = new();
}
