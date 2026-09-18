using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.UI
{
    public class OutlineShadowUI : OutlineUI
    {
        [Header("Shadow")]
        public Vector2 shadowOffset;
        public Color shadowColor = Color.black;

        // 그림자 기능 자체를 껐다 켰다 하는 스위치(ShadowUI.shadowEnabled와 같은 방식,
        // 2026-08-31 사용자 요청).
        public bool shadowEnabled = true;

        public override void UpdateGenericUI(bool isEditor)
        {
            base.UpdateGenericUI(isEditor);

            // Shadow 자식이 없는 곳(예: 그림자 없이 이 클래스를 상속만 하는 경우, 2026-09-02)은
            // 조용히 아무 것도 안 한다 — OutlineUI의 같은 가드와 동일한 이유.
            if (isEditor)
            {
                var shadowTf = RectTransform.Find("Shadow");
                if (shadowTf == null) return;

                var shadow = shadowTf.GetComponent<ImageUI>();
                if (shadow == null) return;

                UpdateShadow(shadowTf.GetComponent<RectTransform>(), shadow, true);

                EndEditor(shadow);
            }
            else
            {
                var shadow = Get<ImageUI>(UIId.Of("Shadow"));
                if (shadow == null) return;

                UpdateShadow(GetTransform(UIId.Of("Shadow")), shadow);
            }
        }
        
        // 런타임에서 그림자를 명시적으로 켜고 끈다(ShadowUI.SetShadowEnabled와 같은 방식,
        // 2026-08-31 사용자 요청).
        public void SetShadowEnabled(bool enabled, bool isEditor = false)
        {
            shadowEnabled = enabled;

            UpdateGenericUI(isEditor);
        }

        private void UpdateShadow(RectTransform shadowTrans, ImageUI shadow, bool isEditor = false)
        {
            SetRectPosition(shadowTrans, shadowOffset, isEditor);

            if (targetSprite)
            {
                SetSprite(shadow, targetSprite, isEditor);
            }

            SetPixelsPerUnit(shadow, unitMultiplier, isEditor);
            SetColor(shadow, shadowColor, isEditor);
            SetMaterial(shadow, SilhouetteMaterial, isEditor);
            SetRaycastTarget(shadow, false, isEditor);
            shadow.SetActive(shadowEnabled);

            // Shadow는 Outline/Image와 달리 늘어나는 앵커가 아니라 독립된 자리(shadowOffset로만
            // 옮기는 자유 형제)라, 부모(OutlineUI가 상속받은 nativeSizeMultiplier)를 바꿔도 저절로
            // 안 따라온다 — 여기서 직접 같은 배율로 맞춘다.
            if (nativeSizeMultiplier > 0f && targetSprite)
            {
                SetRectSizeFromSprite(shadowTrans, targetSprite, nativeSizeMultiplier, isEditor);
            }
        }
    }
}

