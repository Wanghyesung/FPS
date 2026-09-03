using UnityEngine;

/*///////////////////////////////////////////
                WeaponRecoilKick
목적 : 사격 시 무기 모델(이 컴포넌트가 붙은 transform)에만 얹히는 순수 연출용
       스프링-댐퍼 반동.
 *///////////////////////////////////////////

public sealed class WeaponRecoilKick : MonoBehaviour
{
    private Vector3 m_vBaseLocalPos;
    private Quaternion m_qBaseLocalRot;
    private bool m_bBaseCaptured;

    private Vector3 m_vPosOffset;
    private Vector3 m_vPosVelocity;
    private Vector3 m_vRotOffset; // Euler(도) 기준 오프셋
    private Vector3 m_vRotVelocity;

    private float m_fStiffness = 180f;
    private float m_fDamping = 18f;

    public void CaptureBasePose()
    {
        m_vBaseLocalPos = transform.localPosition;
        m_qBaseLocalRot = transform.localRotation;
        m_bBaseCaptured = true;
    }

   
    public void Kick(Vector3 _vPosImpulse, Vector3 _vRotImpulseDeg, float _fStiffness, float _fDamping)
    {
        if (m_bBaseCaptured == false)
            CaptureBasePose();

        m_vPosOffset += _vPosImpulse;
        m_vRotOffset += _vRotImpulseDeg;
        m_fStiffness = _fStiffness;
        m_fDamping = _fDamping;
    }

    // LateUpdate가 아닌 Update: Animation Rigging의 TwoBoneIKConstraint(왼손 IK)는
    // Update와 LateUpdate 사이의 Animation 단계에서 평가된다. 여기서 위치를 갱신해야
    // 같은 프레임에 IK가 갱신된 LeftGrip 위치를 읽는다 — LateUpdate에 두면 IK가 항상
    // 한 프레임 전 그립 위치를 쫓아가서 반동 중 손이 따로 노는 것처럼 보인다.
    private void Update()
    {
        if (m_bBaseCaptured == false)
            return;

        float fDt = Time.deltaTime;

        m_vPosVelocity += (-m_vPosOffset * m_fStiffness - m_vPosVelocity * m_fDamping) * fDt;
        m_vPosOffset += m_vPosVelocity * fDt;

        m_vRotVelocity += (-m_vRotOffset * m_fStiffness - m_vRotVelocity * m_fDamping) * fDt;
        m_vRotOffset += m_vRotVelocity * fDt;

        transform.localPosition = m_vBaseLocalPos + m_vPosOffset;
        transform.localRotation = m_qBaseLocalRot * Quaternion.Euler(m_vRotOffset);
    }
}
