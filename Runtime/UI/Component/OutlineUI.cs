using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityTools.UI;

namespace UnityTools.UI
{
    [ExecuteAlways]
    public class OutlineUI : GenericUI
    {
        [Header("Info")]
        public Sprite targetSprite;
        public float unitMultiplier = 1;
        public Color imageColor = Color.white;

        // 0(기본값)이면 크기를 안 건드린다. Outline/Image는 이 컴포넌트 자신의 루트를 꽉 채우는
        // 늘어나는 앵커라서(Image는 거기서 outlineSize만큼 안쪽으로 더 들어간다) — 그 자식들
        // sizeDelta를 직접 바꾸는 게 아니라 **이 컴포넌트 자신의 RectTransform**을 원본
        // 스프라이트 가로세로 × 이 배율로 정한다. 자신이 다시 부모에 늘어나는 앵커로 꽉 차 있는
        // 경우(TitledBoard 등)엔 효과가 없다 — 그런 자리엔 안 쓰면 된다(2026-08-31, 사용자 요청 —
        // ShadowUI뿐 아니라 OutlineUI·OutlineShadowUI에도 필요).
        public float nativeSizeMultiplier = 0f;

        [Header("Outline")]
        public float outlineSize;
        public Color outlineColor = Color.white;

        // 테두리 기능 자체를 껐다 켰다 하는 스위치 — outlineSize=0으로 두는 방식과 별개로,
        // 코드에서 명시적으로 켜고 끌 수 있게 한다(2026-08-31, 사용자 요청). 꺼져 있으면
        // outlineSize·outlineColor 값과 상관없이 Outline 레이어를 숨긴다.
        public bool outlineEnabled = true;

        // 기본 UI 셰이더는 색을 텍스처 색과 곱해서, 원본 스프라이트가 흰색이 아니면 outlineColor가
        // 그대로 안 나온다(예: 파란 아이콘 + 빨간 테두리 = 탁한 보라) — 텍스처 알파(모양)만 쓰고
        // RGB는 지정한 색 그대로 나오는 UI/Silhouette 셰이더를 Shadow 레이어에 강제한다
        // (2026-08-31, 사용자 지적). protected — OutlineShadowUI(자식)의 Shadow 레이어가 재사용한다.
        private static Material _silhouetteMaterial;
        protected static Material SilhouetteMaterial => _silhouetteMaterial ??= Resources.Load<Material>("Materials/UISilhouette");

        // Outline 레이어 전용 — "사각형 틀 안쪽으로 밀어넣기"(Image를 inset하는 기존 방식)는
        // 사각형이 아닌 실루엣(아치·별 모양 카드 프레임 등)에서 두께가 고르지 않게 나와서,
        // 원본 알파의 가장자리를 따라 링 모양으로 직접 번지는 UI/Outline 셰이더로 바꿨다
        // (2026-09-01, SSR 카드 프레임에서 확인된 문제).
        private static Material _outlineMaterial;
        private static Material OutlineMaterial => _outlineMaterial ??= Resources.Load<Material>("Materials/UIOutline");

        public override void UpdateGenericUI(bool isEditor)
        {
            base.UpdateGenericUI(isEditor);

            if (nativeSizeMultiplier > 0f && targetSprite)
            {
                SetRectSizeFromSprite(RectTransform, targetSprite, nativeSizeMultiplier, isEditor);
            }

            var outline = ResolveOutline(isEditor);
            var image = ResolveCoreImage(isEditor);

            // 이 컴포넌트를 상속하되 Outline·Image 자식이 없는 곳(예: 테두리 없는 일반 버튼에
            // ButtonUI가 이 클래스를 상속하게 된 경우, 2026-09-02)이 있을 수 있다 — 그런 곳은
            // 조용히 아무 것도 안 한다.
            if (outline == null || image == null) return;

            if (isEditor)
            {
                UpdateImage(outline, image, true);
                UpdateOutline(outline, image, true);

                EndEditor(outline, image);
            }
            else
            {
                UpdateImage(outline, image);
                UpdateOutline(outline, image);
            }
        }

        // "Image" 자식을 어디서 찾을지 — 기본은 이름/UIName으로 찾지만, ButtonUI처럼 이미 직접
        // 지정된(인스펙터에 저장되는) image 필드가 있는 곳은 그걸 그대로 재사용하도록 오버라이드한다
        // (2026-09-02, ButtonOutlineShadowUI 기능을 ButtonUI에 흡수하면서 추가). Outline은 지금
        // 이 방식을 바꿔 쓰는 곳이 없어 virtual로 안 열어뒀다 — 필요해지면 그때 연다.
        // 이름표 숫자를 못박지 않고 이름으로 찾는다 — 프로젝트마다 enum 번호가 다르기 때문이다.
        protected virtual ImageUI ResolveCoreImage(bool isEditor) => ResolveChild("Image", UIId.Of("Image"), isEditor);

        protected ImageUI ResolveOutline(bool isEditor) => ResolveChild("Outline", UIId.Of("Outline"), isEditor);

        private ImageUI ResolveChild(string editorName, int id, bool isEditor)
        {
            if (isEditor)
            {
                var t = RectTransform.Find(editorName);
                return t != null ? t.GetComponent<ImageUI>() : null;
            }
            return Get<ImageUI>(id);
        }

        public void SetOutlineColor(Color outline, Color image, bool isEditor = false)
        {
            outlineColor = outline;
            imageColor = image;

            UpdateGenericUI(isEditor);
        }

        // 런타임에서 테두리를 명시적으로 켜고 끈다 — outlineSize를 0으로 두는 것과 달리 굵기·색은
        // 그대로 기억해뒀다가, 다시 켜면 원래 값으로 바로 돌아온다(2026-08-31, 사용자 요청).
        public void SetOutlineEnabled(bool enabled, bool isEditor = false)
        {
            outlineEnabled = enabled;

            UpdateGenericUI(isEditor);
        }

        private void UpdateImage(ImageUI outline, ImageUI image, bool isEditor = false)
        {
            // imageColor도 targetSprite와 같은 조건으로 묶는다 — 이 컴포넌트가 image의 스프라이트를
            // 직접 정하는(targetSprite가 있는) 경우에만 색도 같이 정한다. 무조건 씌우면 ButtonUI처럼
            // image를 자기가 이미 관리하는 곳(targetSprite 없이 이 컴포넌트를 상속만 하는 경우)에서
            // 실제 아이콘 색을 매번 imageColor(기본 흰색)로 되돌려버린다(2026-09-02, 코드 리뷰로 발견
            // — SynergySceneUI.cs가 런타임에 버튼 색을 Lime/Gray로 직접 바꾸는 경우가 실제로 있었다).
            if (targetSprite)
            {
                SetSprite(outline, targetSprite, isEditor);
                SetSprite(image, targetSprite, isEditor);
                SetColor(image, imageColor, isEditor);
            }

            SetPixelsPerUnit(outline, unitMultiplier, isEditor);
            SetPixelsPerUnit(image, unitMultiplier, isEditor);
        }
        
        private void UpdateOutline(ImageUI outline, ImageUI image, bool isEditor = false)
        {
            var imageTrans = image.image.rectTransform;

            // Image는 카드 박스에 꽉 맞춰 그대로 둔다 — 실제 그림이라 밀어넣지 않는다.
            SetRectAnchor(imageTrans, Vector2.one, Vector2.zero, isEditor);
            SetRectOffset(imageTrans, Vector2.zero, Vector2.zero, isEditor);
            SetColor(outline, outlineColor, isEditor);
            SetRaycastTarget(outline, false, isEditor);

            var outlineTrans = outline.RectTransform;

            // Outline은 반대로 바깥으로 outlineSize만큼 더 키운다 — 테두리를 원본 그림 바깥으로
            // 실제로 삐져나오게 하려면, 그릴 자리 자체가 원본보다 커야 한다(2026-09-01, 사용자
            // 지적 — "외곽선이 실제 크기 이상 벗어나지 않는다").
            SetRectAnchor(outlineTrans, Vector2.one, Vector2.zero, isEditor);
            SetRectOffset(outlineTrans, Vector2.one * outlineSize, -Vector2.one * outlineSize, isEditor);

            // 9슬라이스(보더) 있는 스프라이트와 없는 스프라이트는 테두리를 만드는 방식 자체가
            // 다르다(2026-09-02, 카드 새 디자인에서 발견 — 사용자 지적: "외곽선과 이미지의
            // 테두리 라인이 안 맞아").
            bool hasBorder = targetSprite != null && targetSprite.border != Vector4.zero;

            if (hasBorder)
            {
                // 보더 있는 스프라이트(둥근 사각 카드판 등)는 같은 스프라이트를 Sliced로 그대로
                // 키워서 뒤에 깐다 — 9슬라이스는 모서리 픽셀 크기를 안 늘리므로, 커진 만큼 순수
                // 링(모서리 포함)만 원본 밖으로 삐져나오고 곡률은 원본과 항상 정확히 같다.
                // 알파-링 셰이더(아래 else)는 "반경만큼 도장 찍듯 번지는" 방식이라 원본이 이미
                // 둥근 모서리면 그보다 더 둥글게 뭉개져 곡률이 안 맞았다 — 실측(코너 확대 비교)
                // 으로 확인.
                //
                // Simple(전체 스케일)로 바꿔보는 시도도 있었다(2026-09-04, 사용자 제안 —
                // board_demon king selection_256의 깨진 보더 문제를 피하려고) — 실제 플레이로
                // 확인해보니 기대와 달라 되돌림. 다시 시도할 계획이면 이 근처 git 이력을 먼저 볼 것.
                SetMaterial(outline, SilhouetteMaterial, isEditor);
                SetImageType(outline, UnityEngine.UI.Image.Type.Sliced, isEditor);

                var oldWidthModifier = outline.GetComponent<OutlineWidthModifier>();
                if (oldWidthModifier != null) oldWidthModifier.enabled = false;
            }
            else
            {
                // 사각형이 아닌 실루엣(별·아치 등)은 보더 개념이 없어 위 방식을 못 쓴다 — 원본
                // 알파의 가장자리를 따라 링을 직접 그리는 기존 방식을 그대로 쓴다.
                SetMaterial(outline, OutlineMaterial, isEditor);
                SetImageType(outline, UnityEngine.UI.Image.Type.Simple, isEditor);

                var coreRect = imageTrans.rect;
                var outlineRect = outlineTrans.rect;

                var widthUV = coreRect.width > 0f && coreRect.height > 0f
                    ? new Vector2(outlineSize / coreRect.width, outlineSize / coreRect.height)
                    : Vector2.zero;

                // 커진 Outline 쿼드의 UV를 셰이더가 core(원본 크기) 기준 위치로 되돌려 매핑할 때
                // 쓰는 배율.
                var quadScale = coreRect.width > 0f && coreRect.height > 0f
                    ? new Vector2(outlineRect.width / coreRect.width, outlineRect.height / coreRect.height)
                    : Vector2.one;

                GetBorderFractions(coreRect, out var borderFrac, out var borderUV);
                var spriteUVRect = GetSpriteUVRect(image.Sprite);

                var widthModifier = outline.GetComponent<OutlineWidthModifier>();
                if (widthModifier == null)
                {
                    widthModifier = outline.gameObject.AddComponent<OutlineWidthModifier>();
                }
                widthModifier.enabled = true;
                widthModifier.SetWidth(widthUV, quadScale, borderFrac, borderUV, spriteUVRect);
            }

            // outlineSize=0이면 뒤 레이어가 원본과 같은 크기라 안 보인다 — 그래도 불필요한
            // 드로우콜을 아예 없애려고 SetActive로 한 번 더 끈다(2026-08-31, 사용자 지적).
            outline.SetActive(outlineEnabled && outlineSize > 0f);
        }

        // 9슬라이스(보더) 스프라이트 대응 — Image 레이어(Sliced)는 unitMultiplier·보더 두께에
        // 맞춰 모서리는 안 늘리고 가운데만 늘린다. Outline은 셰이더 안에서 직접 같은 매핑을
        // 계산하므로(UI/Outline.shader의 MapAxis), 그 계산에 필요한 두 비율을 여기서 구한다
        // (2026-09-01, board_demon king selection_256처럼 보더 있는 스프라이트 대응 — 사용자 지적,
        // "원본의 unitMultiplier는 고려 안 하는거야?").
        // borderFrac: 보더 두께 ÷ core 사각형 크기(렌더 크기 기준, unitMultiplier 반영).
        // borderUV: 보더 두께 ÷ 원본 스프라이트 텍스처 크기(순수 텍스처 좌표 기준).
        private void GetBorderFractions(Rect coreRect, out Vector4 borderFrac, out Vector4 borderUV)
        {
            borderFrac = Vector4.zero;
            borderUV = Vector4.zero;

            if (targetSprite == null || targetSprite.border == Vector4.zero) return;

            var border = targetSprite.border; // x=left, y=bottom, z=right, w=top (텍스처 픽셀)

            var spriteSize = targetSprite.rect.size;
            borderUV = new Vector4(
                spriteSize.x > 0f ? border.x / spriteSize.x : 0f,
                spriteSize.y > 0f ? border.y / spriteSize.y : 0f,
                spriteSize.x > 0f ? border.z / spriteSize.x : 0f,
                spriteSize.y > 0f ? border.w / spriteSize.y : 0f);

            // Unity Image가 보더를 렌더 크기로 바꿀 때 쓰는 것과 같은 공식(pixelsPerUnit ÷
            // 캔버스 referencePixelsPerUnit, 여기에 unitMultiplier) — 조상에 Canvas가 없으면
            // (에디터 미리보기 등) 기본값 100으로 근사한다.
            var canvas = GetComponentInParent<Canvas>();
            var refPpu = canvas != null ? canvas.referencePixelsPerUnit : 100f;
            var effectivePpu = (targetSprite.pixelsPerUnit / refPpu) * unitMultiplier;
            if (effectivePpu <= 0f) return;

            var left = border.x / effectivePpu;
            var right = border.z / effectivePpu;
            var bottom = border.y / effectivePpu;
            var top = border.w / effectivePpu;

            // 보더 합이 박스보다 크면(박스가 아주 작을 때) 비율대로 줄인다 — Unity Image의
            // GetAdjustedBorders와 같은 안전장치.
            if (coreRect.width > 0f && left + right > coreRect.width)
            {
                var ratio = coreRect.width / (left + right);
                left *= ratio; right *= ratio;
            }
            if (coreRect.height > 0f && bottom + top > coreRect.height)
            {
                var ratio = coreRect.height / (bottom + top);
                bottom *= ratio; top *= ratio;
            }

            borderFrac = new Vector4(
                coreRect.width > 0f ? left / coreRect.width : 0f,
                coreRect.height > 0f ? bottom / coreRect.height : 0f,
                coreRect.width > 0f ? right / coreRect.width : 0f,
                coreRect.height > 0f ? top / coreRect.height : 0f);
        }

        // 스프라이트가 아틀라스(Sprite Mode: Multiple, 스킬 아이콘 시트 등)에서 왔으면 텍스처
        // 전체가 아니라 그 안의 작은 조각만 차지한다 — sprite.uv(코너 UV들)의 최소·최대로 실제
        // 차지 영역을 구한다. 이 방식은 패킹 방식(원본 텍스처 그대로 쓰는지, 런타임에 별도
        // 아틀라스로 다시 패킹되는지)과 무관하게 항상 "지금 실제로 쓰는 텍스처 기준 UV"를 준다.
        // 단일 스프라이트(텍스처 전체가 그 스프라이트)면 자동으로 (0,0,1,1)이 나온다 — 별도 분기
        // 없이 그대로 셰이더에 넘겨도 항상 안전하다(2026-09-04, 스킬 아이콘 아웃라인이 엉뚱한
        // 모양으로 나오는 문제로 발견 — UI/Outline.shader가 스프라이트 로컬 좌표를 텍스처 전체
        // 좌표로 착각해 아틀라스의 다른 자리를 샘플링하고 있었다).
        private static Vector4 GetSpriteUVRect(Sprite sprite)
        {
            if (sprite == null || sprite.uv == null || sprite.uv.Length == 0)
                return new Vector4(0f, 0f, 1f, 1f);

            var uvs = sprite.uv;
            Vector2 min = uvs[0], max = uvs[0];
            for (int i = 1; i < uvs.Length; i++)
            {
                min = Vector2.Min(min, uvs[i]);
                max = Vector2.Max(max, uvs[i]);
            }

            return new Vector4(min.x, min.y, Mathf.Max(max.x - min.x, 1e-5f), Mathf.Max(max.y - min.y, 1e-5f));
        }
    }
}