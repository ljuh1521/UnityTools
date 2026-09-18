using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.UI
{
    public class ShadowUI : GenericUI
    {
        [Header("Info")]
        public Sprite targetSprite;
        public float unitMultiplier = 1;
        public Color imageColor = Color.white;

        // 0(기본값)이면 크기를 안 건드린다. 원본 스프라이트 가로세로(픽셀)에 이 배율을 곱해
        // Image/Shadow 크기를 정한다 — 예: 200x300 스프라이트에 1.5를 주면 300x450. Image/Shadow
        // 자식(ImageUI)에 각자 따로 안 들어가도 여기 인스펙터에서 한 번에 설정된다(2026-08-31,
        // "ShadowUI 인스펙터에선 안 보인다"는 지적으로 이 자리로 되돌림 — ImageUI 쪽 필드는
        // 독립된 ImageUI를 따로 쓸 때를 위해 그대로 남겨둔다).
        public float nativeSizeMultiplier = 0f;

        [Header("Shadow")]
        public Vector2 shadowOffset;
        public Color shadowColor = Color.black;

        // 그림자 기능 자체를 껐다 켰다 하는 스위치 — 코드에서 명시적으로 켜고 끌 수 있게 한다
        // (OutlineUI.outlineEnabled와 같은 방식, 2026-08-31 사용자 요청).
        public bool shadowEnabled = true;

        // 기본 UI 셰이더는 색을 텍스처 색과 곱해서, 원본 스프라이트가 흰색이 아니면 shadowColor가
        // 그대로 안 나온다 — 텍스처 알파(모양)만 쓰고 RGB는 지정한 색 그대로 나오는 UI/Silhouette
        // 셰이더를 Shadow 레이어에 강제한다(2026-08-31, 사용자 지적).
        private static Material _silhouetteMaterial;
        private static Material SilhouetteMaterial => _silhouetteMaterial ??= Resources.Load<Material>("Materials/UISilhouette");

        public override void UpdateGenericUI(bool isEditor)
        {
            base.UpdateGenericUI(isEditor);

            if (isEditor)
            {
                var image = RectTransform.Find("Image").GetComponent<ImageUI>();
                var shadow = RectTransform.Find("Shadow").GetComponent<ImageUI>();
                var shadowTrans = shadow.GetComponent<RectTransform>();
            
                // 🔹 변경 감지 등록

                UpdateImage(shadow, image);
                UpdateShadow(shadowTrans, shadow);
            
                EndEditor(shadow, image);
            }
            else
            {
                var image = Get<ImageUI>(UIId.Of("Image"));
                var shadow = Get<ImageUI>(UIId.Of("Shadow"));
                var shadowTrans = GetTransform(UIId.Of("Shadow"));
            
                UpdateImage(shadow, image);
                UpdateShadow(shadowTrans, shadow);
            }
        }
        
        // 런타임에서 그림자를 명시적으로 켜고 끈다 — 색·오프셋은 그대로 기억해뒀다가, 다시 켜면
        // 원래 값으로 바로 돌아온다(OutlineUI.SetOutlineEnabled와 같은 방식, 2026-08-31 사용자 요청).
        public void SetShadowEnabled(bool enabled, bool isEditor = false)
        {
            shadowEnabled = enabled;

            UpdateGenericUI(isEditor);
        }

        private void UpdateImage(ImageUI shadow, ImageUI image, bool isEditor = false)
        {
            if (targetSprite)
            {
                SetSprite(shadow, targetSprite, isEditor);
                SetSprite(image, targetSprite, isEditor);
            }

            SetPixelsPerUnit(shadow, unitMultiplier, isEditor);
            SetPixelsPerUnit(image, unitMultiplier, isEditor);
            SetColor(image, imageColor, isEditor);

            if (nativeSizeMultiplier > 0f && targetSprite)
            {
                SetRectSizeFromSprite(image.RectTransform, targetSprite, nativeSizeMultiplier, isEditor);
                SetRectSizeFromSprite(shadow.RectTransform, targetSprite, nativeSizeMultiplier, isEditor);
            }
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
        }
    }
}

