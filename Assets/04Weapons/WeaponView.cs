using Cysharp.Threading.Tasks;
using UnityEngine;

/*///////////////////////////////////////////
                WeaponView
목적 : 발사 이벤트(WeaponSystem.OnMuzzleFlash)를 받아 총구 연출 FX를 뿌리는 표시 전용 View.
       FX는 Instantiate/Destroy 대신 ObjectPool에서 대여하고 일정 시간 후 자동 반납해
       교전 중 GC 스파이크를 막는다(performance.md).
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class WeaponView : MonoBehaviour
{
    [SerializeField] private SOPoolData m_refTrailPoolData;
    [SerializeField] private SOPoolData m_refSmokePoolData;
    [SerializeField] private SOPoolData m_refShellPoolData;
    [SerializeField] private float m_fFxLifetime = 2f;

    private void OnEnable()
    {
        WeaponSystem.OnMuzzleFlash += HandleMuzzleFlash;
    }

    private void OnDisable()
    {
        WeaponSystem.OnMuzzleFlash -= HandleMuzzleFlash;
    }

    private void HandleMuzzleFlash(Vector3 _vPos, Vector3 _vDir)
    {
        if (_vDir.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion tRot = Quaternion.LookRotation(_vDir, Vector3.up);
        SpawnFx(m_refTrailPoolData, _vPos, tRot);
        SpawnFx(m_refSmokePoolData, _vPos, tRot);
        SpawnFx(m_refShellPoolData, _vPos, tRot);
    }

    private void SpawnFx(SOPoolData _data, Vector3 _vPos, Quaternion _tRot)
    {
        if (_data == null || ObjectPool.Instance == null)
        {
            return;
        }

        PoolObject refObj = ObjectPool.Instance.Rent(_data, _vPos, _tRot);
        if (refObj == null)
        {
            return;
        }

        ObjectPool.Instance.ReturnAfter(_data, refObj, m_fFxLifetime).Forget();
    }
}
