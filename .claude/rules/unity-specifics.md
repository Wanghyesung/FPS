# Unity 특화 규칙

## 에디터 vs 런타임

```csharp
// 런타임 코드 (Assets/Scripts/) — UnityEditor를 가드 없이 절대 사용하지 마세요
#if UNITY_EDITOR
using UnityEditor;
#endif

private void OnValidate()
{
    #if UNITY_EDITOR
    EditorUtility.SetDirty(this);
    #endif
}
```

- `Editor/` 폴더 안의 코드: 에디터 전용이며, 빌드에서 자동으로 제외됩니다
- `Editor/` 밖의 코드: `UnityEditor` 사용은 반드시 `#if UNITY_EDITOR`로 가드해야 합니다
- 가드를 빠뜨리면: 에디터에서는 컴파일되지만, **빌드에서 실패**하며 빌드 시점까지 아무런 경고도 없습니다

## 플랫폼 정의

```csharp
// 좋은 예 — 항상 폴백을 제공함
#if UNITY_ANDROID
    string dataPath = Application.persistentDataPath;
#elif UNITY_IOS
    string dataPath = Application.persistentDataPath;
#else
    string dataPath = Application.dataPath;
#endif

// 나쁜 예 — 다른 플랫폼에서 코드가 조용히 제외됨
#if UNITY_ANDROID
    SetupMobileControls();
#endif
```

## `?.` 연산자의 함정

```csharp
// 위험함 — Unity의 파괴된 오브젝트 감지를 우회함
_target?.TakeDamage(10);  // 파괴된 오브젝트에서도 TakeDamage를 호출함!

// 안전함 — Unity의 == 연산자가 파괴된 오브젝트를 감지함
if (_target != null)
{
    _target.TakeDamage(10);
}
```

Unity는 파괴된 오브젝트를 null과 비교할 때 `true`를 반환하도록 `==`를 오버라이드합니다. `?.` 연산자는 C# 참조 동등성을 사용하는데, 이는 파괴된 오브젝트를 감지하지 못합니다. 이것이 가장 미묘한 Unity 버그 1위입니다.

## 생명주기 순서

```
Awake()       → 오브젝트가 생성될 때 한 번 호출됨 (비활성화 상태여도)
OnEnable()    → 오브젝트가 활성화될 때 호출됨
Start()       → 첫 Update 전에 한 번 호출됨 (활성화된 경우에만)
FixedUpdate() → 물리 틱 (기본값 0.02초)
Update()      → 매 프레임
LateUpdate()  → 모든 Update 이후, 매 프레임
OnDisable()   → 오브젝트가 비활성화될 때 호출됨
OnDestroy()   → 오브젝트가 파괴될 때 호출됨
```

- 오브젝트 간 Awake 순서에 의존하지 마세요 — `[DefaultExecutionOrder]`나 명시적 초기화를 사용하세요
- `OnDisable`은 `OnDestroy` 이전에 호출됩니다 — 이벤트 구독 해제는 `OnDisable`에서 하세요
- 오브젝트가 한 번도 활성화되지 않으면 `Start`는 호출되지 않습니다

## 스레딩

Unity API는 메인 스레드 전용입니다. 백그라운드 스레드에서는 다음을 할 수 없습니다:
- `Transform`, `GameObject`, `Component`에 접근
- `Instantiate`, `Destroy` 호출
- `Time`, `Input`, `Physics`에 접근

```csharp
// UniTask로 메인 스레드로 돌아가기:
await UniTask.SwitchToMainThread();

// 또는 SynchronizationContext로:
SynchronizationContext.Current.Post(_ => { /* 여기서 Unity API 사용 */ }, null);
```

## 코루틴 금지 — UniTask 사용

`StartCoroutine` / `IEnumerator` / `yield return`을 사용하지 마세요. 모든 비동기 작업에 UniTask를 사용하세요.

UniTask가 해결하는 코루틴의 문제점:
- `gameObject.SetActive(false)`가 되면 코루틴은 조용히 멈추고 재개되지 않습니다
- 코루틴은 취소, 에러 처리, 반환값이 없습니다
- 코루틴은 힙에 할당됩니다

```csharp
// 나쁜 예 — 코루틴
private IEnumerator WaitAndDo()
{
    yield return new WaitForSeconds(1f);
    DoSomething();
}

// 좋은 예 — UniTask
private async UniTask WaitAndDoAsync(CancellationToken token)
{
    await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: token);
    DoSomething();
}
```

항상 `CancellationToken`을 전달하세요. View에서는: `this.GetCancellationTokenOnDestroy()`. System에서는: `CancellationTokenSource`를 직접 소유하고 `Dispose()`에서 취소하세요.

## DontDestroyOnLoad

아껴서 사용하세요. 부트스트래퍼 씬 패턴을 우선하세요:
```
BootstrapScene (한 번 로드되며, 영구적인 서비스를 포함)
    → GameScene, MenuScene 등을 추가적으로(Additively) 로드함
```

## Transform

- `transform.SetParent(parent, false)` — 로컬 트랜스폼을 보존하려면 `worldPositionStays: false`를 사용하세요
- `Application.isPlaying` — 에디터 도메인 리로드 중 클린업을 피하기 위해 OnDisable/OnDestroy에서 확인하세요

## Time

- `Update`와 `LateUpdate`에서는 `Time.deltaTime`
- `FixedUpdate`에서는 `Time.fixedDeltaTime`
- `FixedUpdate`에서 `Time.deltaTime`을 절대 사용하지 마세요 (그곳에서는 `fixedDeltaTime`과 같은 값이지만, 혼란스럽습니다)
- 일시정지와 무관한 로직(UI 애니메이션 등)에는 `Time.unscaledDeltaTime`

## 컴포넌트 속성

```csharp
[RequireComponent(typeof(Rigidbody))]        // Rigidbody를 자동으로 추가하고, 제거를 방지함
[DisallowMultipleComponent]                   // 중복 컴포넌트를 방지함
[DefaultExecutionOrder(-100)]                 // 기본 스크립트보다 먼저 실행됨
[SelectionBase]                               // 자식이 아닌 이 오브젝트를 클릭 시 선택함
```

## .meta 파일

- 절대 수동으로 편집하지 마세요
- 항상 해당 에셋과 함께 커밋하세요
- .meta 파일이 없으면 Unity가 GUID를 재생성하며, 모든 참조가 깨집니다
- 고아가 된 .meta 파일은 잡동사니가 되고 충돌을 일으킬 수 있습니다
