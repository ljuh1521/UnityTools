using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityTools.UI
{
    // UI/Outline 셰이더(같은 폴더 UIOutline.shader)에 아웃라인 폭·9슬라이스 보더 정보를 전달하는
    // 보조 컴포넌트. 인스턴스마다 값이 달라도 머티리얼(UI/Outline)은 하나만 공유할 수 있게, 값을
    // 머티리얼 프로퍼티가 아니라 정점의 uv 채널에 실어 보낸다. 이 값들을 계산해서 SetWidth를
    // 부르는 쪽(예: 아웃라인을 그리는 GenericUI 계열 컴포넌트)이 필요할 때 이 컴포넌트를
    // 자기 자식에 자동으로 붙이는 방식을 쓴다(DefenceR의 OutlineUI가 그 예).
    // 2026-09-02, DefenceR 세션에서 만든 뒤 프로젝트 비의존이라 공용 패키지로 옮김(com.ljuh.unitytools).
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    public class OutlineWidthModifier : BaseMeshEffect
    {
        [HideInInspector] public Vector2 outlineWidthUV;

        // Outline 쿼드가 원본(core, Image 크기)보다 얼마나 커졌는지 — 셰이더가 커진 쿼드의 UV를
        // 원본 스프라이트 UV로 되돌려 매핑할 때 쓴다. 1이면 안 커진 것.
        [HideInInspector] public Vector2 quadScale = Vector2.one;

        // 9슬라이스 보더 두께 — core 사각형 크기 대비 비율(x=left, y=bottom, z=right, w=top).
        // 보더가 없는 스프라이트(0,0,0,0)면 아래 MapAxis가 기존 선형 매핑과 완전히 같아진다.
        [HideInInspector] public Vector4 borderFrac;

        // 9슬라이스 보더 두께 — 원본 스프라이트 텍스처 크기 대비 비율(x=left, y=bottom, z=right, w=top).
        [HideInInspector] public Vector4 borderUV;

        public void SetWidth(Vector2 widthUV, Vector2 scale, Vector4 border, Vector4 borderUvFrac)
        {
            if (outlineWidthUV == widthUV && quadScale == scale && borderFrac == border && borderUV == borderUvFrac) return;

            outlineWidthUV = widthUV;
            quadScale = scale;
            borderFrac = border;
            borderUV = borderUvFrac;

            EnsureCanvasChannels();
            if (graphic != null) graphic.SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureCanvasChannels();
        }

        // uv2/uv3(TEXCOORD2/3)는 Canvas의 "Additional Shader Channels"를 켜야만 실제로 메쉬에
        // 실린다(기본값은 uv1만 켜져 있음) — 매번 수동으로 씬의 Canvas를 찾아 켜 달라고 하는 대신
        // 여기서 필요할 때 스스로 켠다(2026-09-01, quadScale이 조용히 버려지던 문제의 재발 방지).
        private void EnsureCanvasChannels()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            const AdditionalCanvasShaderChannels needed =
                AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3;

            if ((canvas.additionalShaderChannels & needed) == needed) return;

#if UNITY_EDITOR
            var so = new SerializedObject(canvas);
            so.FindProperty("m_AdditionalShaderChannelsFlag").intValue |= (int)needed;
            so.ApplyModifiedPropertiesWithoutUndo();
#else
            canvas.additionalShaderChannels |= needed;
#endif
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;

            var packed1 = new Vector4(outlineWidthUV.x, outlineWidthUV.y, quadScale.x, quadScale.y);

            UIVertex vertex = default;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.uv1 = packed1;
                vertex.uv2 = borderFrac;
                vertex.uv3 = borderUV;
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
