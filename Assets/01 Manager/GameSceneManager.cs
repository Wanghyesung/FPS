using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/*///////////////////////////////////////////
                GameSceneManager
기능 : 로비(SelectStage)에서 고른 스테이지 idx에 대응하는 SOSceneData를 찾아
      Addressable 씬을 비동기로 로드하고, 진행률을 이미지(fillAmount)로 보여준다.
      씬 전환 도중에도 살아있어야 해서 씬에 배치된 채 DontDestroyOnLoad로 유지하고,
      씬 로드가 끝나면 그 씬에서 쓸 오브젝트 풀 데이터까지 이어서 로드한다.

수명 주의 : 이 매니저는 첫 씬의 인스턴스 하나만 살아남고, 이후 씬이 만드는 새 인스턴스는
      Awake의 중복 가드에서 즉시 파괴된다. 그래서 씬 오브젝트(버튼의 UnityEvent 등)가
      이 매니저를 인스펙터로 직접 붙잡으면 안 된다 - 씬 파일의 m_Target은 그 씬 안의
      오브젝트만 지목할 수 있어서, 재로드된 씬의 버튼은 "곧 파괴될 새 인스턴스"를 가리키게 되고
      타겟이 Missing이 되어 조용히 동작을 멈춘다(메서드 이름을 바꿔도 소용없다).
      씬 쪽에서는 반드시 SceneController처럼 호출 시점에 m_Instance를 조회할 것.
      반대로 이 매니저가 참조하는 오브젝트(로딩 오버레이)는 반드시 자식으로 두어야
      DontDestroyOnLoad에 함께 딸려와서 참조가 깨지지 않는다.
 *///////////////////////////////////////////
public sealed class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager m_Instance = null;

    //SelectStage의 이미지 idx와 1:1 대응. 각 스테이지가 어떤 씬 + 어떤 풀데이터를 쓰는지 여기서 관리
    [SerializeField] private List<SOSceneData> m_listSceneData = new List<SOSceneData>();

    //[SerializeField] private Image m_refProgressImage; //fillAmount로 로딩 진행률 표시
    //반드시 이 오브젝트의 자식이어야 한다(씬 Canvas 밑에 두면 씬이 언로드될 때 죽은 참조가 된다)
    [SerializeField] private LoadingOverlay m_refLoadingOverlay;
    [SerializeField] private string m_strFirstSceneName = "StartScene"; //Addressable이 아닌 Build Settings 등록 씬(로비/처음 씬)

    [SerializeField] private GameObject m_refLoadCanvas;
    public int SelectedStageIdx { get; private set; } = 0;

    private AsyncOperationHandle<SceneInstance> m_tSceneHandle;

    private void Awake()
    {
        if (m_Instance != null && m_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(gameObject);

        if (m_refLoadingOverlay != null)
            m_refLoadingOverlay.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (m_Instance == this)
            m_Instance = null;
    }

    //SelectStage에서 이미지를 클릭했을 때 호출
    public void StartScene()
    {
        LoadStage(0);
    }
    public void LoadStage(int _iStageIdx)
    {
        if (_iStageIdx < 0 || _iStageIdx >= m_listSceneData.Count)
        {
            Debug.Log("잘못된 스테이지 idx : GameSceneManager");
            return;
        }

        SelectedStageIdx = _iStageIdx;
        LoadSceneAsync(m_listSceneData[_iStageIdx]).Forget();
    }

    private async UniTaskVoid LoadSceneAsync(SOSceneData _refSceneData)
    {
        if (m_refLoadingOverlay != null)
            m_refLoadingOverlay.gameObject.SetActive(true);

        SetProgress(0.0f);
        //씬 로드가 끝났다고 100%가 되면 안 되므로(뒤에 풀 로딩이 남음) 구간을 절반씩 나눠서 표시
        //씬 로드 0~0.5, 풀 로드 0.5~1.0
        var refSceneProgress = Progress.Create<float>(fPercent => SetProgress(fPercent * 0.5f));
        var refPoolProgress = Progress.Create<float>(fPercent => SetProgress(0.5f + fPercent * 0.5f));

        m_tSceneHandle = Addressables.LoadSceneAsync(_refSceneData.SceneAddress, LoadSceneMode.Single);
        await m_tSceneHandle.ToUniTask(refSceneProgress, cancellationToken: this.GetCancellationTokenOnDestroy());

        await ObjectPoolManager.m_Instance.LoadPoolAsync(_refSceneData.PoolDataList, this.GetCancellationTokenOnDestroy(), refPoolProgress);

        if (m_refLoadingOverlay != null)
            m_refLoadingOverlay.gameObject.SetActive(false);

        //DungeonManager.m_Instance.StartStage(SelectedStageIdx);
        Debug.Log("로딩완료");
    }

    private void SetProgress(float _fPercent)
    {
        if (m_refLoadingOverlay == null)
            return;

        m_refLoadingOverlay.SetProgress(_fPercent);
    }

    //던전 클리어 등으로 처음 씬(로비)으로 돌아갈 때 호출. 로비는 Addressable이 아니라 Build Settings에 등록된 일반 씬이라 SceneManager로 바로 로드
    public void LoadFirstScene()
    {
        m_refLoadCanvas.SetActive(true);
        LoadFirstSceneAsync().Forget();
    }

    private async UniTaskVoid LoadFirstSceneAsync()
    {
        //진행률 계산 없이 배경(오버레이)만 켰다 끄기
        if (m_refLoadingOverlay != null)
            m_refLoadingOverlay.ShowLoadingImage();

        await SceneManager.LoadSceneAsync(m_strFirstSceneName, LoadSceneMode.Single)
            .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());

        if (m_refLoadingOverlay != null)
            m_refLoadingOverlay.CompletedLoading();
    }


    public void LoadScene(SOSceneData _SOSceneData)
    {
        LoadSceneAsync(_SOSceneData).Forget();
    }
    public void LoadBattleScene()
    {
        LoadStage(0);
        m_refLoadCanvas.SetActive(false);
    }
}
