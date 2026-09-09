using UnityEngine;

/*///////////////////////////////////////////
                SceneController
목적 : 씬 안의 버튼이 GameSceneManager를 "직접" 참조하지 않도록 중간에 세우는 씬 수명 컴포넌트.
 *///////////////////////////////////////////

public sealed class SceneController : MonoBehaviour
{
    [SerializeField] private SOSceneData m_refSceneData; //이 버튼이 어떤 씬으로 보낼지

    public void LoadScene()
    {
        if (GameSceneManager.m_Instance == null)
        {
            Debug.LogError("GameSceneManager가 없다 : SceneController");
            return;
        }

        GameSceneManager.m_Instance.LoadScene(m_refSceneData);
    }

    public void LoadFirstScene()
    {
        if (GameSceneManager.m_Instance == null)
        {
            Debug.LogError("GameSceneManager가 없다 : SceneController");
            return;
        }

        GameSceneManager.m_Instance.LoadFirstScene();
    }
}
