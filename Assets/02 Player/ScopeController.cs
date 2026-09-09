using System;
using UnityEngine;

/*///////////////////////////////////////////
                ScopeController
목적 : 조준 진입/해제를 받아 카메라 FOV 줌, 조준선 카메라 이동, 마우스 감도 보정,
       무기 메쉬 숨김, 스코프 오버레이 알파를 한 프레임 안에서 묶어 처리하는 오케스트레이터.
 *///////////////////////////////////////////

[DefaultExecutionOrder(110)] // CameraZoom(90) → GameCameraManager(100) 이 끝난 뒤 진행도를 읽는다
public sealed class ScopeController : MonoBehaviour
{
    // 스코프 오버레이 진행도(0 = 해제, 1 = 완전 진입). ScopeOverlayView가 구독한다.
    public static event Action<float> OnScopeChanged;

    [SerializeField] private PlayerMovement m_refMovement;
    [SerializeField] private float m_fHideWeaponAt = 0.5f; // 이 진행도 이상에서 무기 메쉬를 숨긴다

    private CameraZoom m_refCameraZoom;
    private Weapon m_refWeapon;

    private eAimMode m_eActiveMode = eAimMode.None; // 이번 조준에서 사용 중인 모드
    private bool m_bAiming;                         // Enter/Exit 멱등 가드 — Zoom()이 매 프레임 호출되어도 1회만 반응
    private bool m_bWeaponHidden;
    private bool m_bAimPivotSet;
    private float m_fAppliedProgress = -1f;

    private void Start()
    {
        if (GameCameraManager.m_Instance != null)
            m_refCameraZoom = GameCameraManager.m_Instance.CameraZoom;
    }

    private void LateUpdate()
    {
        if (m_refCameraZoom == null)
            return;

        float fProgress = m_refCameraZoom.ZoomProgress;
        if (Mathf.Approximately(fProgress, m_fAppliedProgress) == true)
            return;

        m_fAppliedProgress = fProgress;

        // 오버레이는 스코프 모드에서만 
        OnScopeChanged?.Invoke(m_eActiveMode == eAimMode.Scope ? fProgress : 0f);

        if (m_refMovement != null)
            m_refMovement.SetLookScale(Mathf.Lerp(1f, m_refCameraZoom.LookSensitivityScale, fProgress));

        SetWeaponRender(fProgress);

        //조준이 끝나면 Pivot해제
        if (m_bAiming == false && fProgress <= 0f)
            ClearAimPivot();
    }

    // Player.EquipWeapon이 호출 — 무기를 교체해도 조준 상태가 이전 무기에 남지 않게 한다.
    public void SetWeapon(Weapon _refWeapon)
    {
        if (m_bWeaponHidden == true && m_refWeapon != null)
            m_refWeapon.SetRenderersVisible(true);

        m_bWeaponHidden = false;
        m_refWeapon = _refWeapon;
    }

    public void Enter()
    {
        if (m_bAiming == true)
            return;

        if (m_refWeapon == null || m_refCameraZoom == null)
            return;

        eAimMode eMode = m_refWeapon.AimMode;
        if (eMode == eAimMode.None)
            return;

        m_bAiming = true;
        m_eActiveMode = eMode;

        //카메라 FOV각 조절
        m_refCameraZoom.SetZoom(m_refWeapon.ZoomFov, m_refWeapon.ZoomBlendTime);

        // 아이언사이트만 카메라를 총의 조준선으로 옮긴다.
        if (eMode == eAimMode.IronSight)
        {
            GameCameraManager.m_Instance.SetAimPivot(m_refWeapon.ZoomTr);
            m_bAimPivotSet = true;
        }
    }

    public void Exit()
    {
        if (m_bAiming == false)
            return;

        m_bAiming = false;

        if (m_refCameraZoom != null)
            m_refCameraZoom.ClearZoom();
    }

    private void SetWeaponRender(float _fProgress)
    {
        if (m_refWeapon == null)
            return;

        bool bHide = (m_refWeapon.HideWeaponOnAim == true && _fProgress >= m_fHideWeaponAt);
        if (bHide == m_bWeaponHidden)
            return;

        m_bWeaponHidden = bHide;
        m_refWeapon.SetRenderersVisible(bHide == false);
    }

    private void ClearAimPivot()
    {
        m_eActiveMode = eAimMode.None;

        if (m_bAimPivotSet == false)
            return;

        m_bAimPivotSet = false;

        GameCameraManager.m_Instance.SetAimPivot(null);
    }
}
