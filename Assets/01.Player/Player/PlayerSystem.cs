using System;
using UnityEngine;

/*///////////////////////////////////////////
                PlayerSystem
목적 : CharacterController 기반 플레이어 이동/점프/웅크리기와 체력을 담당하는 System.
       입력이 어디서 오는지 알지 못하며(InputView가 호출), 히트스캔 피격은
       IDamageable, 자기장 피해는 IZoneTarget 구현을 통해 받는다.
       총알 피해에만 방탄조끼 30% 감쇠를 적용하기 위해 ItemSystem(같은 GameObject)를
       캐싱해 참조한다 — 자기장 피해(ApplyZoneDamage)에는 감쇠를 적용하지 않는다(기획 §5.2).
 *///////////////////////////////////////////

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public sealed class PlayerSystem : MonoBehaviour, IDamageable, IZoneTarget
{
    private const float VEST_DAMAGE_MULTIPLIER = 0.7f; // 기획 §5.2 — 총알 피해 30% 감소

    public static event Action OnPlayerDied;
    public static event Action<int, int> OnHealthChanged;
    public static event Action<int> OnDamaged;

    [SerializeField] private float m_fWalkSpeed = 4f;
    [SerializeField] private float m_fSprintSpeed = 7f;
    [SerializeField] private float m_fCrouchSpeed = 2f;
    [SerializeField] private float m_fJumpHeight = 1.2f;
    [SerializeField] private float m_fGravity = -20f;
    [SerializeField] private float m_fStandHeight = 1.8f;
    [SerializeField] private float m_fCrouchHeight = 1.1f;

    private readonly PlayerModel m_model = new PlayerModel();

    private CharacterController m_refController;
    private Transform m_refTransform;
    private ItemSystem m_refItemSystem;
    private WeaponSystem m_refWeaponSystem;
    private LootPickupSystem m_refInteractable;

    private Vector2 m_vMoveInput;
    private Vector3 m_vVelocity;
    private float m_fVerticalVelocity;
    private bool m_bIsSprinting;
    private bool m_bIsCrouching;
    private bool m_bIsGrounded;
    private bool m_bIsMovementLocked;

    public int CurrentHP => m_model.HP;
    public int MaxHP => PlayerModel.MAX_HP;
    public bool IsDead => m_model.IsDead;
    public bool IsAiming => m_model.IsAiming;
    public bool IsGrounded => m_bIsGrounded;
    public bool IsMoving => !m_bIsMovementLocked && m_vMoveInput.sqrMagnitude > 0.0001f;
    public Vector3 Position => m_refTransform != null ? m_refTransform.position : transform.position;

    private void Awake()
    {
        m_refController = GetComponent<CharacterController>();
        m_refTransform = transform;
        m_refItemSystem = GetComponent<ItemSystem>();
        m_refWeaponSystem = GetComponent<WeaponSystem>();
        ApplyHeight(m_fStandHeight);
    }

    private void OnEnable()
    {
        ZoneSystem.Register(this);
    }

    private void OnDisable()
    {
        ZoneSystem.Unregister(this);
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(m_model.HP, PlayerModel.MAX_HP);
    }

    private void Update()
    {
        if (m_model.IsDead)
        {
            return;
        }

        m_bIsGrounded = m_refController.isGrounded;

        if (m_bIsGrounded && m_fVerticalVelocity < 0f)
        {
            m_fVerticalVelocity = -2f; // 접지 유지용 상시 하향 가속
        }

        float fSpeed = m_bIsMovementLocked ? 0f : ResolveSpeed();

        Vector3 vPlanar = m_refTransform.right * m_vMoveInput.x + m_refTransform.forward * m_vMoveInput.y;
        if (vPlanar.sqrMagnitude > 1f)
        {
            vPlanar.Normalize();
        }

        m_fVerticalVelocity += m_fGravity * Time.deltaTime;

        m_vVelocity.x = vPlanar.x * fSpeed;
        m_vVelocity.z = vPlanar.z * fSpeed;
        m_vVelocity.y = m_fVerticalVelocity;

        m_refController.Move(m_vVelocity * Time.deltaTime);
    }

    public void SetMoveInput(Vector2 _vInput)
    {
        m_vMoveInput = _vInput;
    }

    public void SetSprint(bool _bIsSprinting)
    {
        m_bIsSprinting = _bIsSprinting;
    }

    public void SetCrouch(bool _bPressed)
    {
        if (!_bPressed)
        {
            return; // 토글 방식 — 눌린 순간에만 상태를 뒤집는다
        }

        m_bIsCrouching = !m_bIsCrouching;
        ApplyHeight(m_bIsCrouching ? m_fCrouchHeight : m_fStandHeight);
    }

    public void SetAiming(bool _bIsAiming)
    {
        m_model.IsAiming = _bIsAiming;
    }

    /// <summary>구급상자 사용처럼 "사용 중 이동 불가" 아이템이 잠금/해제할 때 호출한다(기획 §5.2).</summary>
    public void SetMovementLocked(bool _bLocked)
    {
        m_bIsMovementLocked = _bLocked;
    }

    public void Jump()
    {
        if (m_model.IsDead || m_bIsMovementLocked || !m_bIsGrounded)
        {
            return;
        }

        m_fVerticalVelocity = Mathf.Sqrt(m_fJumpHeight * -2f * m_fGravity);
    }

    /// <summary>범위 안에 들어온 루팅 픽업을 후보로 등록한다(LootPickupSystem의 트리거가 호출).</summary>
    public void SetInteractable(LootPickupSystem _refPickup)
    {
        if (_refPickup == null)
        {
            return;
        }

        m_refInteractable = _refPickup;
    }

    public void ClearInteractable(LootPickupSystem _refPickup)
    {
        if (m_refInteractable == _refPickup)
        {
            m_refInteractable = null;
        }
    }

    /// <summary>Interact(E) 입력 — 현재 범위 안의 픽업을 획득 시도한다(기획 §4.3).</summary>
    public void TryInteract()
    {
        if (m_model.IsDead || m_refInteractable == null)
        {
            return;
        }

        if (m_refInteractable.TryPickup(m_refItemSystem, m_refWeaponSystem))
        {
            m_refInteractable = null;
        }
    }

    public void Heal(int _iAmount)
    {
        if (m_model.IsDead || _iAmount == 0)
        {
            return;
        }

        // 음수는 "완전 회복" 센티널(구급상자) — ItemDefinition.HealAmount 규약과 맞춘다
        m_model.HP = _iAmount < 0 ? PlayerModel.MAX_HP : Mathf.Min(PlayerModel.MAX_HP, m_model.HP + _iAmount);
        OnHealthChanged?.Invoke(m_model.HP, PlayerModel.MAX_HP);
    }

    /// <summary>총알 피해 — 방탄조끼 감쇠가 적용되는 경로(IDamageable).</summary>
    public void TakeDamage(int _iAmount, bool _bIsHeadshot)
    {
        int iFinal = _iAmount;

        if (m_refItemSystem != null && m_refItemSystem.HasVest && iFinal > 0)
        {
            iFinal = Mathf.Max(1, Mathf.RoundToInt(iFinal * VEST_DAMAGE_MULTIPLIER));
        }

        ApplyDamage(iFinal);
    }

    /// <summary>자기장 피해 — 방탄조끼 감쇠 대상이 아니다(IZoneTarget).</summary>
    public void ApplyZoneDamage(int _iAmount)
    {
        ApplyDamage(_iAmount);
    }

    public void TakeDamage(int _iAmount)
    {
        ApplyDamage(_iAmount);
    }

    private void ApplyDamage(int _iAmount)
    {
        if (m_model.IsDead || _iAmount <= 0)
        {
            return;
        }

        m_model.HP = Mathf.Max(0, m_model.HP - _iAmount);
        OnHealthChanged?.Invoke(m_model.HP, PlayerModel.MAX_HP);
        OnDamaged?.Invoke(_iAmount); // 구급상자 사용 취소 등 "피격 반응" 구독자용

        if (m_model.IsDead)
        {
            OnPlayerDied?.Invoke();
        }
    }

    private float ResolveSpeed()
    {
        if (m_bIsCrouching)
        {
            return m_fCrouchSpeed;
        }

        if (m_bIsSprinting && m_vMoveInput.y > 0.1f)
        {
            return m_fSprintSpeed;
        }

        return m_fWalkSpeed;
    }

    private void ApplyHeight(float _fHeight)
    {
        m_refController.height = _fHeight;
        m_refController.center = new Vector3(0f, _fHeight * 0.5f, 0f);
    }
}
