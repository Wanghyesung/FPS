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
