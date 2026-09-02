using UnityEngine;

/*///////////////////////////////////////////
                Grenade
목적 : GrenadeThrower가 계산한 초기 속도로 날아가는 투사체. Bullet과 달리 직선
       스크립트 이동이 아니라 진짜 Rigidbody 물리(useGravity)로 날아가며 지형에
       자연스럽게 부딪히고 굴러간다 — 예고선(GrenadeThrower)이 같은 포물선 공식으로
       착지점을 미리 계산하므로 실제 궤적과 사실상 일치한다.

       폭발은 충돌 즉시가 아니라 퓨즈 타이머 방식이다. PoolObject.SetAliveTime()이
       이미 "그 시간 뒤 자동으로 Push() 호출"을 보장하므로, 별도 타이머를 새로 만들지
       않고 PoolObject.OnPush 이벤트를 그대로 퓨즈 트리거로 재사용한다.
 *///////////////////////////////////////////

[RequireComponent(typeof(PoolObject))]
[RequireComponent(typeof(Rigidbody))]
public class Grenade : MonoBehaviour
{
    [SerializeField] private PoolObject m_refPoolObject;
    [SerializeField] private PoolObject m_refExplosionEffectObj;

    private Rigidbody m_refRigidbody;
    private AttackInfo m_refAttackInfo;

    private readonly Collider[] m_arrOverlapBuffer = new Collider[16]; // 폭발 판정용 — 매 폭발마다 새로 할당하지 않기 위해 미리 확보

    private void Awake()
    {
        m_refPoolObject = GetComponent<PoolObject>();
        m_refRigidbody = GetComponent<Rigidbody>();

        m_refPoolObject.OnPush += Explode; // SetAliveTime()으로 예약한 퓨즈 시간이 지나 자동 반납될 때 폭발
    }

    public static GameObject SpawnAttackObject(PoolObject _refPrefab, Vector3 _vPos, AttackInfo _refAttackInfo, Vector3 _vInitialVelocity)
    {
        GameObject refObj = ObjectPoolManager.m_Instance.GetObject(_refPrefab, _vPos);
        if (refObj == null)
            return null;

        Grenade refGrenade = refObj.GetComponent<Grenade>();
        if (refGrenade != null)
            refGrenade.Fire(_refAttackInfo, _vInitialVelocity);

        return refObj;
    }

    private void Fire(AttackInfo _refAttackInfo, Vector3 _vInitialVelocity)
    {
        m_refAttackInfo = _refAttackInfo;

        m_refRigidbody.velocity = _vInitialVelocity;
        m_refRigidbody.angularVelocity = Vector3.zero;

        m_refPoolObject.SetAliveTime(_refAttackInfo.AliveTime); // AliveTime = 퓨즈까지 남은 시간
    }

    private void Explode()
    {
        if (m_refAttackInfo == null)
            return;

        int iCount = Physics.OverlapSphereNonAlloc(transform.position, m_refAttackInfo.ExplosionRadius, m_arrOverlapBuffer, m_refAttackInfo.HitLayers);

        tShotInfo tShot = new tShotInfo();
        tShot.HitPosition = transform.position;

        for (int i = 0; i < iCount; ++i)
        {
            IDamageable iDamageable = m_arrOverlapBuffer[i].GetComponent<IDamageable>();
            if (iDamageable == null)
                continue;

            iDamageable.TakeDamage(m_refAttackInfo, tShot);
        }

        if (m_refExplosionEffectObj != null)
        {
            GameObject refEffect = ObjectPoolManager.m_Instance.GetObject(m_refExplosionEffectObj);
            if (refEffect != null)
                refEffect.transform.position = transform.position;
        }
    }
}
