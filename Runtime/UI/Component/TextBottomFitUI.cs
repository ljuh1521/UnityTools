using TMPro;
using UnityEngine;

namespace UnityTools.UI
{
    /// <summary>
    /// 아래 정렬한 글자를 상자 바닥에 붙인다(2026-09-16 사용자 요청 — 아이템 개수 글자의 좌우
    /// 여백과 아래 여백이 달라 보였다).
    ///
    /// TMP의 아래 정렬은 글자 바닥이 아니라 <b>폰트의 내림선</b>에 맞춘다 — g·y가 아래로 내려가는
    /// 자리를 항상 비워 두므로, 숫자만 쓰는 칸은 그 높이만큼 떠 보인다. 수성돋움은 내림선이
    /// 27분의 8.91이라 글자 크기 36에서 11.88이 뜬다(좌우 여백이 5인데 아래만 17인 셈).
    ///
    /// 뜨는 높이는 글자 크기에 비례하는데 자동 크기 조절이 켜져 있으면 글자 수에 따라 크기가
    /// 달라지므로, 미리 계산해 둘 수가 없다. 그래서 TMP가 크기를 확정하고 화면에 올리기 직전에
    /// (OnPreRenderText) 그때 값으로 글자를 그만큼 내린다.
    ///
    /// 글자를 옮길 뿐 TMP 설정은 건드리지 않는다 — 상자 크기나 여백을 바꾸면 자동 크기 조절이
    /// 다시 계산되어, 보정하려던 양이 보정 때문에 달라지는 물림이 생긴다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public class TextBottomFitUI : MonoBehaviour
    {
        private TMP_Text text;

        private void OnEnable()
        {
            text = GetComponent<TMP_Text>();
            if (text == null) return;

            // 먼저 떼고 건다 — 에디터 리로드처럼 OnEnable이 짝 없이 한 번 더 도는 자리가 있고,
            // 두 번 걸리면 글자가 두 번 내려간다(2026-09-16 사용자 지적 "더 내려가 있어").
            text.OnPreRenderText -= Fit;
            text.OnPreRenderText += Fit;
            text.SetVerticesDirty();
        }

        private void OnDisable()
        {
            if (text == null) return;

            text.OnPreRenderText -= Fit;
            text.SetVerticesDirty();
        }

        private void Fit(TMP_TextInfo info)
        {
            if (info == null || info.lineCount <= 0) return;

            if (!TryGetInkBottom(info, out float inkBottom)) return;

            // 아래 정렬은 내림선을 상자 바닥에 맞추므로, 내림선과 실제로 그려진 바닥의 차이만큼
            // 내리면 글자가 상자 바닥에 딱 앉는다. 앉는 선이 아니라 그려진 바닥을 기준으로 삼는 건
            // 이 폰트의 숫자가 앉는 선보다 2.48(크기 36 기준) 더 내려오기 때문이다 — 앉는 선에
            // 맞추면 그만큼 상자 밖으로 삐져나온다.
            float drop = info.lineInfo[info.lineCount - 1].descender - inkBottom;

            if (Mathf.Approximately(drop, 0f)) return;

            for (int i = 0; i < info.meshInfo.Length; i++)
            {
                var vertices = info.meshInfo[i].vertices;
                if (vertices == null) continue;

                int count = info.meshInfo[i].vertexCount;

                for (int v = 0; v < count; v++) vertices[v].y += drop;
            }
        }

        /// 실제로 그려지는 글자의 맨 아래. 정점은 SDF 여백까지 품고 있어 쓸 수 없으므로,
        /// 글자마다 폰트에 적힌 잉크 상자(윗변 위치 − 높이)를 보고 가장 낮은 것을 고른다.
        private static bool TryGetInkBottom(TMP_TextInfo info, out float inkBottom)
        {
            inkBottom = float.MaxValue;

            for (int i = 0; i < info.characterCount; i++)
            {
                var character = info.characterInfo[i];
                if (!character.isVisible || character.textElement == null) continue;

                var metrics = character.textElement.glyph.metrics;
                float bottom = character.baseLine + (metrics.horizontalBearingY - metrics.height) * character.scale;

                if (bottom < inkBottom) inkBottom = bottom;
            }

            return inkBottom < float.MaxValue;
        }
    }
}
