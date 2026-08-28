using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;


/*///////////////////////////////////////////
                InputManager
기능 : 연결된 액션의 값을 가져와서 해당 값 셋팅
 *///////////////////////////////////////////

public struct tInputInfo
{
    public Vector2 MoveDir;
    public Vector2 ScreenPos;
    public Vector2 Delta;

    public bool OnSpace;
    public bool OnLButon;
    public bool OnRButton;
}

public class InputManager : MonoBehaviour
{
    private tInputInfo m_tInputInfo = new tInputInfo();
    public tInputInfo InputInfo => m_tInputInfo;

    public static InputManager m_Instance = null;

    [SerializeField] private List<InputActionReference> m_listMoveAction;
    [SerializeField] private List<InputActionReference> m_listScreenAction;
    [SerializeField] private List<InputActionReference> m_listDeltaAction;

    [SerializeField] private List<InputActionReference> m_listMoveSpaceAction;
    [SerializeField] private List<InputActionReference> m_listFireAction;
    [SerializeField] private List<InputActionReference> m_listZoomAction;

    AsyncOperationHandle handle;

    private bool m_isDeltaInitialized = false;
    private void Awake()
    {
        if (m_Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(gameObject);

        for (int i = 0; i < m_listMoveAction.Count; ++i)
            m_listMoveAction[i].action.Enable();

        for (int i = 0; i < m_listScreenAction.Count; ++i)
            m_listScreenAction[i].action.Enable();

        for (int i = 0; i < m_listDeltaAction.Count; ++i)
            m_listDeltaAction[i].action.Enable();

        for (int i = 0; i < m_listMoveSpaceAction.Count; ++i)
            m_listMoveSpaceAction[i].action.Enable();

        for (int i = 0; i < m_listFireAction.Count; ++i)
            m_listFireAction[i].action.Enable();

        for (int i = 0; i < m_listZoomAction.Count; ++i)
            m_listZoomAction[i].action.Enable();

    }

    private void Start()
    {

    }

    private void  Update()
    {
        UpdateMoveValue();

        UpdateScreenMoveValue();

        UpdateDeltaValue();

        UpdateSpaceValue();

        UpdateFireValue();

        UpdateZoomValue();

        
    }

    private void UpdateMoveValue()
    {
        for (int i = 0; i < m_listMoveAction.Count; ++i)
        {
            Vector2 vMoveValue = m_listMoveAction[i].action.ReadValue<Vector2>();
            m_tInputInfo.MoveDir = vMoveValue.normalized;
        }
    }

    private void UpdateSpaceValue()
    {
        for (int i = 0; i < m_listMoveSpaceAction.Count; ++i)
        {
            m_tInputInfo.OnSpace = m_listMoveSpaceAction[i].action.IsPressed();
        }
    }

    private void UpdateFireValue()
    {
        for (int i = 0; i < m_listFireAction.Count; ++i)
        {
            m_tInputInfo.OnLButon = m_listFireAction[i].action.IsPressed();
        }
    }

    private void UpdateZoomValue()
    {
        for (int i = 0; i < m_listZoomAction.Count; ++i)
        {
            m_tInputInfo.OnRButton = m_listZoomAction[i].action.IsPressed();
        }
    }

    private void UpdateScreenMoveValue()
    {
        for (int i = 0; i < m_listScreenAction.Count; ++i)
        {
            Vector2 vScreenPos = m_listScreenAction[i].action.ReadValue<Vector2>();
            m_tInputInfo.ScreenPos = vScreenPos;
        }
    }

    private void UpdateDeltaValue()
    {
        for (int i = 0; i < m_listDeltaAction.Count; ++i)
        {
            Vector2 vDelta = m_listDeltaAction[i].action.ReadValue<Vector2>();

            if (!m_isDeltaInitialized)
            {
                if (vDelta.sqrMagnitude > 0f)
                {
                    m_tInputInfo.Delta = Vector2.zero;
                    m_isDeltaInitialized = true;
                    continue;
                }
            }

            m_tInputInfo.Delta = vDelta;
        }
    }


}