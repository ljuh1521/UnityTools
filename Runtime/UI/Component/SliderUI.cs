using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.UI
{
    /// <summary>
    /// 게이지. <see cref="ButtonUI"/>와 같은 방식으로 <b>targetSprite 하나를 정하면 배경과 채움이
    /// 같이 그 그림이 되고</b>, 색은 배경(imageColor)·채움(fillColor)을 따로 정한다. 테두리·그림자는
    /// <see cref="OutlineShadowUI"/>에서 그대로 물려받는다(2026-09-17 사용자 요청).
    ///
    /// 예전에는 Background 안에 Image_Outline 부품을 통째로 끼워 넣어 테두리를 냈는데, 그러면
    /// 그 부품이 중첩 GenericUI라 바깥에서 아무것도 못 잡는다(Collect가 거기서 멈춘다).
    ///
    /// <b>프리팹은 이렇게 둔다</b> — 슬라이더 루트 바로 아래에 형제로:
    /// <list type="bullet">
    /// <item>Shadow (ImageUI, 선택) — 맨 앞에. 형제 순서가 곧 그리는 순서라 뒤에 둘수록 위로 온다.</item>
    /// <item>Outline (ImageUI, 선택)</item>
    /// <item>Background (ImageUI) — 유니티 슬라이더가 만드는 그 노드에 ImageUI를 붙인다.</item>
    /// <item>Fill Area / Fill (ImageUI) — 슬라이더의 fillRect. 여기에도 ImageUI를 붙인다.</item>
    /// </list>
    /// 없는 것은 조용히 건너뛴다.
    /// </summary>
    public class SliderUI : OutlineShadowUI
    {
        [Header("UI")]
        [SerializeField] private Slider slider;
        [SerializeField] private RectTransform fillArea;

        [Tooltip("배경 그림. 비워 두면 'Background' 자식에서 찾는다.")]
        [SerializeField] private ImageUI background;

        [Header("Fill")]
        [Tooltip("채움 색. 배경 색은 위 imageColor가 맡는다.")]
        public Color fillColor = Color.white;

        [Header("Info")]
        public Vector2 areaOffset;
        public Vector2 fillOffset;

        public float Value
        {
            get => slider.value;
            set => slider.value = value;
        }

        public override void UpdateGenericUI(bool isEditor)
        {
            // 게이지는 그림을 늘려 쓰므로 "원본 크기로 맞추기"를 쓸 자리가 없다 — 물려받은 칸이라
            // 지울 수는 없으니 값이 들어와도 무시한다. 인스펙터에서는 아예 안 보인다(UpdateEditor의
            // UpdateSlider). 안 그러면 게이지 길이가 그림 원본 크기로 튄다(2026-09-17 사용자 요청).
            nativeSizeMultiplier = 0f;

            var backgroundImage = ResolveCoreImage(isEditor);

            // 물려받은 테두리 처리는 본판을 "부모를 꽉 채우도록" 되돌린다 — 카드처럼 본판이 곧 판인
            // 부품에는 맞지만, 게이지 배경에 일부러 여백을 준 경우엔 그 여백이 말없이 사라진다.
            // 자리는 사람이 정하는 것이라 원래대로 되돌린다(2026-09-17 검토에서 발견).
            var keptRect = backgroundImage != null ? backgroundImage.RectTransform : null;
            var keptAnchorMin = keptRect != null ? keptRect.anchorMin : Vector2.zero;
            var keptAnchorMax = keptRect != null ? keptRect.anchorMax : Vector2.one;
            var keptOffsetMin = keptRect != null ? keptRect.offsetMin : Vector2.zero;
            var keptOffsetMax = keptRect != null ? keptRect.offsetMax : Vector2.zero;

            // 테두리·그림자, 그리고 배경 그림을 여기서 한 번 그린다.
            base.UpdateGenericUI(isEditor);

            if (keptRect != null)
            {
                SetRectAnchor(keptRect, keptAnchorMax, keptAnchorMin, isEditor);
                SetRectOffset(keptRect, keptOffsetMax, keptOffsetMin, isEditor);
            }

            // 테두리 자식이 없으면 base가 배경까지 통째로 건너뛴다 — 그림·색은 여기서 다시 정한다.
            // 같은 값을 두 번 넣는 것뿐이라 겹쳐도 문제없다.
            var fill = ResolveFill();

            ApplyImage(backgroundImage, imageColor, isEditor);
            ApplyImage(fill, fillColor, isEditor);

            if (isEditor)
            {
                UpdateSlider(true);

                EndEditor(slider.fillRect, fillArea);

                if (backgroundImage != null) EndEditor(backgroundImage);
                if (fill != null) EndEditor(fill);
            }
            else
            {
                UpdateSlider();
            }
        }

        /// <summary>채움 색을 바꾸고 바로 반영한다 — 남은 양에 따라 초록·빨강으로 바꾸는 식.</summary>
        public void SetFillColor(Color color, bool isEditor = false)
        {
            fillColor = color;

            UpdateGenericUI(isEditor);
        }

        // OutlineUI는 "Image"라는 이름의 자식을 찾지만 슬라이더의 본판은 Background다.
        protected override ImageUI ResolveCoreImage(bool isEditor)
        {
            if (background != null) return background;

            var node = RectTransform.Find("Background");

            return node != null ? node.GetComponent<ImageUI>() : null;
        }

        // 채움은 슬라이더가 이미 fillRect로 들고 있다 — 따로 칸을 두지 않는다.
        private ImageUI ResolveFill()
        {
            var rect = slider != null ? slider.fillRect : null;

            return rect != null ? rect.GetComponent<ImageUI>() : null;
        }

        private void ApplyImage(ImageUI image, Color color, bool isEditor)
        {
            if (image == null) return;

            if (targetSprite) SetSprite(image, targetSprite, isEditor);

            SetColor(image, color, isEditor);
            SetPixelsPerUnit(image, unitMultiplier, isEditor);
        }

        private void UpdateSlider(bool isEditor = false)
        {
            SetRectOffset(fillArea, -areaOffset - fillOffset, areaOffset + fillOffset, isEditor);
            SetRectOffset(slider.fillRect, fillOffset, -fillOffset, isEditor);
        }
    }
}
