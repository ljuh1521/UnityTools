// 사각형이 아닌 실루엣(아치·별 모양 등)에도 두께가 고른 UI 테두리 셰이더. 흔한 "같은 스프라이트를
// 뒤에 하나 더 깔고 사각형 틀 안쪽으로 밀어넣는" 방식은 실루엣이 사각형이 아니면 두께가 고르지
// 않게 나온다. 이 셰이더는 대신 원본 알파의 가장자리를 따라 링 모양으로 직접 번지게 해서, 모양이
// 아무리 복잡해도 두께가 고르다.
//
// 9슬라이스(보더) 스프라이트도 지원한다 — 보더·unitMultiplier에 맞춰 모서리는 안 늘리고 가운데만
// 늘리는 렌더링과 똑같이, 이 셰이더도 (core 사각형 기준 위치) → (원본 스프라이트 UV) 변환을 보더
// 구간별로 나눠 계산한다(MapAxis). 보더가 0인 스프라이트는 이 계산이 그대로 기존 선형 매핑과
// 같아진다.
//
// 필요한 값은 전부 정점의 uv 채널에 담아 받는다(같은 폴더의 OutlineWidthModifier가 채운다) —
// 인스턴스마다 값이 달라도 머티리얼 하나를 여러 오브젝트가 공유할 수 있게 하기 위해서다.
// uv2/uv3는 기본적으로 꺼져 있는 Canvas 채널이라 OutlineWidthModifier가 스스로 켠다.
//
// 쓰는 쪽 준비물: 이 셰이더를 배경(뒤) 레이어에 씌우고, 그 위에 같은 스프라이트를 인셋 없이
// 그대로 그리는 앞(실제 그림) 레이어를 겹친다 — 뒤 레이어가 원본 밖으로 삐져나온 링만 보인다.
// 원본이 앞 레이어에 그대로 덮이므로 뒤 레이어 안쪽은 안 보여도 된다.
//
// 원본: DefenceR 프로젝트(2026-09-01)에서 만든 뒤 프로젝트 비의존이라 공용 패키지로 옮김
// (2026-09-02).
Shader "UI/Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        _ClipRect ("Clip Rect", vector) = (-32767, -32767, 32767, 32767)
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #define OUTLINE_SAMPLES 24
            #define OUTLINE_TWO_PI 6.28318530718

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                // OutlineWidthModifier가 채운다 — xy=UV 기준 아웃라인 폭, zw=쿼드 확대 배율.
                float4 packed1  : TEXCOORD1;
                // xyzw = 보더 두께(core 사각형 크기 대비 비율) left/bottom/right/top.
                float4 borderFrac : TEXCOORD2;
                // xyzw = 보더 두께(원본 스프라이트 크기 대비 비율) left/bottom/right/top.
                float4 borderUV : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float2 outlineWidth  : TEXCOORD1;
                float2 quadScale     : TEXCOORD2;
                float4 borderFrac    : TEXCOORD3;
                float4 borderUV      : TEXCOORD4;
                float4 worldPosition : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = v.texcoord;
                OUT.outlineWidth = v.packed1.xy;
                OUT.quadScale = v.packed1.zw;
                OUT.borderFrac = v.borderFrac;
                OUT.borderUV = v.borderUV;

                #ifdef UNITY_HALF_TEXEL_OFFSET
                OUT.vertex.xy += (_ScreenParams.zw - 1.0) * float2(-1, 1);
                #endif

                OUT.color = v.color * _Color;
                return OUT;
            }

            sampler2D _MainTex;

            // core 사각형 기준 위치(0..1, 보더 구간 포함)를 원본 스프라이트 UV로 바꾼다 —
            // Image 레이어(Sliced)가 모서리는 안 늘리고 가운데만 늘리는 것과 같은 매핑이다.
            // bMin/bMax=0이면(보더 없는 스프라이트) 그대로 frac을 돌려줘 기존 선형 매핑과 같다.
            float MapAxis(float frac, float bMin, float bMax, float uvMin, float uvMax)
            {
                if (frac < bMin)
                    return lerp(0, uvMin, frac / max(bMin, 1e-5));
                if (frac > 1 - bMax)
                    return lerp(uvMax, 1, (frac - (1 - bMax)) / max(bMax, 1e-5));
                return lerp(uvMin, uvMax, (frac - bMin) / max(1 - bMin - bMax, 1e-5));
            }

            // core 밖(0..1 벗어남)이면 "그림 없음"으로 친다 — Outline 쿼드가 원본보다 커진 만큼
            // 생긴 바깥 여백은 실제 텍스처에 대응하는 픽셀이 없기 때문에, 텍스처 Clamp로
            // 가장자리 픽셀을 늘리는 대신 명시적으로 투명 처리한다.
            half SampleCoreAlpha(float2 coreFrac, float4 borderFrac, float4 borderUV)
            {
                if (coreFrac.x < 0 || coreFrac.x > 1 || coreFrac.y < 0 || coreFrac.y > 1) return 0;

                float2 uv;
                uv.x = MapAxis(coreFrac.x, borderFrac.x, borderFrac.z, borderUV.x, 1 - borderUV.z);
                uv.y = MapAxis(coreFrac.y, borderFrac.y, borderFrac.w, borderUV.y, 1 - borderUV.w);
                return tex2D(_MainTex, uv).a + _TextureSampleAdd.a;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 커진 Outline 쿼드의 UV(0..1)를 core(Image와 같은 크기) 기준 위치로 되돌린다 —
                // 쿼드 중심은 그대로, 가장자리로 갈수록 core 범위(0..1) 밖으로 벌어진다.
                float2 coreFrac = (IN.texcoord - 0.5) * IN.quadScale + 0.5;

                half srcAlpha = SampleCoreAlpha(coreFrac, IN.borderFrac, IN.borderUV);
                half ringAlpha = 0;
                half maxAlpha = srcAlpha;

                if (IN.outlineWidth.x > 0 || IN.outlineWidth.y > 0)
                {
                    // 원 둘레로 샘플링해 "지금 이 자리에서 가장 가까운 원본 그림까지의 거리"가
                    // 아웃라인 폭 안쪽인지를 근사한다 — 원본이 사각형이 아니어도 폭이 고르다.
                    UNITY_UNROLL
                    for (int i = 0; i < OUTLINE_SAMPLES; i++)
                    {
                        float angle = i * (OUTLINE_TWO_PI / OUTLINE_SAMPLES);
                        float2 offset = float2(cos(angle) * IN.outlineWidth.x, sin(angle) * IN.outlineWidth.y);
                        maxAlpha = max(maxAlpha, SampleCoreAlpha(coreFrac + offset, IN.borderFrac, IN.borderUV));
                    }

                    // 원본이 있는 자리(srcAlpha>0)는 위에 그려질 Image 레이어가 실제 그림으로
                    // 덮으므로 여기선 "원본 밖인데 주변엔 원본이 있는" 링만 남긴다.
                    ringAlpha = saturate(maxAlpha - srcAlpha);
                }

                half4 color = half4(IN.color.rgb, IN.color.a * ringAlpha);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
