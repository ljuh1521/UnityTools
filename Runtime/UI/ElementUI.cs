using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityTools.UI
{
    public class ElementUI : MonoBehaviour, IElementUI
    {
        // 이름표는 숫자로 담는다 — 프로젝트마다 다른 enum을 패키지가 알 수 없기 때문이다.
        // 직렬화 이름(elementId)과 저장 형태(정수)가 예전 enum 필드와 같으므로,
        // 이미 만들어 둔 프리팹의 이름표 값은 그대로 이어진다.
        [SerializeField, UIId] private int elementId = UIId.None;
        [SerializeField] private RectTransform rectTransform;

        public int Id => elementId;
        public RectTransform RectTransform => rectTransform;
        public GameObject GameObject => rectTransform.gameObject;
        public bool ActiveSelf => gameObject.activeSelf;
        
        // 인스펙터 설정
        // 확인 방법
        // var props = imageObject.GetIterator();
        // while (props.NextVisible(true))
        // {
        //     Debug.Log($"Property: {props.name}({props.type}) (displayName: {props.displayName})");
        // }

        protected virtual void OnEnable() { }

        protected virtual void OnDisable() { }

        protected virtual void Update() { }

        public virtual void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        public virtual void SetParent(RectTransform parent)
        {
            rectTransform.SetParent(parent);
        }

        public virtual void SetPosition(Vector2 pos)
        {
            rectTransform.anchoredPosition = pos;
        }
        public virtual void SetSize(Vector2 size)
        {
            rectTransform.sizeDelta = size;
        }

        protected void SetSprite(ImageUI image, Sprite sprite, bool isEditor)
        {
            if (isEditor)
            {
#if UNITY_EDITOR
                var imageObject = new SerializedObject(image.image);

                imageObject.FindProperty("m_Sprite").objectReferenceValue = sprite;
                imageObject.ApplyModifiedProperties();
                return;
#endif
            }
            
            image.Sprite = sprite;
        }

        // 기본 UI 머티리얼(null=Unity 기본)이 아니라 다른 셰이더를 강제로 씌운다 — OutlineUI/
        // ShadowUI가 원본이 흰색이 아닌 스프라이트에도 테두리·그림자 색이 정확히 나오게
        // UI/Silhouette 셰이더를 강제할 때 쓴다(2026-08-31, 사용자 지적).
        protected void SetMaterial(ImageUI image, Material material, bool isEditor)
        {
            if (isEditor)
            {
#if UNITY_EDITOR
                var imageObject = new SerializedObject(image.image);

                imageObject.FindProperty("m_Material").objectReferenceValue = material;
                imageObject.ApplyModifiedProperties();
                return;
#endif
            }

            image.image.material = material;
        }

        protected void SetColor(ImageUI image, Color color, bool isEditor)
        {
            if (isEditor)
            {
#if UNITY_EDITOR
                var imageObject = new SerializedObject(image.image);

                imageObject.FindProperty("m_Color").colorValue = color;
                imageObject.ApplyModifiedProperties();
                return;
#endif
            }
            
            image.Color = color;
        }

        // 꾸밈 전용 레이어(테두리·그림자 등)가 실제 그림 밖으로 삐져나오면 그 자리의 클릭을
        // 가로챌 수 있다 — raycastTarget을 꺼서 클릭이 그대로 아래(진짜 버튼)로 통과하게 한다
        // (2026-09-01, OutlineUI가 카드 바깥으로 삐져나오면서 옆 카드 클릭을 먹던 문제).
        protected void SetRaycastTarget(ImageUI image, bool raycastTarget, bool isEditor)
        {
            if (isEditor)
            {
#if UNITY_EDITOR
                var imageObject = new SerializedObject(image.image);

                imageObject.FindProperty("m_RaycastTarget").boolValue = raycastTarget;
                imageObject.ApplyModifiedProperties();
                return;
#endif
            }

            image.image.raycastTarget = raycastTarget;
        }

        // UI/Outline 셰이더는 UV가 사각형 전체에 선형으로(0..1) 걸쳐 있다고 가정한다 — Sliced
        // 타입은 보더가 0이어도 9분할 메쉬를 그대로 만들어 UV가 구간별로 나뉘어, 그 가정이
        // 깨진다(2026-09-01, 링이 안 보이던 원인). Outline 레이어는 항상 Simple로 강제한다.
        protected void SetImageType(ImageUI image, UnityEngine.UI.Image.Type type, bool isEditor)
        {
            if (isEditor)
            {
#if UNITY_EDITOR
                var imageObject = new SerializedObject(image.image);

                imageObject.FindProperty("m_Type").enumValueIndex = (int)type;
                imageObject.ApplyModifiedProperties();
                return;
#endif
            }

            image.image.type = type;
        }

        protected void SetPixelsPerUnit(ImageUI image, float unit, bool isEditor)
        {
            if (isEditor)
            {
#if UNITY_EDITOR
                var imageObject = new SerializedObject(image.image);

                imageObject.FindProperty("m_PixelsPerUnitMultiplier").floatValue = unit;
                imageObject.ApplyModifiedProperties();
                return;
#endif                
            }
            
            image.image.pixelsPerUnitMultiplier = unit;
        }   
        
        protected void SetRectPosition(RectTransform rectTransform, Vector2 anchorPos, bool isEditor)
        {
            if (isEditor)
            {
#if UNITY_EDITOR
                var rectObject = new SerializedObject(rectTransform);

                rectObject.FindProperty("m_AnchoredPosition").vector2Value = anchorPos;
                rectObject.ApplyModifiedProperties();
                return;
#endif
            }
            
            rectTransform.anchoredPosition = anchorPos;
        }    
        
        protected void SetRectAnchor(RectTransform rectTransform, Vector2 anchorMax, Vector2 anchorMin, bool isEditor)
        {
            if (isEditor)
            {
#if UNITY_EDITOR
                var rectObject = new SerializedObject(rectTransform);
                
                rectObject.FindProperty("m_AnchorMax").vector2Value = anchorMax;
                rectObject.FindProperty("m_AnchorMin").vector2Value = anchorMin;
                rectObject.ApplyModifiedProperties();
                return;
#endif
            }
            
            rectTransform.anchorMax = anchorMax;
            rectTransform.anchorMin = anchorMin;
        }    
        
        protected void SetRectOffset(RectTransform rect, Vector2 offsetMax, Vector2 offsetMin, bool isEditor)
        {
            if (isEditor)
            {
#if UNITY_EDITOR
                var rectObject = new SerializedObject(rect);
                var anchoredPosition = rectObject.FindProperty("m_AnchoredPosition");
                var sizeDelta = rectObject.FindProperty("m_SizeDelta");
                var pivot = rectObject.FindProperty("m_Pivot");

                Vector2 max = offsetMax - (anchoredPosition.vector2Value + Vector2.Scale(sizeDelta.vector2Value, Vector2.one - pivot.vector2Value));
                sizeDelta.vector2Value += max;
                anchoredPosition.vector2Value += Vector2.Scale(max, pivot.vector2Value);

                Vector2 min = offsetMin - (anchoredPosition.vector2Value - Vector2.Scale(sizeDelta.vector2Value, pivot.vector2Value));
                sizeDelta.vector2Value -= min;
                anchoredPosition.vector2Value += Vector2.Scale(min, Vector2.one - pivot.vector2Value);

                rectObject.ApplyModifiedProperties();
                return;
#endif
            }

            rect.offsetMax = offsetMax;
            rect.offsetMin = offsetMin;
        }

        // 원본 스프라이트의 가로세로(픽셀, PPU와 무관한 실제 임포트 크기)에 배율을 곱해 크기를
        // 정한다 — 예: 200x300 스프라이트에 1.5를 주면 300x450. ShadowUI 전용이 아니라 이
        // ElementUI를 상속하는 아무 컴포넌트에서나 쓸 수 있게 여기 뒀다(2026-08-31, 사용자 요청).
        protected void SetRectSizeFromSprite(RectTransform rectTransform, Sprite sprite, float sizeMultiplier, bool isEditor)
        {
            if (sprite == null) return;

            // 9슬라이스로 쓰는 "보드"류 스프라이트(임포트 설정에 border가 있는 것)는 원본
            // 비율대로 늘리면 안 된다 — 보드는 상자 크기에 맞춰 자유롭게 늘어나는 게 의도된
            // 디자인이라, 원본 텍스처 크기·비율 자체가 최종 크기와 무관하다(2026-08-31, 사용자
            // 지적 — "보드는 원본과 비율이 다른 경우가 대부분"). 조용히 무시하지 않고 경고를
            // 남긴다 — 설정을 잘못 넣은 건지 바로 알 수 있게.
            if (sprite.border != Vector4.zero)
            {
                Debug.LogWarning($"[크기 자동 지정] {sprite.name}은 9슬라이스(border) 스프라이트라 " +
                                  "원본 비율로 크기를 못 정합니다 — 보드류는 상자 크기에 맞춰 늘어나는 게 의도된 디자인입니다.");
                return;
            }

            var targetSize = new Vector2(sprite.rect.width, sprite.rect.height) * sizeMultiplier;
            var size = targetSize;

            // 늘어나는 앵커(anchorMin≠anchorMax)면 sizeDelta가 절대 크기가 아니라 "부모 크기 ×
            // 앵커 폭 + sizeDelta"의 일부다(유니티 공식) — 지금 이 시점의 부모 실제 크기를 읽어서
            // 최종 렌더 크기가 targetSize가 되도록 역산한 sizeDelta를 넣는다. 멈추지 않고 그
            // 자리에서 맞춘다(2026-08-31, 사용자 요청).
            if (rectTransform.anchorMin != rectTransform.anchorMax)
            {
                if (rectTransform.parent is not RectTransform parentRect)
                {
                    Debug.LogWarning($"[크기 자동 지정] {rectTransform.name}은 늘어나는 앵커인데 부모가 " +
                                      "RectTransform이 아니라 기준 크기를 계산할 수 없습니다.");
                    return;
                }

                var anchorSpan = rectTransform.anchorMax - rectTransform.anchorMin;
                size = targetSize - Vector2.Scale(parentRect.rect.size, anchorSpan);
            }

            SetRectSize(rectTransform, size, isEditor);
        }

        // sizeDelta를 직접 정한다 — SetRectSizeFromSprite가 계산한 값을 실제로 쓰는 곳, 그리고
        // 텍스트 박스처럼 스프라이트 없이 크기만 직접 정할 때도 재사용한다(2026-08-31, 사용자
        // 요청 — 이미지 배율에 맞춰 텍스트 박스도 같이 커지게).
        protected void SetRectSize(RectTransform rectTransform, Vector2 size, bool isEditor)
        {
            if (isEditor)
            {
#if UNITY_EDITOR
                var rectObject = new SerializedObject(rectTransform);

                rectObject.FindProperty("m_SizeDelta").vector2Value = size;
                rectObject.ApplyModifiedProperties();
                return;
#endif
            }

            rectTransform.sizeDelta = size;
        }
    }
}