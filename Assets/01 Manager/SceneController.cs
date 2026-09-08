using Cysharp.Threading.Tasks;
using UnityEngine;

/*///////////////////////////////////////////
                SceneController
 *///////////////////////////////////////////

public sealed class SceneController : MonoBehaviour
{
    [SerializeField] private SOSceneData m_refSceneData;

    public void LoadScene()
    {
        GameSceneManager.m_Instance.LoadScene(m_refSceneData);
    }
}
