using UnityEditor;
using UnityEngine;

/*///////////////////////////////////////////
                Bullet
목적 : Weapon이 풀에서 꺼낸 투사체를 직선으로 전진시키고, 진행 경로상의 충돌을
       매 FixedUpdate마다 Raycast로 검사해 IDamageable에게 피해를 준다.
       AttackInfo.AliveTime이 지나면 PoolObject가 자동으로 풀에 반납한다.
 *///////////////////////////////////////////

[RequireComponent(typeof(PoolObject))]
public class Bullet : MonoBehaviour
{
    [SerializeField] private PoolObject m_refPoolObject;
    [SerializeField] private PoolObject m_refHitEffectObj;

    private AttackInfo m_refAttackInfo;
    private tShotInfo m_tShotInfo;


    private Rigidbody m_refRigdbody;
    private void Awake()
    {
        m_refPoolObject = GetComponent<PoolObject>();
        m_refRigdbody = GetComponent<Rigidbody>();

    }

    private void FixedUpdate()
    {
        float fStep = m_tShotInfo.Speed * Time.fixedDeltaTime;

        m_refRigdbody.MovePosition(m_refRigdbody.position + transform.forward * fStep);
    }

    public static GameObject SpawnAttackObject(PoolObject _refPrefab, Vector3 _vPos, Quaternion _qRot, AttackInfo _refAttackInfo, tShotInfo _refShotInfo)
    {
        GameObject refObj = ObjectPoolManager.m_Instance.GetObject(_refPrefab, _vPos);
        if (refObj == null)
            return null;

        refObj.transform.rotation = _qRot;

        Bullet refBullet = refObj.GetComponent<Bullet>();
        if (refBullet != null)
            refBullet.Fire(_refAttackInfo, _refShotInfo);

        return refObj;
    }

    private void Fire(AttackInfo _refAttackInfo, tShotInfo _refShotInfo)
    {

        m_refAttackInfo = _refAttackInfo;
        m_tShotInfo = _refShotInfo;
        m_refPoolObject.SetAliveTime(_refAttackInfo.AliveTime);
    }


    private void OnTriggerEnter(Collider other)
    {
        Attack(other);
    }

    protected virtual void Attack(Collider _refOther)
    {
        int iOtherLayer = 1 << _refOther.gameObject.layer;

        if( (iOtherLayer & m_refAttackInfo.HitLayers.value) == 0)
            return;

        var iDamageable = _refOther.GetComponent<IDamageable>();
       
        if (iDamageable != null)
        {
            ++m_tShotInfo.HitCount;
            m_tShotInfo.HitPosition = transform.position;
            iDamageable.TakeDamage(m_refAttackInfo, m_tShotInfo);
          
        }

        if (m_refHitEffectObj != null)
        {
            GameObject refHitEffect = ObjectPoolManager.m_Instance.GetObject(m_refHitEffectObj);
            if (refHitEffect != null)
                refHitEffect.transform.position = transform.position;
        }

        if (m_tShotInfo.HitCount >= m_refAttackInfo.MaxHitCount)
            ObjectPoolManager.m_Instance.PushObject(gameObject);

    }
}
