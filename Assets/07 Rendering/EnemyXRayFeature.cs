using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/*///////////////////////////////////////////
                EnemyXRayFeature
목적 : Opaque가 다 그려진 뒤 Enemy 레이어만 2패스로 덧그려서,
       벽/장애물에 가려진(깊이 테스트에서 밀린) 적만 실루엣으로 비쳐 보이게 하는
       URP Renderer Feature. 플레이어의 투시 능력이 켜져 있는 동안에만 패스를 큐에 넣는다.

       머티리얼 에셋을 따로 만들지 않고 셰이더 참조로 런타임 머티리얼을 생성한다
       (URP 내장 Feature들이 쓰는 CoreUtils.CreateEngineMaterial 패턴).
       색/알파를 인스펙터에서 바로 조절할 수 있고 관리할 에셋이 하나 줄어든다.
 *///////////////////////////////////////////

public sealed class EnemyXRayFeature : ScriptableRendererFeature
{
    [Header("Setup")]
    [Tooltip("Assets/07 Rendering/EnemyXRay.shader 를 지정한다")]
    [SerializeField] private Shader m_refXRayShader;
    
    //타겟으로 지정할 레잉어
    [SerializeField] private LayerMask m_tTargetLayer = 1 << 12;

    [SerializeField] private RenderPassEvent m_eRenderPassEvent = RenderPassEvent.AfterRenderingSkybox;

    [Header("Look")]
    [ColorUsage(true, true)]
    [SerializeField] private Color m_tXRayColor = new Color(1.0f, 0.06f, 0.06f, 1.0f);
    [Range(0.0f, 1.0f)]
    [SerializeField] private float m_fFillAlpha = 0.85f;

    [ColorUsage(true, true)]
    [SerializeField] private Color m_tRimColor = new Color(1.0f, 0.45f, 0.35f, 1.0f);
    [Range(0.5f, 8.0f)]
    [SerializeField] private float m_fRimPower = 2.5f;
    [Range(0.0f, 4.0f)]
    [SerializeField] private float m_fRimStrength = 1.2f;

    private static readonly int XRayColorId   = Shader.PropertyToID("_XRayColor");
    private static readonly int FillAlphaId   = Shader.PropertyToID("_FillAlpha");
    private static readonly int RimColorId    = Shader.PropertyToID("_RimColor");
    private static readonly int RimPowerId    = Shader.PropertyToID("_RimPower");
    private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");

    private EnemyXRayPass m_refPass;
    private Material m_refRuntimeMaterial;

    // 이 피처는 빌드에서 두 벌로 존재할 수 있다. 파이프라인이 GraphicsSettings를 통해
    // 로드한 사본과, 이 피처를 참조하는 씬이 Addressable 번들로 구워질 때 암시적
    // 의존성으로 딸려 들어간 사본이다. on/off를 static으로 두면 어느 사본을 거쳐
    // 켜더라도 실제로 렌더링하는 사본이 같은 값을 본다
    private static bool s_bIsXRayOn;

    public static bool IsXRayOn => s_bIsXRayOn;

    public override void Create()
    {
        s_bIsXRayOn = false;

        CoreUtils.Destroy(m_refRuntimeMaterial);
        m_refRuntimeMaterial = null;

        if (m_refXRayShader == null)
            return;

        m_refRuntimeMaterial = CoreUtils.CreateEngineMaterial(m_refXRayShader);
        ApplyShdaerData();

        //렌더링 패스 (어떤 메테링러 , 어떤 레이어를 타겟으로 할지)
        m_refPass = new EnemyXRayPass(m_refRuntimeMaterial, m_tTargetLayer)
        {
            renderPassEvent = m_eRenderPassEvent
        };
    }

    //투시가 꺼져 있어도 매 프레임 호출
    public override void AddRenderPasses(ScriptableRenderer _refRenderer, ref RenderingData _tRenderingData)
    {
        if (s_bIsXRayOn == false)
            return;

        if (m_refPass == null || m_refRuntimeMaterial == null)
            return;

        // 머티리얼 프리뷰 썸네일과 리플렉션 프로브까지 빨갛게 물드는 걸 막는다.
        // Scene 뷰는 일부러 남겨둬서 에디터에서 눈으로 디버깅할 수 있게 한다
        CameraType eCameraType = _tRenderingData.cameraData.cameraType;
        if (eCameraType == CameraType.Preview || eCameraType == CameraType.Reflection)
            return;

        _refRenderer.EnqueuePass(m_refPass);
    }

    protected override void Dispose(bool _bDisposing)
    {
        CoreUtils.Destroy(m_refRuntimeMaterial);
        m_refRuntimeMaterial = null;
        m_refPass = null;
    }

    // 투시 능력 컨트롤러(PlayerXRayVision)가 호출한다
    public static void SetXRayEnabled(bool _bOn)
    {
        s_bIsXRayOn = _bOn;
    }

    private void ApplyShdaerData()
    {
        //ConstBuffer로 Shader로 던지는 값
        m_refRuntimeMaterial.SetColor(XRayColorId, m_tXRayColor);
        m_refRuntimeMaterial.SetColor(RimColorId, m_tRimColor);
        m_refRuntimeMaterial.SetFloat(FillAlphaId, m_fFillAlpha);
        m_refRuntimeMaterial.SetFloat(RimPowerId, m_fRimPower);
        m_refRuntimeMaterial.SetFloat(RimStrengthId, m_fRimStrength);
    }

    /*///////////////////////////////////////////
                    EnemyXRayPass
    목적 : Enemy 레이어를 마스크 패스 → 오버레이 패스 순으로 그린다.
           스텐실/깊이 상태는 셰이더의 Pass가 직접 들고 있으므로
           여기서는 RenderStateBlock으로 덮어쓸 필요가 없다
     *///////////////////////////////////////////
    private sealed class EnemyXRayPass : ScriptableRenderPass
    {
        private const int MASK_PASS_INDEX = 0;
        private const int OVERLAY_PASS_INDEX = 1;

        // 원본 머티리얼(Synty URP/Lit)의 LightMode 태그로 그릴 렌더러를 고른다.
        // 실제로 실행되는 셰이더는 overrideMaterial 쪽이다
        private static readonly List<ShaderTagId> ShaderTagList = new()
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"),
        };

        private static readonly ProfilingSampler Sampler = new("Enemy X-Ray");

        private readonly Material m_refMaterial;
        private FilteringSettings m_tFilteringSettings;

        internal EnemyXRayPass(Material _refMaterial, LayerMask _tTargetLayer)
        {
            m_refMaterial = _refMaterial;
            //필터링은 불투명 단계에서 렌더링을 하고 타겟 레이어를 셋팅
            m_tFilteringSettings = new FilteringSettings(RenderQueueRange.opaque, _tTargetLayer);
        }

        //Enemy 레이어를 두 번 그립니다
        public override void Execute(ScriptableRenderContext _refContext, ref RenderingData _tRenderingData)
        {
            if (m_refMaterial == null)
                return;

            //직전까지 버퍼에 누적된 렌더링 명령어들을 GPU에 일괄 전송하고, 다음 명령을 담을 수 있도록 버퍼를 비웁니다.
            CommandBuffer refCmd = CommandBufferPool.Get();

            using (new ProfilingScope(refCmd, Sampler))
            {
                _refContext.ExecuteCommandBuffer(refCmd);
                refCmd.Clear();

                //URP 렌더링 파이프라인에서 적들을 어떤 셰이더 태그(ShaderTagList)와
                //정렬 기준(CommonOpaque)으로 그릴지 설정 객체를 만듭니다

                DrawingSettings tDrawingSettings =
                    CreateDrawingSettings(ShaderTagList, ref _tRenderingData, SortingCriteria.CommonOpaque);
                tDrawingSettings.overrideMaterial = m_refMaterial;

                // 모든 적의 마스크 패스를 먼저 돌린 뒤에 오버레이 패스를 돌린다.
                // 오브젝트마다 0,1을 번갈아 그리면 적끼리 겹칠 때 아직 마스킹되지 않은
                // 뒤쪽 적이 앞쪽 적 위에 칠해진다

                //overrideMaterialPassIndex는 유니티 DrawingSettings에서 제공하는 속성으로,
                //오버라이드한 머티리얼의 여러 패스(Pass) 중 "특정 번호의 패스만 골라서" 그리도록 강제하는 설정입니다.
                tDrawingSettings.overrideMaterialPassIndex = MASK_PASS_INDEX;
                _refContext.DrawRenderers(_tRenderingData.cullResults,
                    ref tDrawingSettings, ref m_tFilteringSettings);

                tDrawingSettings.overrideMaterialPassIndex = OVERLAY_PASS_INDEX;
                _refContext.DrawRenderers(_tRenderingData.cullResults,
                    ref tDrawingSettings, ref m_tFilteringSettings);
            }

            _refContext.ExecuteCommandBuffer(refCmd);
            CommandBufferPool.Release(refCmd);
        }
    }
}
