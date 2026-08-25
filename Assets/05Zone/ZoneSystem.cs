using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

/*///////////////////////////////////////////
                ZoneSystem
목적 : 기획 §6 자기장 시스템. 매치 시작 시각을 기준으로 페이즈를 진행하며,
       경고 시작~축소 시간 동안 반경을 Lerp로 줄이고, 1초 간격 UniTask 틱으로
       반경 밖의 모든 IZoneTarget에 현재 페이즈 피해를 적용한다(코루틴 금지).
       중심은 고정(맵 중앙)이며, 최종 페이즈 진입 후 유예 60초가 지나면
       OnFinalGracePeriodExpired로 매치 강제 종료를 알린다(기획 §10 스테일메이트 방지).
       대상 목록은 각 대상이 OnEnable/OnDisable에서 스스로 등록/해제한다(FindObjectOfType 금지).
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class ZoneSystem : MonoBehaviour
{
    /// <summary>(페이즈 번호, 다음 이벤트까지 남은 시간, 현재 반경) — HUD가 구독.</summary>
    public static event Action<int, float, float> OnPhaseChanged;
    public static event Action OnFinalGracePeriodExpired;

    private static readonly List<IZoneTarget> s_listTargets = new List<IZoneTarget>(8);

    [SerializeField] private ZonePhaseDefinition[] m_refPhases;
    [SerializeField] private Transform m_refCenterAnchor;
    [SerializeField] private ZoneView m_refView;
    [SerializeField] private float m_fFinalGracePeriod = 60f;

    private CancellationTokenSource m_cts;
    private Vector3 m_vCenter;
    private float m_fMatchTime;
    private float m_fGraceTimer;
    private float m_fCurrentRadius = 200f;
    private int m_iCurrentPhase = -1;
    private bool m_bIsRunning;
    private bool m_bGraceFired;

    public Vector3 CurrentCenter => m_vCenter;
    public float CurrentRadius => m_fCurrentRadius;
    public int CurrentPhase => m_iCurrentPhase < 0 ? 0 : m_iCurrentPhase;

    public static void Register(IZoneTarget _refTarget)
    {
        if (_refTarget == null || s_listTargets.Contains(_refTarget))
        {
            return;
        }

        s_listTargets.Add(_refTarget);
    }

    public static void Unregister(IZoneTarget _refTarget)
    {
        if (_refTarget == null)
        {
            return;
        }

        s_listTargets.Remove(_refTarget);
    }

    private void Awake()
    {
        m_vCenter = m_refCenterAnchor != null ? m_refCenterAnchor.position : transform.position;

        if (m_refPhases != null && m_refPhases.Length > 0 && m_refPhases[0] != null)
        {
            m_fCurrentRadius = m_refPhases[0].FinalRadius;
        }
    }

    private void OnEnable()
    {
        MatchFlowSystem.OnMatchEnded += HandleMatchEnded;
    }

    private void OnDisable()
    {
        MatchFlowSystem.OnMatchEnded -= HandleMatchEnded;
        StopZone();
    }

    private void Start()
    {
        if (m_refPhases == null || m_refPhases.Length == 0)
        {
            return;
        }

        m_bIsRunning = true;
        m_cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        DamageTickLoopAsync(m_cts.Token).Forget();

        ApplyState(0, m_fCurrentRadius, 0f, true);
    }

    private void Update()
    {
        if (!m_bIsRunning)
        {
            return;
        }

        m_fMatchTime += Time.deltaTime;

        int iPhase = ResolvePhaseIndex(m_fMatchTime);
        float fRadius = ResolveRadius(iPhase, m_fMatchTime);
        float fRemaining = ResolveRemaining(iPhase, m_fMatchTime);

        ApplyState(iPhase, fRadius, fRemaining, iPhase != m_iCurrentPhase);

        if (iPhase < m_refPhases.Length - 1 || m_bGraceFired)
        {
            return;
        }

        m_fGraceTimer += Time.deltaTime;
        if (m_fGraceTimer < m_fFinalGracePeriod)
        {
            return;
        }

        m_bGraceFired = true;
        OnFinalGracePeriodExpired?.Invoke();
    }

    private void ApplyState(int _iPhase, float _fRadius, float _fRemaining, bool _bForceNotify)
    {
        m_iCurrentPhase = _iPhase;
        m_fCurrentRadius = _fRadius;

        if (m_refView != null)
        {
            m_refView.Refresh(m_vCenter, _fRadius);
        }

        if (_bForceNotify)
        {
            OnPhaseChanged?.Invoke(_iPhase, _fRemaining, _fRadius);
        }
    }

    private async UniTaskVoid DamageTickLoopAsync(CancellationToken _token)
    {
        while (m_bIsRunning)
        {
            bool bCanceled = await UniTask.Delay(TimeSpan.FromSeconds(1d), cancellationToken: _token)
                .SuppressCancellationThrow();

            if (bCanceled || !m_bIsRunning)
            {
                return;
            }

            ApplyZoneDamageTick();

            // HUD 타이머는 1초 해상도면 충분하므로 매 프레임이 아니라 이 틱에서 갱신한다
            OnPhaseChanged?.Invoke(m_iCurrentPhase, ResolveRemaining(m_iCurrentPhase, m_fMatchTime), m_fCurrentRadius);
        }
    }

    private void ApplyZoneDamageTick()
    {
        if (m_iCurrentPhase < 0 || m_iCurrentPhase >= m_refPhases.Length)
        {
            return;
        }

        ZonePhaseDefinition refPhase = m_refPhases[m_iCurrentPhase];
        if (refPhase == null || refPhase.DamagePerSecond <= 0)
        {
            return;
        }

        float fRadiusSqr = m_fCurrentRadius * m_fCurrentRadius;

        for (int i = s_listTargets.Count - 1; i >= 0; i--)
        {
            IZoneTarget refTarget = s_listTargets[i];
            UnityEngine.Object refUnityObject = refTarget as UnityEngine.Object;

            if (refTarget == null || refUnityObject == null)
            {
                s_listTargets.RemoveAt(i); // 씬 전환 등으로 파괴된 잔여 엔트리 정리
                continue;
            }

            if (refTarget.IsDead)
            {
                continue;
            }

            Vector3 vDelta = refTarget.Position - m_vCenter;
            vDelta.y = 0f; // 지상 맵이므로 수평 거리로만 판정

            if (vDelta.sqrMagnitude <= fRadiusSqr)
            {
                continue; // 인수조건 #5 — 자기장 안에서는 피해 없음
            }

            refTarget.ApplyZoneDamage(refPhase.DamagePerSecond);
        }
    }

    private int ResolvePhaseIndex(float _fTime)
    {
        int iResult = 0;

        for (int i = 0; i < m_refPhases.Length; i++)
        {
            if (m_refPhases[i] == null || _fTime < m_refPhases[i].WarningStartTime)
            {
                continue;
            }

            iResult = i;
        }

        return iResult;
    }

    private float ResolveRadius(int _iPhase, float _fTime)
    {
        ZonePhaseDefinition refPhase = m_refPhases[_iPhase];
        if (refPhase == null)
        {
            return m_fCurrentRadius;
        }

        if (_iPhase == 0 || refPhase.ShrinkDuration <= 0f)
        {
            return refPhase.FinalRadius;
        }

        ZonePhaseDefinition refPrev = m_refPhases[_iPhase - 1];
        float fPrevRadius = refPrev != null ? refPrev.FinalRadius : refPhase.FinalRadius;
        float fT = Mathf.Clamp01((_fTime - refPhase.WarningStartTime) / refPhase.ShrinkDuration);
        return Mathf.Lerp(fPrevRadius, refPhase.FinalRadius, fT);
    }

    private float ResolveRemaining(int _iPhase, float _fTime)
    {
        if (_iPhase < 0 || _iPhase >= m_refPhases.Length)
        {
            return 0f;
        }

        ZonePhaseDefinition refPhase = m_refPhases[_iPhase];
        if (refPhase != null && refPhase.ShrinkDuration > 0f)
        {
            float fShrinkEnd = refPhase.WarningStartTime + refPhase.ShrinkDuration;
            if (_fTime < fShrinkEnd)
            {
                return fShrinkEnd - _fTime; // 축소가 끝날 때까지 남은 시간
            }
        }

        if (_iPhase + 1 < m_refPhases.Length && m_refPhases[_iPhase + 1] != null)
        {
            return Mathf.Max(0f, m_refPhases[_iPhase + 1].WarningStartTime - _fTime);
        }

        return Mathf.Max(0f, m_fFinalGracePeriod - m_fGraceTimer);
    }

    private void HandleMatchEnded(bool _bIsVictory)
    {
        StopZone();
    }

    private void StopZone()
    {
        m_bIsRunning = false;

        if (m_cts == null)
        {
            return;
        }

        m_cts.Cancel();
        m_cts.Dispose();
        m_cts = null;
    }
}
