// OutlineUI/ShadowUI 등이 같은 스프라이트를 재사용해 테두리·그림자를 만들 때 쓴다. 기본
// UI 셰이더는 색을 텍스처 색과 곱하기 때문에(예: 파란 아이콘 × 빨간 틴트 = 탁한 보라)
// 원본이 흰색이 아니면 지정한 색이 그대로 안 나온다 — 텍스처의 알파(모양)만 쓰고 RGB는
// 전부 버려서 항상 지정한 색 그대로 나오는 실루엣을 만든다(2026-08-31, 사용자 지적).
// 나머지(스텐실·클리핑·알파컷)는 유니티 기본 UI 셰이더(UI/Default)를 그대로 따른다 —
// RectMask2D 등 기존 UI 클리핑과 그대로 호환된다.
Shader "UI/Silhouette"
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

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
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

                #ifdef UNITY_HALF_TEXEL_OFFSET
                OUT.vertex.xy += (_ScreenParams.zw - 1.0) * float2(-1, 1);
                #endif

                // Image.color(인스펙터 색상)만 그대로 쓴다 — 텍스처 RGB는 안 섞는다.
                fixed4 tint = v.color * _Color;

                // Linear 색공간 프로젝트에서는 텍스처(sRGB 임포트)는 샘플링할 때 자동으로
                // 감마→리니어 변환이 되는데, 이 vertex color(인스펙터에서 감마로 고른 값)는
                // 그 변환을 안 거친다. 그대로 출력하면 최종 화면에 다시 리니어→감마가 걸리며
                // 밝고 탁하게 나온다(2026-09-07 — 스포이드로 실측: F06927 지정 → 화면에 F8AC6D로
                // 나옴. F06927을 감마 인코딩(x^(1/2.2))하면 (248,169,106)로 F8AC6D와 거의
                // 일치해 이 가설을 확인함). 여기서 미리 리니어로 바꿔주면 상쇄된다.
                #ifndef UNITY_COLORSPACE_GAMMA
                tint.rgb = GammaToLinearSpace(tint.rgb);
                #endif

                OUT.color = tint;
                return OUT;
            }

            sampler2D _MainTex;

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 texColor = tex2D(_MainTex, IN.texcoord);

                // RGB는 지정한 색 그대로, 알파만 텍스처(원본 그림의 모양)를 따라간다 —
                // 원본이 무슨 색이든 실루엣 색이 항상 정확히 나온다.
                half4 color = half4(IN.color.rgb, IN.color.a * (texColor.a + _TextureSampleAdd.a));

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
