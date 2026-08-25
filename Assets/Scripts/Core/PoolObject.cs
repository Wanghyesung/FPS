using UnityEngine;

/*///////////////////////////////////////////
                PoolObject
목적 : ObjectPool이 다루는 풀링 대상의 기본 컴포넌트.
       IPoolable 기본 구현(아무 것도 하지 않음)을 제공하고, 파티클·트레일 등
       추가 리셋이 필요한 오브젝트는 이 클래스를 상속해 override 한다.
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public class PoolObject : MonoBehaviour, IPoolable
{
    public virtual void OnSpawned()
    {
    }

    public virtual void OnDespawned()
    {
    }
}
