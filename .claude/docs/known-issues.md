# Known Issues

## [수동 씬 작업 필요] Animation Rigging 패키지로 손 IK/조준 재구현 (2026-08-28)

기존 `WeaponIK.cs`(Humanoid `OnAnimatorIK` 기반 손 IK)를 삭제하고, `com.unity.animation.rigging`
패키지의 Two Bone IK Constraint / Multi-Aim Constraint 기반으로 교체하기로 결정. 코드는
`WeaponRigTarget.cs`(신규), `AimRigTarget.cs`(신규), `Player.cs`/`Weapon.cs`(연결 코드 수정)까지
끝냈지만, `.unity`/`.prefab` 직접 편집이 훅으로 차단돼 있어(`block-scene-edit.sh`) 아래는 Unity
에디터(또는 UnityMCP)에서 직접 해야 한다.

1. **Player 아바타 계층에 Rig 오브젝트 생성** — `Rig` 컴포넌트가 붙은 빈 자식 오브젝트 하나, 그
   아래에 자식 3개: `RightHandIK`/`LeftHandIK`(각각 Two Bone IK Constraint), `AimConstraint`
   (Multi-Aim Constraint).
2. **Two Bone IK Constraint 본 연결** — LeftHandIK: Root/Mid/Tip = UpperArm_L/LowerArm_L/Hand_L.
   RightHandIK도 같은 방식으로 만들어 두되, **target은 연결하지 말 것** — `Player.EquipWeapon()`이
   오른손은 항상 null을 넘긴다(자기참조 계층 문제로 오른손 IK는 쓰지 않기로 함, 아래 "발견된 근본
   원인" 참고).
3. **Multi-Aim Constraint 설정** — Constrained Object = Chest/Spine 본. Source Objects에 새로
   만든 빈 오브젝트("AimTargetPoint") 하나를 등록하고, 그 오브젝트에 `AimRigTarget` 컴포넌트를
   붙인 뒤 인스펙터의 `Aim` 필드에 씬의 `Aim` 컴포넌트를 연결.
4. **Player 루트에 RigBuilder 추가** — Rig Layers 리스트에 2번에서 만든 `Rig` 오브젝트 등록.
5. **Player 오브젝트에서 `WeaponIK` 컴포넌트 제거**(이미 스크립트는 삭제됐으므로 Missing Script로
   보일 수 있음), 대신 `WeaponRigTarget` 컴포넌트를 추가하고 `Left Hand Ik`/`Right Hand Ik` 슬롯에
   1번에서 만든 두 Two Bone IK Constraint를 연결.
6. **(검토) `UpperBodyMask.mask`/`DownBodyMask.mask`, `PlayerAnim.controller`의 UpperBody 레이어
   폐기 여부 결정** — 아래 "상하체 애니메이션 분리" 항목이 미완성 상태로 남아있는데, Multi-Aim
   Constraint가 상체 조준을 코드 레벨에서 직접 처리하므로 이 레이어 분리 작업 자체가 불필요해질
   가능성이 큼. 새 Rig 적용 후 상체 조준이 의도대로 동작하는지 먼저 확인하고, 정상이면 아래
   항목은 폐기 처리할 것.

### 발견된 근본 원인(기존 `WeaponIK`에서도 동일하게 적용되던 제약, 유지됨)
`RightHandGripTr`은 `WeaponSocket`(Hand_R의 자식)의 자식인 `Weapon` 아래에 있어서, 오른손 IK
타겟이 오른손 본 자신의 자손이 되는 자기참조 구조다. Two Bone IK Constraint도 같은 계층을
풀어야 하므로 오른손 IK를 걸면 회전이 수렴하지 않을 것으로 예상됨(기존 코드에서 62도 어긋난 채
수렴하지 않는 것을 실측). 오른손은 계속 리지드 페어런팅에 맡긴다.



## [진단 완료 / 수정 코드 적용 완료 — 에디터 수동 마무리 필요] 상하체 애니메이션 분리 (2026-08-28)

`PlayerAnim.controller`, `AnimationTable.cs`, `Player.cs`는 수정을 마쳤으나, 두 가지는
`.meta` / 씬(`.unity`) 파일에 대한 훅 차단(`block-meta-edit.sh`, `block-scene-edit.sh`)
때문에 에이전트가 직접 반영할 수 없었다. **Unity 에디터에서 아래 두 단계를 완료해야 실제로 동작한다:**

1. **`UpperBodyMask.mask`를 `UpperBody` 레이어에 할당**
   - `Assets/02 Player/UpperBodyMask.mask`는 새로 생성됨(내용은 Body/Head/양팔/양손가락/HandIK만 활성화한
     Humanoid 바디파트 마스크). `.meta`가 없으므로 Unity가 처음 임포트할 때 자동으로 GUID를 생성한다.
   - `PlayerAnim.controller` 열기 → Layers 패널의 `UpperBody` 레이어 톱니바퀴(Settings) 클릭 →
     `Mask` 슬롯에 `UpperBodyMask` 드래그.
   - 마스크 내용이 의도대로인지(Humanoid 탭에서 Body/Head/Arms/Fingers만 파랑, Legs는 회색) 육안으로
     한 번 확인 권장 — 손으로 작성한 비트마스크라 Unity Inspector에서 최종 확인이 필요함.

2. **`BattleScene.unity`의 Player → `AnimationTable` 컴포넌트 `m_listAnimationList` 갱신**
   - 기존 `Fire` 항목: Param Type을 **Bool → Trigger**로 변경 (Animator 파라미터 `Fire`를 Bool에서
     Trigger로 바꿨기 때문에 맞춰야 함).
   - 새 항목 추가: State = `HasWeapon`, Param Type = `Bool`, Param Name = `HasWeapon`.

### 발견된 근본 원인 버그 (이번 작업으로 함께 수정됨)
기존 `Player.cs Update()`가 무기 소지 중 매 프레임 `AnimationTable.SetBool(eEntityState.Shot, true)`를
호출하고 있었는데, 이 State(`Shot`)는 Animator의 `Fire` 파라미터(당시 타입 **Bool**)에 매핑되어 있었다.
즉 무기를 줍는 순간 `Fire` bool이 계속 `true`로 고정되고, 되돌아오는 조건(`StandFire → Base`, `IfNot Fire`)이
영원히 만족되지 않아 **한 번 사격 애니메이션(`StandFire`)에 진입하면 무기를 든 동안 절대 빠져나오지 못하는
버그**였다. `Player.Fire()`가 부르는 `SetTrigger(eEntityState.Shot)`도 같은 노드의 `Bool` 값을 재적용할
뿐이라 실질적으로 새 트리거 역할을 하지 못했다.

이번 리팩터로 `Fire`를 진짜 Animator Trigger로 바꾸고, "무기 소지 여부"는 별도의 `HasWeapon` Bool로
분리했다. `HasWeapon`은 `EquipWeapon()`에서 장착 시 1회만 true로 설정한다(현재 코드베이스에 무기 해제
로직이 없어 매 프레임 갱신할 이유가 없음 — 추후 무기 드롭 기능이 추가되면 그때 `false`로 설정하는 지점을
넣어야 함).

## [수동 씬 작업 필요] 무기 손 고정/절차적 반동 기능 추가 (2026-08-28)

`SOAttackInfo.cs`(Visual Recoil 필드 추가), `WeaponRecoilKick.cs`(신규 컴포넌트),
`WeaponIK.cs`(Pole Vector/팔꿈치 힌트 추가), `Weapon.cs`/`Player.cs`(연결 코드)까지는 코드로 끝냈지만,
`.unity`/`.asset` 직접 편집이 훅으로 차단돼 있어(`block-scene-edit.sh`) 아래는 Unity 에디터에서 직접
해줘야 한다. 이번 세션엔 UnityMCP도 연결되어 있지 않아 MCP로도 대신할 수 없었음.

1. **`WeaponRecoilKick` 컴포넌트 추가** — 씬의 `Weapon_Rifle` 게임오브젝트(비활성 상태인 `TRG`도 쓸 거면
   같이)에 `Add Component → Weapon Recoil Kick`. 없어도 에러는 안 나고 그냥 시각적 반동만 없이 동작함
   (`Weapon.OnBulletFired()`에서 null 체크하고 건너뜀).
2. **(선택) `LeftElbowHintTr`(Pole Vector) 연결** — 무기 그립 근처에 빈 자식 오브젝트를 하나 만들어
   팔꿈치가 향해야 할 방향(대체로 캐릭터 바깥쪽/아래쪽)에 배치한 뒤, `Weapon` 컴포넌트의
   `Left Elbow Hint Tr` 슬롯에 연결. 안 해도 회귀는 없음(기존처럼 힌트 없이 애니메이터 기본 팔꿈치
   방향으로 동작).
3. **`SO_AttackInfo_Rifle.asset`의 새 필드 값 확인** — `VisualKickback`/`VisualRotKick`/
   `VisualRotKickRandomYaw`/`VisualSpringStiffness`/`VisualSpringDamping`은 `SOAttackInfo.cs`의 필드
   기본값(`Vector3(0, 0.01, -0.05)` 등)이 자동 채워지지만, 인스펙터에서 한 번 열어 원하는 느낌으로
   튜닝해볼 것.

시각적 반동(무기 모델 킥)과 조준 반동(`PlayerMovement.AddRecoil`가 담당하는 pitch/yaw 스프레이 컨트롤),
실제 탄 퍼짐(`Weapon.m_fInaccuracyAngle`)은 서로 다른 세 개의 독립된 값/경로로 분리되어 있음 — 하나를
키운다고 다른 게 같이 흔들리지 않는다.

## [진단 완료 / 부분 수정] 왼손 Two Bone IK가 총(LeftHandGrip)을 안 따라가던 문제 (2026-08-31)

### 증상
총(Weapon_Rifle)이 회전해서 LeftHandGrip 위치가 바뀌어도 왼손(Hand_L)이 전혀 따라가지 않음.

### 근본 원인 1 (수정 완료) — RigBuilder.Build()가 Animator 자체 초기화보다 먼저 실행됨
`Player.Awake()`에서 씬에 미리 배치된 시작 무기를 장착하며 `EquipWeapon()` → `WeaponRigTarget.SetWeapon()`
→ `RigBuilder.Build()`가 호출됐는데, 이 시점은 `Animator`가 자기 내부 PlayableGraph를 아직 초기화하기
전이라 나중에 Animator가 자기 기본 그래프로 덮어써 버려 IK 계산 결과가 화면에 전혀 반영되지 않았다.
`Player.cs`의 장착 로직을 `Awake()` → `Start()`로 이동해서 해결(Unity가 모든 Awake/OnEnable이 Start
이전에 끝난다고 보장하므로 안전). 실측: 수정 전엔 그립을 45~60도 회전시켜도 Hand_L 위치가 480프레임
넘게 전혀 안 움직였고(거리 0.55 유닛 고정), 수정 후엔 즉시 따라가서 기본 포즈 기준 오차 0.000004
유닛(사실상 0)까지 줄었다.

### 근본 원인 2 (수정 완료) — TwoBoneIKConstraint의 targetPositionWeight가 0
`LeftHandIK` 컴포넌트의 `targetPositionWeight`가 인스펙터에 0으로 남아있어서, target 참조는 정확해도
위치 자체가 전혀 반영되지 않고 있었다. `WeaponRigTarget.SetHand()`에서 `targetPositionWeight`를 1로
강제하도록 수정(`targetRotationWeight`/`hintWeight`는 의도적으로 건드리지 않기로 함 — 위치 추적만
필요하다는 요구사항).

### 남은 문제 (미해결 — 원인 특정 못함, 재현 조건 확정)
`WeaponSocket`을 **큰 각도로** 회전시키면 Two Bone IK가 아예 수렴하지 않고 고정된 상태로 멈추는
증상이 재현됨. Play 모드에서 픽업 직후 깨끗한 상태(오차 0.000004 유닛, 사실상 완벽)로 시작해서
단일 회전 조작만으로 반복 검증함:

- 회전 45도(단일 축): 592프레임 뒤 오차 0.0000024 유닛 — **완벽하게 추적됨**
- 회전 135도+20도(2축 복합, 단발): 683프레임 뒤 오차 0.455 유닛 — **전혀 안 붙고 고정**. 이후
  2600프레임을 더 기다려도(재시작 없이) 오차가 정확히 그대로 유지됨(0.4553751 → 0.4553748,
  6번째 유효숫자까지 불변) — 프레임 지연이 아니라 그 각도에서 아예 수렴을 포기한 상태로 정지.

즉 **작은/중간 각도(대략 45~60도 이하)에서는 정상 작동하고, 그보다 큰 각도로 꺾으면 재현율 100%로
깨진다.** 아래를 전부 실측으로 배제했지만 진짜 원인은 특정 못함 — 다음 세션에서 이어서 조사할 것:
- target이 팔 최대 리치(rigid 계산 0.61 유닛, 이번 재현에서 shoulderToTarget=0.289) 안에 있음 —
  reach 초과 아님. 이론상 law-of-cosines로 항상 해가 존재해야 하는 범위인데 못 찾는 것이 이상함.
- `targetPositionWeight=1`, `weight=1`, RigLayer/Rig `weight=1`, `active=true` 모두 확인됨
- `maintainTargetPositionOffset`/`maintainTargetRotationOffset` 둘 다 false
- `hintWeight`를 0→1로 바꿔도(큰 각도 재현 케이스에서 직접 테스트) 오차 완전히 동일 — 힌트가
  원인이 아님이 확실함
- `RigBuilder.Build()`를 그 상태에서 다시 호출해도 오차 그대로 — 그래프 재빌드로도 안 풀림
- `Animator.Update(0f)`를 수동으로 추가 호출하면 오히려 정상이던 상태(오차 ~0)가 즉시 0.26으로
  깨짐 — MCP로 코드 실행하며 직접 `Animator.Update()`/`RigBuilder.Build()`를 수동 호출하는 진단
  행위 자체가 상태를 오염시킬 수 있으므로, **다음에 이 문제를 다시 팔 때는 수동 Update/Build 호출을
  섞지 말고 자연스러운 프레임 진행만으로 재현할 것** (이번 최종 재현은 그렇게 했음 — 위 135도
  케이스는 회전 1회 호출 + 대기만으로 얻은 깨끗한 결과).

다음에 조사할 방향 제안: (1) Elbow_L(mid)의 위치/회전 자체가 올바른 법선각으로 계산됐는지 —
tip만 어긋난 건지 mid부터 어긋난 건지 아직 안 봤음. (2) LeftHint의 위치가 이 특정 회전 각도에서
root-target 축과 거의 일직선이 되어 bend-plane 계산이 특이점(degenerate)에 빠지는지 — 다만
hintWeight=0/1 둘 다 결과가 같았으므로 가능성은 낮아 보임. (3) Animation Rigging 패키지(1.2.0)의
TwoBoneIKConstraintJob 자체 버그 여부를 Unity 공식 이슈 트래커에서 검색.

위쪽 "Animation Rigging 패키지로 손 IK/조준 재구현" 항목에 이미 기록된 오른손 IK 자기참조 비수렴
문제(62도 어긋남, 그래서 오른손은 IK 대신 리지드 페어런팅 유지)와 같은 계열의 증상일 가능성이 있다 —
`LeftHandGrip`도 `Hand_R` 본 아래 `WeaponSocket`/`Weapon` 4단 깊이에 물려있어서, 애니메이션으로 계속
움직이는 본 체인 아래 깊이 중첩된 타겟을 Two Bone IK가 큰 회전 상태에서 완전히 수렴시키지 못하는 게
이 리그 구조 자체의 한계일 수 있다. 다만 자연스러운 조준 회전 범위(대략 ±20도 이내로 추정)에서는
`(-28.0, 0.02, 5.0)` 기본 포즈 실측처럼 오차가 사실상 0이었으므로, 실전 조준 범위에서는 육안으로
문제되지 않을 가능성이 높다 — 실제 게임 내 조준 범위로 먼저 테스트해보고 그래도 거슬리면 추가 조사할 것.

(2026-09-01 갱신: 사용자 확인 결과 왼손/오른손 IK 모두 현재 잘 동작 중. 위 수렴 버그는 재현은 됐지만
실전에서 막힌 적은 없는 것으로 보임 — 아래 새 항목의 재설계 작업 중 조준 각도가 커지면 다시 걸릴 수
있으니 그때 가서 대응하기로 함.)

## [완료] WeaponAimAlign을 RigBuilder 그래프 안으로 이전 (2026-09-01)

### 진단
`WeaponAimAlign.cs`(순수 Update() 스크립트, 무기를 pivot 기준 RotateAround로 직접 회전시킴)가
무기의 부모 본(현재 확인된 소켓 기준 `Clavicle_R`/`Hand_R`, 즉 애니메이션으로 매 프레임 움직이는
스켈레톤 체인)이 **이번 프레임 최종 포즈로 갱신되기 전에** 실행된다. Unity 프레임 순서는
`Update → (Animator+Rig 평가) → LateUpdate`이므로, `WeaponAimAlign.Update()`가 읽는
`refFireTr.forward`/무기 트랜스폼은 항상 지난 프레임 본 포즈 기준 — 총이 이동 애니메이션으로
흔들리는 동안 조준 보정도 한 프레임 밀린 값으로 계산됨. Aim 튜토리얼([#04] Unity Animation
Rigging - Aiming a weapon)이 경고하는 "부착 콘스트레인트가 조준 콘스트레인트보다 먼저 평가돼야
한다"는 원칙을 정확히 거꾸로 밟고 있는 구조. `Aim.cs`가 `Camera.main`을 Update에서 읽어 카메라
갱신(LateUpdate)보다 한 프레임 앞서는 문제까지 겹쳐서, 실질적으로 카메라 지연 + 본 지연 이중으로
밀린 데이터를 쓰고 있음.

관련 과거 기록: 이 프로젝트는 이미 한 번(2026-08-28) `AimRigTarget.cs`로 Multi-Aim Constraint 기반
재설계를 시도했다가 코드까지 다 만들어놓고 중단된 이력이 있음(현재 `AimRigTarget.cs` 파일 없음 —
삭제됨). 왜 중단됐는지는 기록에 없고, 사용자도 명확한 이유는 기억 못 함 — 다만 당시 걸림돌로 보였던
왼손/오른손 IK는 현재(2026-09-01) 둘 다 정상 동작 확인됨.

### 실제 구현한 방향 — 영상([#04] Aiming a weapon using Multi Aim Constraint) 그대로,
### 커스텀 IAnimationJob 아님, 내장 Multi-Aim Constraint 사용 (UnityMCP로 직접 수행, 검증 완료)
당초 계획했던 커스텀 `IAnimationJob` 대신, 영상이 실제로 쓰는 `com.unity.animation.rigging` 내장
`MultiAimConstraint`로 구현 — 더 단순하고 코드도 덜 필요함.

1. `AimTargetFollower.cs`(신규) — `Aim.TargetPosition`을 자기 위치로 복사만 하는 얇은
   MonoBehaviour. `AimTargetPoint` GameObject에 부착.
2. `WeaponAimAlign.cs` 축소 완료 — 트랜스폼 직접 조작 코드 전부 제거. 줌 여부에 따라 주입받은
   `MultiAimConstraint`의 `weight`만 `MoveTowards`로 블렌딩.
3. `WeaponRigTarget.cs`에 `m_refWeaponAimConstraint` 필드 추가. `SetWeapon()`이 장착 시점에
   `constrainedObject`를 이번 무기 루트로 갈아끼우고, 그 무기의 `WeaponAimAlign`에
   `SetAimConstraint()`로 콘스트레인트 참조를 주입.
4. `Player.cs` `EquipWeapon()` → `WeaponRigTarget.SetWeapon()` 호출에 무기 루트 Transform 인자 추가.
5. Unity 씬(UnityMCP `execute_code`/`manage_gameobject`/`manage_components`로 직접 수행):
   - `Player/Visual` 밑에 `AimTargetPoint`(`AimTargetFollower` + `RigTransform`) 생성.
   - `Player/Visual/RigLayer` 밑에 `WeaponAimIK`(`MultiAimConstraint`) 생성, **sibling index 0**으로
     이동시켜 `LeftHandIK`/`RightHandIK`보다 먼저 평가되게 함(`transform.SetSiblingIndex(0)`).
   - `sourceObjects`에 `AimTargetPoint` 등록(weight 1). `aimAxis`는 실측 결과 기본값 Z가 정확히
     일치(FireTr.forward를 무기 루트 로컬 공간으로 변환하면 정확히 (0,0,1)) — 별도 설정 불필요.
   - `WeaponRigTarget.m_refWeaponAimConstraint`, `Aim.m_refCameraPitchTr`(→CameraPivot3D),
     `AimTargetFollower.m_refAim`(→Player의 Aim) 각각 배선.
6. **회전 기준점은 무기 루트 자체**로 결정(사용자 확정) — 기존 `WeaponAimAlign.m_refPivotTr`이
   가리키던 개머리판 안 `Pivot` 자식(무기 루트에서 약 0.26 유닛 떨어짐)은 더 이상 안 씀. Multi-Aim
   Constraint는 Constrained Object 자신의 위치만 축으로 쓸 수 있어서 영상과 동일하게 감 — 프리팹
   재구조화 없음. 시각적으로 회전 축이 살짝 달라지는 트레이드오프는 승인됨.
7. (관련 이슈, 같이 처리) `Aim.cs`가 `Camera.main` 대신 `PlayerMovement`의 `CameraPivot3D`를
   기준으로 레이캐스트하도록 변경 + `[DefaultExecutionOrder(10)]`로 `PlayerMovement`보다 항상
   나중에 실행되도록 고정. 카메라 쪽 프레임 지연도 같이 제거됨.

### 삽질 기록 — TransformStreamHandle cannot be resolved
`AimTargetPoint`를 처음에 `Player` 루트 바로 밑(Visual 밖)에 만들었더니 Play 모드에서
`System.InvalidOperationException: The TransformStreamHandle cannot be resolved.`가 매 프레임
발생(Burst job, `RigSyncSceneToStreamJob.TransformSyncer.Sync`). `RigTransform` 컴포넌트를 추가해도
그 자체로는 안 고쳐짐(런타임 로직이 없는 빈 마커라 이것만으론 스트림 바인딩이 해결 안 됨). 이미 잘
동작하던 `LeftHint`/`RightHint`(TwoBoneIKConstraint의 hint로 쓰이는, 마찬가지로 SyncSceneToStream
대상)가 전부 `Player/Visual` **밑**에 있는 걸 확인하고 `AimTargetPoint`도 `Player/Visual` 밑으로
옮기니 즉시 해결됨. **결론: Rig 콘스트레인트가 참조하는 SyncSceneToStream 대상 Transform은
Animator의 avatarRoot(`Visual`) 하위에 있어야 한다 — Player 루트 바로 밑은 안 됨.**

### 검증 결과 (UnityMCP `execute_code`로 Play 모드 직접 측정, 2026-09-01)
- `Player.PickupWeapon()`(리플렉션으로 강제 호출, 씬에 `GameCameraManager`/`InputManager`
  부트스트랩이 없어 일부 경로는 우회) → 장착 후 `constrainedObject` = 정확히 장착된 무기 이름으로
  확인됨(런타임 배선 정상).
- `WeaponAimAlign.Zoom = true` 후 1초 대기 → `constraint.weight` 0→1.000 정상 블렌딩,
  `FireTr.forward`와 "무기→AimTargetPoint 방향"의 각도차 **0.00도** (완전 정렬).
- `Zoom = false` 후 1초 대기 → `weight` 1.000→0.000 정상 복귀.
- 같은 상태에서 `Hand_L`과 `LeftHandGripTr`의 거리 **0.00001 유닛** — 왼손 Two-Bone IK가
  WeaponAimIK보다 나중에 평가되면서도(순서 정상) 여전히 완벽하게 추적함, 회귀 없음.
- 콘솔 에러 없음(단, 이 씬을 부트스트랩 씬 없이 단독 Play할 때 `InputManager.m_Instance`/
  `GameCameraManager.m_Instance`가 null이라 `Player.Update()`/`PlayerMovement.Update()`가 매 프레임
  NullReferenceException을 던지는 건 **이번 작업과 무관한 기존 갭** — 부트스트랩 씬과 함께 실행하면
  발생 안 함, 별도 이슈로 남겨둠).

### 남은 리스크 (해결 안 됨)
조준 각도가 커질수록 위 "왼손 Two Bone IK가 총을 안 따라가던 문제" 항목의 미해결 수렴 버그
(60~135도 이상에서 멈춤)에 걸릴 가능성은 여전히 있음 — 이번 검증은 정지 상태 기준 각도라 큰 회전
상태에서의 수렴은 아직 실측 안 함. 실전 조준 범위로 플레이 테스트하며 지켜볼 것.

## [완료 / 리스크 재현됨] 상체(척추) 조준 굴입 추가 — Spine_02/03 Multi-Aim Constraint (2026-09-01)

영상([#04])이 무기 정렬과 별도로 "spine" 노드를 만드는 구간(13분대, "quickly do the other two
nodes so first one is called spine")을 따라, 무기뿐 아니라 상체도 조준 방향으로 굽도록 추가.

### 구현
- `Player/Visual/RigLayer` 밑에 `SpineAimIK_02`(`Spine_02` 대상), `SpineAimIK_03`(`Spine_03` 대상)
  두 개의 `MultiAimConstraint`를 신규 생성. Source Object는 기존 `AimTargetPoint` 재사용.
- **Rig 형제 순서**: `SpineAimIK_02 → SpineAimIK_03 → WeaponAimIK → LeftHandIK → RightHandIK`.
  척추가 부모 체인이라 무기/손 IK보다 먼저 평가되어야 함 — 안 그러면 척추 회전이 이미 정렬된
  무기를 다시 틀어버림.
- `aimAxis` 실측: `Spine_02`는 로컬 +Y, `Spine_03`은 로컬 -Y가 각각 캐릭터 정면과 일치(두 본이
  반대 부호 컨벤션 — Synty 리그 특성으로 보임). `worldUpType`은 None이라 `upAxis`는 기본값 그대로
  둬도 무방(코드상 None일 때 안 쓰임).
- 줌 여부와 무관하게 **항상 켜짐**(weight=1 고정, 사용자 확정) — `WeaponAimIK`처럼 블렌딩 스크립트
  없음.
- 회전 범위는 본당 `limits = (-45, 45)`로 제한(과도한 젖힘 방지, 조율 가능한 값).

### 실측 검증 — 상체 굴입 자체는 정상 동작
Play 모드에서 `AimTargetPoint`를 위/앞으로 이동시켜 확인: `Spine_02`/`Spine_03` 모두
"본→AimTargetPoint 방향"과 aimAxis 사이 각도차 **0.00도** — limits 범위 안에서 완벽하게 정렬됨.

### ⚠️ 리스크가 실제로 재현됨 — 왼손 IK가 "멈춘 채 고정"되는 상태 유발
위 검증 도중, 캐릭터 정면 기준 앞으로 5·위로 4 정도(고도각 약 39도)의 급한 각도로 조준 테스트를
하자 왼손 Two-Bone IK가 즉시 깨졌고(Hand_L~LeftHandGrip 오차 2.5~2.7 유닛, 정상은 0.00001),
**그 뒤로 타겟을 다시 거의 정면(고도각 0)으로 되돌려도 오차가 그대로 2.5 유닛 부근에 고정**되어
회복되지 않았다. 이는 정확히 기존에 기록된 "왼손 Two Bone IK가 총을 안 따라가던 문제" 항목의
증상(특정 각도에서 수렴을 포기하면 프레임이 지나도 스스로 안 풀림)과 같은 버그다.

**중요한 새 정보:** 척추 굴입이 추가되면서 팔이 그 문제 구간에 도달하는 문턱이 낮아졌다 —
무기 회전만 있을 때보다 척추+무기 회전이 합쳐지면서 같은 "화면상 조준 각도"에도 실제 팔 굽힘
각도는 더 커짐. 즉 **이 기능을 추가한 뒤로는 예전보다 더 쉽게, 더 자주 이 버그를 만날 수 있다.**

### 완화 적용 및 재검증 (2026-09-01, 사용자 선택: limits 좁혀서 완화)
`SpineAimIK_02`/`03`의 `limits`를 ±45도 → **±30도**로 축소. Play 모드에서 재검증:

- 처음 IK를 깨뜨렸던 그 각도(정면 기준 앞 5·위 4, 고도각 약 39도)로 다시 테스트 →
  `Hand_L~LeftHandGrip` 오차 **0.00001**(완전 정상), 척추/무기 정렬 오차 전부 0.00도 —
  **문제 각도에서 완전히 복구됨**.
- 더 가파르게(앞 2·위 6, 고도각 약 72도, 거의 수직에 가까움)로 밀어붙이면 여전히 깨짐
  (오차 2.71 유닛) — 즉 ±30도가 "안전 구간"을 넓혀주긴 했지만 완전히 없앤 건 아니고, 실제
  슈팅 게임에서 거의 안 쓰는 극단적으로 가파른 고도각(70도 이상)에서만 남은 리스크로 축소됨.

**결론: ±30도 제한으로 실전 조준 범위(약 40도 이하 고도각)에서는 문제 재현 안 됨.** 더 가파른
각도의 잔여 리스크는 실제 플레이에서 그 정도로 위를 조준할 일이 거의 없다는 전제로 수용하고
넘어감 — 나중에 실제로 문제 되면 왼손 IK 수렴 버그 자체를 다시 파거나 limits를 더 좁힐 것.
