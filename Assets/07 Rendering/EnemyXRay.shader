/*///////////////////////////////////////////
                EnemyXRay
목적 : 벽/장애물에 가려진 적을 실루엣으로 덧그리는 투시 셰이더.
       EnemyXRayFeature가 overrideMaterial로 Enemy 레이어에만 적용한다.

       스텐실 비트 0(값 1)만 사용한다 — URP는 StencilUsage.UserMask로
       비트 [0..3]만 유저에게 개방하고, 4~7은 Deferred 라이트/머티리얼 타입용이다.

       Pass 0(XRayMask)   : 지금 화면에 실제로 "보이는" 적 표면에 스텐실 비트를 심는다.
       Pass 1(XRayOverlay): 가려진 픽셀 중 비트가 안 찍힌 곳에만 색을 칠한다.

       Pass 0이 없으면 Synty 모듈러 캐릭터처럼 아머 메시가 본체를 파고드는 모델에서
       "적이 훤히 보이는데도 아머에 가려진 본체가 ZTest Greater를 통과해" 빨간 얼룩이 뜬다.
       Pass 1의 Comp NotEqual + Pass Replace는 픽셀당 드로우를 1회로 고정해
       겹친 메시가 몇 개든 반투명 색이 균일하게 나오도록 만든다.
 *///////////////////////////////////////////

Shader "FPS/EnemyXRay"
{
    Properties
    {
        [HDR] _XRayColor    ("X-Ray Color", Color) = (1, 0.06, 0.06, 1)
        _FillAlpha          ("Fill Alpha", Range(0, 1)) = 0.85

        [HDR] _RimColor     ("Rim Color", Color) = (1, 0.45, 0.35, 1)
        _RimPower           ("Rim Power", Range(0.5, 8)) = 2.5
        _RimStrength        ("Rim Strength", Range(0, 4)) = 1.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // SRP Batcher 호환을 위해 모든 프로퍼티는 UnityPerMaterial 안에 들어가야 한다
        CBUFFER_START(UnityPerMaterial)
            half4 _XRayColor;
            half4 _RimColor;
            half  _FillAlpha;
            half  _RimPower;
            half  _RimStrength;
        CBUFFER_END
        ENDHLSL

        // ------------------------------------------------------------------
        // Pass 0 — 마스크 : 색은 안 그리고 스텐실 비트만 심는다
        //   ZTest LEqual  → 깊이 버퍼와 같은(= 실제로 화면에 보이는) 표면만 통과
        //   ColorMask 0   → 픽셀 셰이딩 비용 없음
        // ------------------------------------------------------------------
        Pass
        {
            Name "XRayMask"

            Cull Back
            ZTest LEqual
            ZWrite Off
            ColorMask 0

            Stencil // (스텐실 버퍼 규칙)
            {
                Ref 1           //비교와 기록에 사용할 기준값(1)입니다.
                WriteMask 1     //비트 1번지 , Read : 스텐실 비교(Comp)를 수행할 때 비트 1번지 페이지만 읽어서 확인합니다.
                Comp Always     
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment MaskFrag

            struct MaskAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct MaskVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            MaskVaryings MaskVert(MaskAttributes IN)
            {
                MaskVaryings OUT = (MaskVaryings)0;
                //UNITY_SETUP_INSTANCE_ID(IN);: GPU 인스턴싱이나 SRP Batcher를 사용할 때, 현재 몇 번째 오브젝트를 그리고 있는지 식별자(Instance ID)
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 MaskFrag(MaskVaryings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                return half4(0, 0, 0, 0); // ColorMask 0이라 버려진다
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Pass 1 — 투시 : 가려진 픽셀에만, 픽셀당 딱 한 번
        //   ZTest Greater → 깊이 버퍼보다 "더 먼" = 무언가에 가려진 픽셀만 통과
        //                   (Reversed-Z 여부와 무관하게 Unity가 내부에서 뒤집어 준다)
        //   Comp NotEqual → Pass 0이 심은 비트(보이는 적) + 이미 이 패스가 칠한 픽셀을 걸러낸다
        //   Pass Replace  → 칠하는 즉시 비트를 심어 뒤따르는 겹친 메시를 막는다
        // ------------------------------------------------------------------
        Pass
        {
            //프레임 디버거 등에서 이 패스를 식별할 수 있도록 붙인 이름입니다.
            Name "XRayOverlay"

            Cull Back
            ZTest Greater
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 1
                Comp NotEqual
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex OverlayVert
            #pragma fragment OverlayFrag

            struct OverlayAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct OverlayVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            OverlayVaryings OverlayVert(OverlayAttributes IN)
            {
                OverlayVaryings OUT = (OverlayVaryings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs tPosInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   tNrmInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = tPosInputs.positionCS;
                OUT.normalWS   = tNrmInputs.normalWS;
                OUT.viewDirWS  = GetWorldSpaceViewDir(tPosInputs.positionWS);
                return OUT;
            }

            half4 OverlayFrag(OverlayVaryings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 vNormal  = normalize(IN.normalWS);
                float3 vViewDir = normalize(IN.viewDirWS);

                // 시선과 표면이 스칠수록 1에 가까워지는 프레넬 — 실루엣 외곽을 밝게 띄운다
                half fRim = pow(saturate(1.0h - saturate(dot(vNormal, vViewDir))), _RimPower);

                half3 cFinal = _XRayColor.rgb + _RimColor.rgb * (fRim * _RimStrength);
                half  fAlpha = saturate(_FillAlpha + fRim * _RimStrength);

                return half4(cFinal, fAlpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
