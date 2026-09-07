# 몬스터 Behavior Tree 재설계 계획

> 작성: 2026-09-04 · 대상: `Assets/05 Enemy/`
> 목표 동작: **적 보이면 쏘고 → 안 보이면 순찰 → HP 낮으면 도주 → 은신 후 기습 복귀 → 교전 중 시야 막히면 다른 지역으로 재배치**

---

## 1. 지금 BT가 "이상한" 진짜 이유

현재 트리 배선 (`SO_SelectNode_Root.asset` 기준):

```
SO_SelectNode_Root (Selector)
├─ [0] SO_SequenceNode_Escape : CheckHP(30) → Escape
├─ [1] SO_SequenceNode_Patrol : ResumeMove → CheckPoint → WaitCheckPoint
└─ [2] SO_SequenceNode_Attack : CheckLength(In,40) → Rotate → Zoom → CheckRay → Attack
```

### 원인 1 — Attack 브랜치가 구조상 도달 불가 (가장 큰 문제)

Selector는 자식이 `Success`/`Running`이면 즉시 반환한다. Patrol의 `SOWaitCheckPointNode`는 순찰 지점에 도착할 때까지 계속 `Running`을 반환하므로, **인덱스가 뒤인 Attack은 영원히 평가되지 않는다.** 적이 코앞에 있어도 순찰을 계속한다.

### 원인 2 — 우선순위 인터럽트가 코드 레벨에서 불가능

`SOSelectNode.cs:27` / `SOSequenceNode.cs:25` 는 `Running`을 반환한 자식의 인덱스를 `iCurrentIdx`에 **래치(latch)** 하고 다음 틱에 그 인덱스부터 루프를 시작한다.

```csharp
for (int i = iCurrentIdx; i < listNode.Count; ++i)   // ← 0이 아니라 iCurrentIdx부터
```

즉 Patrol이 도는 동안 index 0의 HP 체크조차 다시 보지 않는다. "적 보이면 쏘고, HP 없으면 도망"은 **상위 조건을 매 틱 재검사해야만** 성립하는데, 현재 구조는 그걸 막고 있다.

Sequence도 같은 문제가 있다. Attack 시퀀스가 index 4(`Attack`, 쿨다운 중 Running)에 래치된 상태에서 중단되면, 다음에 다시 들어올 때 **거리·시야 검사를 건너뛰고 곧바로 발사**한다.

### 원인 3 — 시야 판정이 트리에서 빠져 있음

- `SO_CheckPOVNode_80`(시야각)이 들어간 `SO_SeqLenPov.asset`은 **어느 listNode에서도 참조되지 않는 고아 에셋**이다. 즉 현재 적은 **등 뒤의 플레이어도 감지한다.**
- `BlackBoard.FindTarget`은 `SOCheckRayNode.cs:41`에서 `true`로만 설정되고 **false로 되돌리는 코드가 아무 데도 없다.** 한 번 발견하면 영구히 "발견함" 상태다.
- `SOCheckRayNode.cs:25`에서 눈높이 오프셋이 주석 처리돼 있어 **레이가 발밑에서 출발한다.** 정작 `SO_CheckRayPlayerNode.asset`에는 예전 값 `m_vEyeOffset: {y: 1.5}`가 그대로 남아 있다.
- `SO_CheckRayPlayerNode.asset`의 `m_tCollideMask`는 `m_Bits: 8448` = **Player(8) + Obstacle(13)** 뿐이다. 씬을 실측하니 콜라이더가 **`Default(0)` 223개 / `Obstacle(13)` 165개 / `Ground(11)` 1개**로, **레벨 지오메트리 대부분이 `Default(0)`에 있는데 마스크에서 빠져 있다.** (2026-09-05 정정: 처음에 `Ground` 누락으로 적었으나 실제 원인은 `Default`였다.)
- **타겟 판정이 틀렸다.** `tHit.transform.root == TargetTr.root` 비교를 쓰는데, 씬에서 `Player`와 `Enemy`가 **둘 다 `DnynamicObject`라는 같은 부모** 아래 있어 `root`가 동일하다. 다른 적을 맞혀도 "플레이어를 봤다"가 된다. 지금은 Enemy 레이어가 마스크에서 빠져 있어 우연히 안 터질 뿐.

### 원인 4 — NavMeshAgent 상태가 브랜치 전환 때 누수됨

각 노드가 에이전트 상태를 바꿔놓고 되돌리지 않는다.

| 노드 | 바꾸는 것 | 되돌리는 곳 |
|---|---|---|
| `SORotateNode` | `updateRotation = false` | 없음 |
| `SOEscapeNode` | `speed × 1.3` | 없음 |
| `SOZoomNode` | `Weapon.Zoom()` | 없음 |

`SOResumeMoveNode`가 이걸 한꺼번에 복구하지만 **Patrol 시퀀스 맨 앞에만** 붙어 있다. Patrol을 거치지 않고 Escape ↔ Attack을 오가면 복구가 영영 안 된다.

### 원인 5 — 빌드 차단 및 잔여 쓰레기

- `SOAttackNode.cs:3` 의 `using UnityEditor;` — 사용처가 없는데도 남아 있어 **런타임 어셈블리가 플레이어 빌드에서 컴파일 실패한다.** (`.claude/rules/unity-specifics.md` 위반)
- `SOCheckAngleNode.cs:24` — `Quaternion.LookRotation(vTargetPos)` 에 **방향이 아니라 월드 좌표**를 넣고 있다. (`SORotateNode`에서 이미 고친 것과 동일한 버그)
- `SOFindWeaponNode.cs:10` — `CreateAssetMenu`의 `menuName`이 `SOCheckLength`와 **완전히 중복**되고, 본문은 `NotImplementedException`이다.
- `SOFind.cs` — 빈 MonoBehaviour 템플릿.
- 고아 에셋 5개: `SO_CheckAngleNode`, `SO_CheckLength_In_60_Out`, `SO_IsArrivePoint`, `SO_SeqLenPov`, `SO_TraceMoveNode`.

---

## 2. 확정된 설계 결정

| 항목 | 결정 |
|---|---|
| 도주 후 재교전 | **은신 후 기습** — 엄폐 지점 도착 후 은신 유지, 시간 경과 **또는** 플레이어가 기습 사거리 안에 들어오면 튀어나옴 |
| 재배치 발동 | **시야가 막혔을 때** — 최근에 봤는데 일정 시간 이상 LOS가 끊기면 측면으로 우회 |
| 시야 상실 시 | **마지막 목격 위치 수색** |
| 히트맵 | **연결 지점만** — `PlayerHeatmapRecorder` 연동은 나중에, 지금은 훅만 남김 |

---

## 3. 새 트리 구조

```
Root (Sequence)
├─ [0] SOPerceptionNode                        매 틱 지각 갱신, 항상 Success
└─ [1] Sel_Main (Selector, 매 틱 0번부터 재평가)
    │
    ├─ [0] Seq_Escape ─────────────── HP 낮음 → 도주 → 은신 → 기습
    │   ├─ SOCheckEscapeNode          HP<=임계 && (IsEscaping or Time>=NextEscapeTime)
    │   ├─ SOEscapeMoveNode           엄폐 지점 선정 + 이동 (도착까지 Running)
    │   └─ SOHideNode                 은신 유지 → 기습 조건 충족 시 Success
    │
    ├─ [1] Seq_Combat ─────────────── 보이면 교전
    │   ├─ SOCheckFindTargetNode      BB.FindTarget == true
    │   └─ Sel_Engage (Selector)
    │       ├─ Seq_Fire (Sequence)    사거리 안 → 제자리 사격
    │       │   ├─ SOCheckLength(In,40)
    │       │   ├─ SOStopMoveNode
    │       │   ├─ SORotateNode
    │       │   ├─ SOZoomNode
    │       │   └─ SOAttackNode
    │       └─ SOTraceMoveNode        사거리 밖 → 접근 (Running)
    │
    ├─ [2] Seq_Reposition ─────────── 근거리인데 시야가 계속 막힘 → 측면 우회
    │   ├─ SOCheckRepositionNode
    │   └─ SORepositionMoveNode
    │
    ├─ [3] Seq_Search ─────────────── 마지막 목격 위치 수색
    │   ├─ SOCheckSearchNode
    │   └─ SOSearchNode
    │
    └─ [4] Seq_Patrol ─────────────── 그 외 전부
        ├─ SOResumeMoveNode           (안전망 — 값이 다를 때만 write)
        ├─ SOPatrolMoveNode
        └─ SOWaitCheckPointNode
```

### 우선순위 순서의 근거

- **Escape가 최상위** — HP는 어떤 상황에서도 즉시 인터럽트해야 한다.
- **Combat이 Reposition보다 위** — "적 보이면 쏜다"가 강한 규칙이다. 재배치 중에 플레이어가 다시 보이면 즉시 사격으로 전환된다.
- **Reposition이 Search보다 위** — 둘 다 "안 보임"에서 발동하지만 조건이 겹치지 않게 나눈다.
  - Reposition: `최근(3초 이내) 목격 && 아직 교전 사거리 안 && LOS 차단 N초 지속` → *"저 벽 뒤에 있다"*
  - Search: 그 외의 목격 기억 (더 오래됐거나 사거리 밖) → *"어디 갔지"*
- **Patrol이 최하위** — 기본 상태.

---

## 4. 엔진 변경: `Abort()` 도입 + 매 틱 재평가

이번 재설계의 핵심이다. 두 가지를 동시에 바꾼다.

### 4-1. 인덱스 래치 제거 (반응형 Selector / Sequence)

```csharp
for (int i = 0; i < listNode.Count; ++i)   // iCurrentIdx → 0
```

- **Selector**: 매 틱 0번부터 재평가 → 상위 조건이 항상 먼저 검사된다. (원인 1·2 해결)
- **Sequence**: 매 틱 0번부터 재평가 → 앞쪽 조건 노드가 가드로 계속 동작한다. "적이 사라졌는데 계속 쏘는" 문제가 구조적으로 사라진다.

대신 **모든 액션 노드는 매 틱 다시 실행돼도 안전(idempotent)해야 한다.** `SetDestination`처럼 매 틱 호출하면 경로를 다시 계산하는 API는 "목표가 실제로 바뀌었을 때만" 호출하도록 가드를 넣는다.

### 4-2. `SONode.Abort()` 추가

```csharp
public abstract class SONode : ScriptableObject
{
    public abstract eNodeState Execute(BlackBoard _refBB);

    // 상위 우선순위 브랜치에 밀려 이번 틱에 실행되지 않을 때 호출.
    // 이 노드가 바꿔놓은 에이전트/무기 상태를 되돌리는 자리.
    public virtual void Abort(BlackBoard _refBB) { }
}
```

Composite는 직전 틱에 `Running`이던 자식 인덱스를 기억했다가, 이번 틱에 다른 브랜치가 이기면 그 자식에게 `Abort()`를 전파한다.

```csharp
public sealed class SOSelectNode : SOListNode
{
    private int iRunningIdx = -1;   // 클론 인스턴스별 상태 (Composite는 BT 진입 시 Instantiate됨)

    public override eNodeState Execute(BlackBoard _refBB)
    {
        for (int i = 0; i < listNode.Count; ++i)
        {
            eNodeState eState = listNode[i].Execute(_refBB);
            if (eState == eNodeState.Failure)
                continue;

            // 우선순위 인터럽트의 핵심 — 밀려난 브랜치가 남긴 상태를 되돌린다
            if (iRunningIdx != -1 && iRunningIdx != i)
                listNode[iRunningIdx].Abort(_refBB);

            iRunningIdx = (eState == eNodeState.Running) ? i : -1;
            return eState;
        }

        AbortRunning(_refBB);
        return eNodeState.Failure;
    }

    public override void Abort(BlackBoard _refBB) => AbortRunning(_refBB);

    private void AbortRunning(BlackBoard _refBB)
    {
        if (iRunningIdx == -1)
            return;
        listNode[iRunningIdx].Abort(_refBB);
        iRunningIdx = -1;
    }
}
```

이걸로 원인 4가 근본 해결된다:

| 노드 | `Abort()`에서 되돌리는 것 |
|---|---|
| `SORotateNode` | `Agent.updateRotation = true` |
| `SOZoomNode` | `Weapon.UnZoom()` |
| `SOStopMoveNode` | `Agent.isStopped = false` |
| `SOEscapeMoveNode` | `Agent.speed = ObjInfo.Speed`, `IsEscaping = false` |
| `SOHideNode` | `Agent.isStopped = false` |
| `SORepositionMoveNode` | `IsRepositioning = false` |
| `SOSearchNode` | `IsSearching = false` |

> **알려진 한계(허용):** 이긴 브랜치를 먼저 `Execute`한 뒤에 진 브랜치를 `Abort`하므로, 두 브랜치가 같은 값을 건드리면 **1프레임 동안** 값이 어긋날 수 있다. 반응형 Sequence 덕분에 이긴 브랜치가 다음 틱에 자기 설정을 다시 세우므로 자연히 복구된다. 그래서 "상태를 바꾸는 노드는 매 틱 자기 값을 다시 세운다"가 규칙이다.

### 4-3. SO 오염 방지 (프로젝트 최우선 규칙)

`BehaviorTree.Awake()`의 `CloneChildren`은 **`SOListNode`(Composite)만 복제**하고 leaf는 원본 에셋을 공유한다. 따라서:

- Composite(`iRunningIdx`)에 진행 상태를 두는 것은 **허용** — 몬스터별 클론이다.
- **leaf 액션 노드는 절대 인스턴스 상태를 갖지 말 것.** 타이머·좌표·플래그는 전부 `BlackBoard`로. 여러 몬스터가 같은 원본 SO를 공유하므로 즉시 오염된다.

---

## 5. BlackBoard 확장

```csharp
[Header("Perception")]
public bool FindTarget;         // 이번 틱에 실제로 보이는지 — SOPerceptionNode가 매 틱 갱신
public float POV;
public bool HasLastSeen;        // 목격 기억이 유효한지
public Vector3 LastSeenPos;     // 마지막으로 본 위치
public float LastSeenTime;      // 마지막으로 본 시각 (Time.time)
public float BlockedSinceTime;  // 시야가 끊기기 시작한 시각 (0 = 안 끊김)

[Header("Escape")]
public bool IsEscaping;
public Vector3 EscapePos;
public float NextEscapeTime;    // 이 시각 전까지는 HP가 낮아도 재도주 안 함
public float HideEndTime;       // 은신 종료 예정 시각

[Header("Reposition")]
public bool IsRepositioning;
public Vector3 RepositionPos;
public float NextRepositionTime;

[Header("Search")]
public bool IsSearching;
public Vector3 SearchPos;
public float SearchEndTime;
```

기존 `FindTarget` / `IsEscaping` / `EscapePos` / `NextEscapeTime`은 이름 그대로 유지 → **씬에 직렬화된 값이 깨지지 않는다.** 필드를 새로 추가하는 것만으로는 기존 데이터가 손실되지 않지만, **이름을 바꾸는 경우에는 반드시 `[FormerlySerializedAs]`를 붙일 것** (`.claude/rules/serialization.md`).

---

## 6. 노드 작업 목록

### 신규 (9개)

| 파일 | 역할 | 반환 |
|---|---|---|
| `SOPerceptionNode.cs` | 거리·시야각·LOS를 **한 번에** 판정해 BB 갱신. 눈높이 오프셋 포함 | 항상 Success |
| `SOCheckFindTargetNode.cs` | `BB.FindTarget` 확인 (`m_bInvert`로 "안 보임"도 처리) | S / F |
| `SOStopMoveNode.cs` | 제자리 사격용 정지. `Abort()`에서 해제 | Success |
| `SOCheckEscapeNode.cs` | HP 임계 + 재도주 쿨다운 + 진행 중 여부 복합 판정 | S / F |
| `SOHideNode.cs` | 은신 유지 → 시간 경과 or 기습 사거리 진입 시 종료 | Running → Success |
| `SOCheckRepositionNode.cs` | 재배치 조건 판정 | S / F |
| `SORepositionMoveNode.cs` | `LastSeenPos` 기준 **측면** 우회 지점 선정 + 이동 | Running → Success |
| `SOCheckSearchNode.cs` | 목격 기억 유효성 (만료 시 `HasLastSeen=false`) | S / F |
| `SOSearchNode.cs` | `LastSeenPos`로 이동 → 도착 후 두리번 → 기억 소거 | Running → Success |

### 수정 (10개)

| 파일 | 내용 |
|---|---|
| `BehaviorTree.cs` | `SONode.Abort()` 추가, BlackBoard 필드 확장 |
| `SOSelectNode.cs` | 래치 제거 → 매 틱 재평가 + Abort 전파 |
| `SOSequenceNode.cs` | 동일 |
| `SOAttackNode.cs` | **`using UnityEditor;` 제거 (빌드 차단)** |
| `SORotateNode.cs` | `Abort()`에서 `updateRotation` 복원 |
| `SOZoomNode.cs` | `Abort()`에서 `UnZoom()` |
| `SOEscapeNode.cs` | 이동 전담으로 축소. 은신 단계는 `SOHideNode`로 분리, `Abort()` 추가 |
| `SOResumeMoveNode.cs` | 매 틱 실행 대비 — **값이 다를 때만 write** |
| `SOCheckPointNode.cs` | 같은 목표면 `SetDestination` 재호출 안 함, `Debug.Log` 제거 |
| `SOTraceMoveNode.cs` | `SetDestination` 반환값 확인 + 목표 갱신 임계값 (매 틱 경로 재계산 방지) |

> **파일명/클래스명은 그대로 두는 쪽을 권장한다.** 클래스명을 바꾸면 기존 `.asset`의 `m_Script` GUID가 깨져 배선을 전부 다시 해야 한다. 역할이 바뀌는 두 노드(`SOEscapeNode` → 이동 전담, `SOCheckPointNode` → 순찰 이동)는 이름을 유지하고 파일 상단 목적 주석만 갱신한다.

### 삭제 (4개)

| 대상 | 이유 |
|---|---|
| `SOFind.cs` | 빈 MonoBehaviour 템플릿 |
| `SOFindWeaponNode.cs` | `NotImplementedException` + `CreateAssetMenu` 이름이 `SOCheckLength`와 중복 |
| `SOCheckAngleNode.cs` | 좌표/방향 버그가 있고 트리에서 안 쓰임 (`SORotateNode`가 대체) |
| `SOIsArrivePointNode.cs` | `SOWaitCheckPointNode`와 기능 중복, 고아 |

고아 에셋도 함께 정리: `SO_CheckAngleNode`, `SO_CheckLength_In_60_Out`, `SO_IsArrivePoint`, `SO_SeqLenPov`
(단 `SO_TraceMoveNode`는 새 Combat 브랜치에서 **다시 사용**하므로 남길 것)

---

## 7. 단계별 구현 순서

각 단계가 끝날 때마다 **플레이 가능한 상태**를 유지한다.

### 1단계 — 엔진 정상화 + 최소 순서 교정
1. `SOAttackNode.cs`의 `using UnityEditor;` 제거
2. `SONode.Abort()` 추가
3. `SOSelectNode` / `SOSequenceNode` 래치 제거 + Abort 전파
4. `SORotateNode` / `SOZoomNode` / `SOEscapeNode`에 `Abort()` 구현
5. 매 틱 재평가에 대비한 가드 — `SOCheckPointNode`(같은 목표면 `SetDestination` 재호출 안 함),
   `SOResumeMoveNode`(값이 다를 때만 write)
6. **SO 배선 순서 교정 2건**
   - `SO_SelectNode_Root` : `[Escape, Patrol, Attack]` → **`[Escape, Attack, Patrol]`**
   - `SO_SequenceNode_Attack` : `[CheckLength, Rotate, Zoom, CheckRay, Attack]`
     → **`[CheckLength, CheckRay, Rotate, Zoom, Attack]`**

> 래치만 걷어내면 HP 인터럽트는 살아나지만 **Attack은 여전히 도달 불가**다 — Patrol이 Selector에서
> 앞자리를 차지한 채 계속 Running을 반환하기 때문. 그래서 1단계에는 순서 교정이 반드시 함께 들어간다.
> Attack 시퀀스 안에서 `CheckRay`를 앞으로 당기는 것도 같은 이유다. 뒤에 있으면 벽 너머의 플레이어를
> 향해 회전·조준을 다 하고 나서야 실패해, 매 프레임 Patrol과 Attack 사이를 오간다.

**검증:** 적이 순찰 중 플레이어를 보면(사거리 40 안 + 시야 트임) 사격으로 전환돼야 한다.
HP를 30 이하로 깎으면 순찰 도중에도 즉시 도주로 인터럽트돼야 한다. 도주가 끝난 뒤 이동 속도가
원래 값으로 돌아오는지도 확인할 것(`SOEscapeNode`의 1.3배 배율 누수).

### 2단계 — 지각 통합 ✅ 완료 (2026-09-05)
1. `SOPerceptionNode` 작성 (거리 + 시야각 + LOS + 눈높이 1.6)
2. BlackBoard에 `HasLastSeen` / `LastSeenPos` / `LastSeenTime` / `BlockedSinceTime` / `TargetRoot` 추가
3. Root를 `Sequence[SO_PerceptionNode, SO_SelectNode_Root]`으로 교체 (`SO_SequenceNode_Root.asset` 신규)
4. `SOCheckFindTargetNode` 작성 — Attack 시퀀스의 `CheckRay` 자리를 대체
5. LayerMask `11009`, `Enemy.Awake`에서 `TargetRoot` 주입

**작업 중 추가로 잡은 것**
- 타겟 판정을 `root` 비교 → `IsChildOf(TargetRoot)`로 교체. 씬에서 `Player`와 `Enemy`가 같은
  부모(`DnynamicObject`) 아래라 `root`가 같아서, 다른 적을 맞혀도 "봤다"가 되는 상태였다.
- LayerMask 누락 레이어는 `Ground`가 아니라 `Default`였다 (콜라이더 223개).

**검증(플레이 테스트 필요):** 등 뒤에 서면 감지되지 않아야 한다. 벽 뒤로 숨으면 `FindTarget`이
`false`로 돌아와야 한다(이전에는 영구히 true).

### 3단계 — 교전 브랜치
1. `SOStopMoveNode` 작성
2. `Seq_Combat` / `Sel_Engage` / `Seq_Fire` 배선, `SOTraceMoveNode` 재사용
3. Selector 순서를 Escape → Combat → Patrol로

**검증:** 사거리 밖에서 보이면 접근, 안에 들어오면 멈춰서 조준 사격.

### 4단계 — 도주·은신·기습
1. `SOCheckEscapeNode` / `SOHideNode` 작성, `SOEscapeNode`를 이동 전담으로 축소
2. `Seq_Escape` 3단 배선

**검증:** HP 30 이하 → 반대 방향 엄폐 지점으로 달림 → 도착 후 은신 → 시간 경과 또는 플레이어 접근 시 튀어나와 사격 → 쿨다운 후 다시 도주.

### 5단계 — 수색·재배치
1. `SOCheckSearchNode` / `SOSearchNode` / `SOCheckRepositionNode` / `SORepositionMoveNode` 작성
2. Selector에 `[2] Reposition`, `[3] Search` 삽입

**검증:** 교전 중 벽 뒤로 숨으면 몇 초 후 측면으로 우회. 멀리 도망가면 마지막 목격 위치까지 수색 후 순찰 복귀.

### 6단계 — 정리
1. 삭제 대상 스크립트/고아 에셋 제거
2. Enemy를 **프리팹화** (아래 8절 참고)

---

## 8. 개발자 수동 작업 (Unity 에디터 — 에이전트가 대신 못 함)

`.claude/rules/performance.md`의 "개발자 액션 아이템" 규칙에 따라 명시한다. **이 작업들이 끝나야 위 코드가 동작한다.**

### 8-1. Enemy 프리팹화 (우선순위 높음)
현재 Enemy는 `Assets/Scenes/BattleScene.unity`에 **직접 배치**돼 있어 BT 배선(`PatrolList` 4개, RootNode)이 씬에만 존재한다. 적을 하나 늘릴 때마다 손으로 다시 배선해야 하고, 오브젝트 풀링을 붙일 수도 없다.

1. Hierarchy에서 Enemy 오브젝트를 `Assets/05 Enemy/Prefabs/`로 드래그
2. `PatrolList`는 씬의 Transform을 가리키므로 프리팹에 담기지 않는다 → **순찰 지점을 씬의 스포너가 주입하는 구조**로 바꿀 것 (`Enemy.Awake`에서 `FindObjectOfType<Player>()` 하는 것과 같은 자리에서 처리)

### 8-2. 눈높이 기준점 — ✅ 2단계에서 해결됨
`SOPerceptionNode`의 `m_fEyeHeight`(1.6)로 처리했다. `BlackBoard.OwnerOffset`이 채워져 있으면
그쪽을 우선 쓰고, 비어 있으면 `Owner.position + up * 1.6`을 눈 위치로 삼는다 — 씬 배선 불필요.

### 8-3. LayerMask 재설정 — ✅ 2단계에서 해결됨
`SO_PerceptionNode.asset`의 `m_tCollideMask = 11009`로 설정.

| 레이어 | 포함 | 이유 |
|---|---|---|
| `Default(0)` | ✅ | 씬 콜라이더 223개 — 레벨 지오메트리 대부분이 여기 있다 |
| `Player(8)` | ✅ | 타겟 몸통 콜라이더 |
| `Head(9)` | ✅ | 타겟 헤드샷 히트박스 |
| `Ground(11)` | ✅ | 바닥 (콜라이더 1개) |
| `Obstacle(13)` | ✅ | 엄폐물 165개 |
| `Enemy(12)` | ❌ | 자기 자신/동료에게 시야가 막힌다 |
| `Weapon(14)` | ❌ | 눈앞의 자기 무기가 시야를 막는다 |

### 8-4. SO 에셋 재직렬화
일부 `.asset` YAML에 나중에 추가된 필드의 키가 아예 없다.
- `SO_Rotate_30.asset` → `m_fRotateDiff` 없음
- `SO_EscapeNode.asset` → `m_fEscapeCooldown` 없음

각 에셋을 인스펙터에서 한 번 열어 값을 확인하고 저장해 직렬화를 맞춰둘 것. (의도한 값이 실제로 들어 있는지 눈으로 확인)

### 8-5. 신규 SO 에셋 생성 + 배선
9개 신규 노드 각각에 대해 `Create → Game/Monster/ActionNode/...`로 에셋을 만들고, Composite 에셋의 `listNode`에 3절 트리 구조대로 배선한다.

---

## 9. 히트맵 연결 지점 (지금은 훅만)

`PlayerHeatmapRecorder`는 현재 **CSV 저장 전용**이고 런타임 조회 API가 없다. 연동하려면 나중에 다음이 필요하다:

1. **읽기 API 추가** — `public bool TryGetWeight(Vector3 _vWorldPos, out float _fWeight)`
   워커 스레드가 `m_hashWeight`를 쓰고 있으므로 **락 또는 스냅샷 복사**가 필요하다. 메인 스레드에서 그냥 읽으면 `Dictionary` 동시 접근으로 터진다.
2. **훅을 남길 지점 3곳**
   - `SOCheckPointNode` — 다음 순찰 지점 선택. 지금은 `PatrolIdx++` 순차. 나중에 "가중치 **높은**(플레이어가 자주 다니는) 곳" 우선으로 교체
   - `SOEscapeNode.TryPickEscapePos` — 후보 지점 중 가중치 **낮은**(플레이어가 안 다니는) 곳 선호 → 더 잘 숨음
   - `SORepositionMoveNode` — 같은 방식으로 우회 지점 평가

**설계 방침:** 지점 선택 로직을 노드 안에 하드코딩하지 말고 `[SerializeField] private SOPointSelector m_SOSelector;` 로 빼둔다. 기본 구현 `SOSequentialSelector`(현재 동작)를 넣어두면, 나중에 `SOHeatmapSelector`를 만들어 인스펙터에서 갈아끼우는 것만으로 히트맵 순찰이 된다. 트리 구조는 건드리지 않는다.

---

## 10. 리스크 / 주의사항

| 리스크 | 대응 |
|---|---|
| 반응형 Sequence로 바뀌면 모든 액션이 매 틱 재실행됨 | `SetDestination` 계열은 "목표가 실제로 바뀌었을 때만" 호출하도록 가드. `SOResumeMoveNode`도 값이 다를 때만 write |
| leaf 노드에 상태를 두면 모든 몬스터가 오염됨 | 타이머·좌표·플래그는 **전부 BlackBoard**로. Composite만 클론된다 |
| 클래스명 변경 시 `.asset`의 `m_Script` GUID가 깨짐 | 이름 유지하고 내부만 고칠 것 |
| 직렬화 필드 이름 변경 | `[FormerlySerializedAs]` 필수 — 없으면 씬/프리팹 값이 조용히 초기화됨 |
| 1프레임 상태 어긋남 (Abort가 Execute 뒤에 도는 구조) | 상태를 바꾸는 노드는 매 틱 자기 값을 다시 세운다 (4-2절) |
| `Enemy.Awake`의 `FindObjectOfType<Player>()` | 적이 늘어나면 스폰 때마다 씬 전체 스캔. 스포너가 주입하는 구조로 바꿀 것 |
| 적 다수 스폰 시 지각 비용 | `SOPerceptionNode`가 SphereCast를 **적당 1회/틱**으로 통합하므로 현재(브랜치마다 개별 레이)보다 오히려 싸진다. 그래도 부족하면 지각 갱신 주기를 0.1초로 늘릴 것 |
