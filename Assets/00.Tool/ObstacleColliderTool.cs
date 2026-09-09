#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/*///////////////////////////////////////////
                ObstacleColliderTool
목적 : 씬의 장애물 프롭 중 BoxCollider가 없는 오브젝트를 찾아
       메시 bounds에 맞춘 BoxCollider를 일괄 부착하는 에디터 툴.
       기존 씬 작업 방식(프리팹 에셋이 아니라 씬 인스턴스에
       BoxCollider를 추가하고 Obstacle 레이어를 지정)을 그대로 따른다.
 *///////////////////////////////////////////

public sealed class ObstacleColliderTool : EditorWindow
{
    private const string GROUND_OBJECT_NAME = "Ground";
    private const int OBSTACLE_LAYER = 13;

    // 스캔 조건
    private float m_fGroundY = -1.81f;
    private float m_fMinHeight = 0.25f;
    private float m_fMaxHeight = 30f;
    private float m_fMaxCenterY = 25f;
    private string m_strExcludeKeywords = "Cloud,Skydome,Grass,Rubbish,Paper,Water,Road,Decal";
    private int m_iExcludeLayerMask;
    private bool m_bIncludeInactive;

    // 적용 옵션
    private bool m_bReplaceMeshCollider = true;
    private bool m_bSetObstacleLayer = true;

    // 스캔 결과
    private readonly List<Candidate> m_listCandidates = new();
    private readonly Dictionary<string, Group> m_hashGroups = new();
    private readonly List<string> m_listGroupKeys = new();
    private Vector2 m_vScroll;
    private bool m_bScanned;
    private int m_iSkippedNoMesh;
    private int m_iSkippedHasCollider;

    private struct Candidate
    {
        public GameObject refGo;
        public MeshFilter refFilter;
        public MeshCollider refMeshCollider;
        public string strGroupKey;
    }

    private sealed class Group
    {
        public bool bEnabled = true;
        public int iCount;
        public float fSampleHeight;
        public int iMeshColliderCount;
        public GameObject refFirst;
    }

    [MenuItem("Tools/Add BoxColliders To Obstacles")]
    private static void Open()
    {
        ObstacleColliderTool refWindow = GetWindow<ObstacleColliderTool>("Obstacle Collider");
        refWindow.minSize = new Vector2(470f, 540f);
        refWindow.Show();
    }

    private void OnEnable()
    {
        // Player / Head / Attack / Ground / Enemy / Weapon 은 장애물이 아니다
        m_iExcludeLayerMask = (1 << 8) | (1 << 9) | (1 << 10) | (1 << 11) | (1 << 12) | (1 << 14);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("스캔 조건", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            m_fGroundY = EditorGUILayout.FloatField("지면 Y (이 위만 대상)", m_fGroundY);
            if (GUILayout.Button("Ground에서 감지", GUILayout.Width(110f)))
                DetectGroundY();
        }

        m_fMinHeight = EditorGUILayout.FloatField("최소 높이 (m)", m_fMinHeight);
        EditorGUILayout.LabelField(" ", "낮은 바닥 장식(풀·도로·쓰레기) 제외용", EditorStyles.miniLabel);
        m_fMaxHeight = EditorGUILayout.FloatField("최대 높이 (m)", m_fMaxHeight);
        m_fMaxCenterY = EditorGUILayout.FloatField("최대 중심 Y", m_fMaxCenterY);
        EditorGUILayout.LabelField(" ", "하늘의 구름·스카이돔 제외용", EditorStyles.miniLabel);

        m_strExcludeKeywords = EditorGUILayout.TextField("제외 키워드(쉼표)", m_strExcludeKeywords);
        m_iExcludeLayerMask = EditorGUILayout.MaskField("제외 레이어", m_iExcludeLayerMask, BuildLayerNames());
        m_bIncludeInactive = EditorGUILayout.Toggle("비활성 오브젝트 포함", m_bIncludeInactive);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("적용 옵션", EditorStyles.boldLabel);
        m_bReplaceMeshCollider = EditorGUILayout.Toggle("MeshCollider를 Box로 교체", m_bReplaceMeshCollider);
        m_bSetObstacleLayer = EditorGUILayout.Toggle("Obstacle 레이어로 지정", m_bSetObstacleLayer);

        EditorGUILayout.Space();
        if (GUILayout.Button("스캔", GUILayout.Height(28f)))
            Scan();

        if (!m_bScanned)
        {
            EditorGUILayout.HelpBox("먼저 [스캔]을 눌러 대상을 확인하세요.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();
        int iEnabled = CountEnabled();
        EditorGUILayout.HelpBox(
            string.Format("대상 {0}개 / 메시 종류 {1}개 · 선택됨 {2}개\n" +
                          "이미 콜라이더 있어 건너뜀: {3}개 · 메시 없어 건너뜀: {4}개",
                m_listCandidates.Count, m_listGroupKeys.Count, iEnabled,
                m_iSkippedHasCollider, m_iSkippedNoMesh),
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("전체 선택")) SetAllGroups(true);
            if (GUILayout.Button("전체 해제")) SetAllGroups(false);
        }

        EditorGUILayout.LabelField("메시 종류별 대상 (체크한 것만 적용)", EditorStyles.boldLabel);
        m_vScroll = EditorGUILayout.BeginScrollView(m_vScroll);
        for (int i = 0; i < m_listGroupKeys.Count; i++)
        {
            string strKey = m_listGroupKeys[i];
            Group refGroup = m_hashGroups[strKey];
            using (new EditorGUILayout.HorizontalScope())
            {
                refGroup.bEnabled = EditorGUILayout.Toggle(refGroup.bEnabled, GUILayout.Width(18f));
                string strLabel = string.Format("{0}  ({1}개, 높이 {2:0.##}m{3})",
                    strKey, refGroup.iCount, refGroup.fSampleHeight,
                    refGroup.iMeshColliderCount > 0
                        ? ", MeshCollider " + refGroup.iMeshColliderCount + "개"
                        : string.Empty);
                EditorGUILayout.LabelField(strLabel);
                if (GUILayout.Button("찾기", GUILayout.Width(48f)) && refGroup.refFirst != null)
                {
                    Selection.activeGameObject = refGroup.refFirst;
                    EditorGUIUtility.PingObject(refGroup.refFirst);
                }
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        GUI.enabled = iEnabled > 0;
        Color tPrevColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.5f, 0.85f, 0.5f);
        if (GUILayout.Button(string.Format("선택한 {0}개에 BoxCollider 적용", iEnabled), GUILayout.Height(32f)))
            Apply();
        GUI.backgroundColor = tPrevColor;
        GUI.enabled = true;
    }

    private void DetectGroundY()
    {
        GameObject refGround = GameObject.Find(GROUND_OBJECT_NAME);
        if (refGround == null)
        {
            EditorUtility.DisplayDialog("Ground 없음",
                "씬에서 \"" + GROUND_OBJECT_NAME + "\" 오브젝트를 찾지 못했습니다.", "확인");
            return;
        }

        BoxCollider refBox = refGround.GetComponent<BoxCollider>();
        if (refBox != null)
        {
            m_fGroundY = refBox.bounds.max.y;
            return;
        }

        Renderer refRenderer = refGround.GetComponent<Renderer>();
        m_fGroundY = refRenderer != null ? refRenderer.bounds.max.y : refGround.transform.position.y;
    }

    private void Scan()
    {
        m_listCandidates.Clear();
        m_hashGroups.Clear();
        m_listGroupKeys.Clear();
        m_iSkippedNoMesh = 0;
        m_iSkippedHasCollider = 0;

        string[] arrKeywords = SplitKeywords(m_strExcludeKeywords);
        GameObject[] arrRoots = SceneManager.GetActiveScene().GetRootGameObjects();

        for (int iRoot = 0; iRoot < arrRoots.Length; iRoot++)
        {
            MeshRenderer[] arrRenderers =
                arrRoots[iRoot].GetComponentsInChildren<MeshRenderer>(m_bIncludeInactive);

            for (int i = 0; i < arrRenderers.Length; i++)
            {
                MeshRenderer refRenderer = arrRenderers[i];
                GameObject refGo = refRenderer.gameObject;

                if (((1 << refGo.layer) & m_iExcludeLayerMask) != 0)
                    continue;
                if (MatchesKeyword(refGo.name, arrKeywords)
                    || MatchesKeyword(GetPrefabName(refGo), arrKeywords))
                    continue;

                if (refGo.GetComponent<BoxCollider>() != null
                    || refGo.GetComponent<SphereCollider>() != null
                    || refGo.GetComponent<CapsuleCollider>() != null)
                {
                    m_iSkippedHasCollider++;
                    continue;
                }

                MeshCollider refMeshCollider = refGo.GetComponent<MeshCollider>();
                if (refMeshCollider != null && !m_bReplaceMeshCollider)
                {
                    m_iSkippedHasCollider++;
                    continue;
                }

                MeshFilter refFilter = refGo.GetComponent<MeshFilter>();
                if (refFilter == null || refFilter.sharedMesh == null)
                {
                    m_iSkippedNoMesh++;
                    continue;
                }

                Bounds tWorld = refRenderer.bounds;
                if (tWorld.max.y <= m_fGroundY) continue;
                if (tWorld.size.y < m_fMinHeight) continue;
                if (tWorld.size.y > m_fMaxHeight) continue;
                if (tWorld.center.y > m_fMaxCenterY) continue;

                string strKey = refFilter.sharedMesh.name;
                Candidate tCandidate;
                tCandidate.refGo = refGo;
                tCandidate.refFilter = refFilter;
                tCandidate.refMeshCollider = refMeshCollider;
                tCandidate.strGroupKey = strKey;
                m_listCandidates.Add(tCandidate);

                if (!m_hashGroups.TryGetValue(strKey, out Group refGroup))
                {
                    refGroup = new Group { fSampleHeight = tWorld.size.y, refFirst = refGo };
                    m_hashGroups.Add(strKey, refGroup);
                    m_listGroupKeys.Add(strKey);
                }
                refGroup.iCount++;
                if (refMeshCollider != null)
                    refGroup.iMeshColliderCount++;
            }
        }

        m_listGroupKeys.Sort(CompareGroupByCount);
        m_bScanned = true;
    }

    private void Apply()
    {
        Undo.SetCurrentGroupName("장애물 BoxCollider 일괄 적용");
        int iUndoGroup = Undo.GetCurrentGroup();

        int iAdded = 0;
        int iRemovedMesh = 0;
        int iLayerChanged = 0;

        try
        {
            for (int i = 0; i < m_listCandidates.Count; i++)
            {
                Candidate tCandidate = m_listCandidates[i];
                if (!m_hashGroups[tCandidate.strGroupKey].bEnabled)
                    continue;
                if (tCandidate.refGo == null || tCandidate.refFilter == null)
                    continue;

                EditorUtility.DisplayProgressBar("BoxCollider 적용 중",
                    tCandidate.refGo.name, (float)i / m_listCandidates.Count);

                if (tCandidate.refMeshCollider != null && m_bReplaceMeshCollider)
                {
                    Undo.DestroyObjectImmediate(tCandidate.refMeshCollider);
                    iRemovedMesh++;
                }

                BoxCollider refBox = Undo.AddComponent<BoxCollider>(tCandidate.refGo);
                Bounds tLocal = tCandidate.refFilter.sharedMesh.bounds;
                refBox.center = tLocal.center;
                refBox.size = tLocal.size;
                iAdded++;

                if (m_bSetObstacleLayer && tCandidate.refGo.layer != OBSTACLE_LAYER)
                {
                    Undo.RecordObject(tCandidate.refGo, "Set Obstacle Layer");
                    tCandidate.refGo.layer = OBSTACLE_LAYER;
                    iLayerChanged++;
                }

                if (PrefabUtility.IsPartOfPrefabInstance(tCandidate.refGo))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(tCandidate.refGo);

                EditorUtility.SetDirty(tCandidate.refGo);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Undo.CollapseUndoOperations(iUndoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(string.Format(
            "[ObstacleColliderTool] BoxCollider {0}개 추가 · MeshCollider {1}개 제거 · 레이어 변경 {2}개. " +
            "되돌리려면 Ctrl+Z, 저장하려면 Ctrl+S.",
            iAdded, iRemovedMesh, iLayerChanged));

        Scan();
    }

    private int CountEnabled()
    {
        int iCount = 0;
        for (int i = 0; i < m_listGroupKeys.Count; i++)
        {
            Group refGroup = m_hashGroups[m_listGroupKeys[i]];
            if (refGroup.bEnabled)
                iCount += refGroup.iCount;
        }
        return iCount;
    }

    private void SetAllGroups(bool _bEnabled)
    {
        for (int i = 0; i < m_listGroupKeys.Count; i++)
            m_hashGroups[m_listGroupKeys[i]].bEnabled = _bEnabled;
    }

    private int CompareGroupByCount(string _strA, string _strB)
    {
        int iDiff = m_hashGroups[_strB].iCount.CompareTo(m_hashGroups[_strA].iCount);
        return iDiff != 0 ? iDiff : string.Compare(_strA, _strB, StringComparison.Ordinal);
    }

    private static string GetPrefabName(GameObject _refGo)
    {
        GameObject refSource = PrefabUtility.GetCorrespondingObjectFromSource(_refGo);
        return refSource != null ? refSource.name : string.Empty;
    }

    private static string[] SplitKeywords(string _strRaw)
    {
        if (string.IsNullOrEmpty(_strRaw))
            return Array.Empty<string>();

        string[] arrRaw = _strRaw.Split(',');
        List<string> listOut = new(arrRaw.Length);
        for (int i = 0; i < arrRaw.Length; i++)
        {
            string strTrimmed = arrRaw[i].Trim();
            if (strTrimmed.Length > 0)
                listOut.Add(strTrimmed);
        }
        return listOut.ToArray();
    }

    private static bool MatchesKeyword(string _strName, string[] _arrKeywords)
    {
        if (string.IsNullOrEmpty(_strName))
            return false;

        for (int i = 0; i < _arrKeywords.Length; i++)
        {
            if (_strName.IndexOf(_arrKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static string[] BuildLayerNames()
    {
        string[] arrNames = new string[32];
        for (int i = 0; i < 32; i++)
        {
            string strName = LayerMask.LayerToName(i);
            arrNames[i] = string.IsNullOrEmpty(strName) ? "(Layer " + i + ")" : strName;
        }
        return arrNames;
    }
}
#endif
