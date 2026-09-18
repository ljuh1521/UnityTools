using UnityEngine;

namespace UnityTools.UI
{
    /// <summary>
    /// "판+글자가 한 벌"인 공용 파트(Image_Text.prefab)의 루트 — 원래는 그냥 GenericUI였는데,
    /// 안쪽 판·글자가 전부 이 루트를 꽉 채우는 늘어나는 앵커라서 nativeSizeMultiplier를 쓸
    /// 자리가 없었다(OutlineUI와 같은 문제, 2026-08-31 사용자 지적). OutlineUI와 같은 방식으로
    /// 이 컴포넌트 자신의 RectTransform을 안쪽 이미지의 원본 스프라이트 비율로 정한다.
    /// </summary>
    public class ImageTextUI : GenericUI
    {
        // 0(기본값)이면 크기를 안 건드린다.
        public float nativeSizeMultiplier = 0f;

        public override void UpdateGenericUI(bool isEditor)
        {
            base.UpdateGenericUI(isEditor);

            if (nativeSizeMultiplier <= 0f) return;

            // 안쪽 판(ImageUnit)은 UIName.None으로 등록돼 있어(PopupBuilder.AddImageText) Get<T>로
            // 못 찾는다 — GetComponentInChildren으로 직접 찾는다.
            var image = GetComponentInChildren<ImageUI>(true);
            if (image == null || image.image.sprite == null) return;

            var oldRootSize = RectTransform.sizeDelta;

            SetRectSizeFromSprite(RectTransform, image.image.sprite, nativeSizeMultiplier, isEditor);

            // 텍스트 박스도 같이 키운다 — 늘어나는 앵커(Image_TextLocal의 Text_Localize 등)는
            // 루트가 커지면 이미 저절로 따라가므로 건드리지 않는다. 고정 앵커로 크기를 직접
            // 지정하는 텍스트(Image_AutoSizeText·Image_GridText의 "Text")만, 루트가 이번에
            // 커진 비율만큼 그대로 같이 키운다 — 매번 "원본 대비 배율"이 아니라 "이번에 바뀐
            // 비율"로 곱해서, 같은 배율로 두 번 눌러도 또 커지지 않는다(2026-08-31, 사용자 요청).
            var newRootSize = RectTransform.sizeDelta;
            if (oldRootSize.x == 0f || oldRootSize.y == 0f) return;

            var ratio = new Vector2(newRootSize.x / oldRootSize.x, newRootSize.y / oldRootSize.y);
            if (ratio == Vector2.one) return;

            foreach (var text in GetComponentsInChildren<TextUI>(true))
            {
                var rt = text.RectTransform;
                if (rt.anchorMin != rt.anchorMax) continue; // 늘어나는 앵커는 이미 따라간다

                SetRectSize(rt, Vector2.Scale(rt.sizeDelta, ratio), isEditor);
            }
        }
    }
}
