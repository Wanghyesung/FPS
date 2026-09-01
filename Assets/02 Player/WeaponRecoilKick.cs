using UnityEngine;

/*///////////////////////////////////////////
                WeaponRecoilKick
목적 : 사격 시 무기 모델(이 컴포넌트가 붙은 transform)에만 얹히는 순수 연출용
       스프링-댐퍼 반동. Weapon.Init()이 그립 정렬 직후 CaptureBasePose()를 호출해
       "반동이 없는 기준 로컬 포즈"를 잡아두고, 그 위에 위치/회전 오프셋을 스프링으로
       튕겼다가 감쇠시킨다.

       조준(PlayerMovement의 pitch/yaw)이나 실제 탄 퍼짐(Weapon.m_fInaccuracyAngle)과는
       완전히 분리되어 있다 — 이 컴포넌트는 transform.localPosition/localRotation만
       건드리고, Aim.TargetPosition/Weapon.Fire()의 조준 계산에는 전혀 관여하지 않는다.
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

    // Weapon.Init()이 TakeWeapon() 직후에 호출한다 — 매 프레임 캡처하면
    // 반동으로 밀린 위치를 새 기준점으로 착각해서 원점이 계속 밀려나는(드리프트) 버그가 생긴다.
    public void CaptureBasePose()
    {
        m_vBaseLocalPos = transform.localPosition;
        m_qBaseLocalRot = transform.localRotation;
        m_bBaseCaptured = true;
    }

    // Weapon.OnBulletFired()가 발사마다 호출 — 위치/회전에 즉각적인 임펄스를 준다.
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
