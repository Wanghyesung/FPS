using UnityEngine;

/*///////////////////////////////////////////
                ZoneView
목적 : 자기장 경계를 시각적으로 표시하기만 하는 Passive View.
       ZoneSystem가 매 프레임 Refresh(center, radius)를 호출하면 위치와 스케일만 맞춘다.
       판정/타이머/피해 로직은 전혀 갖지 않는다.
       경계 메시(DamageZone 프리팹)는 지름 1 기준이라고 가정하고 m_fPrefabUnitDiameter로 보정한다.
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class ZoneView : MonoBehaviour
{
    [SerializeField] private Transform m_refBoundaryRoot;
    [SerializeField] private float m_fPrefabUnitDiameter = 1f;
    [SerializeField] private float m_fHeightScale = 1f;
    [SerializeField] private float m_fGroundY;

    private Vector3 m_vScale = Vector3.one;

    public void Refresh(Vector3 _vCenter, float _fRadius)
    {
        if (m_refBoundaryRoot == null)
        {
            return;
        }

        float fUnit = m_fPrefabUnitDiameter > 0f ? m_fPrefabUnitDiameter : 1f;
        float fScale = _fRadius * 2f / fUnit;

        m_vScale.x = fScale;
        m_vScale.y = m_fHeightScale;
        m_vScale.z = fScale;

        m_refBoundaryRoot.position = new Vector3(_vCenter.x, m_fGroundY, _vCenter.z);
        m_refBoundaryRoot.localScale = m_vScale;
    }
}
