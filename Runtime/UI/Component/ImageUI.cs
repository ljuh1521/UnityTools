using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.UI
{
    public class ImageUI : ElementUI
    {
        [Space(10)]
        public Image image;

        // 0(기본값)이면 안 건드린다. 원본 스프라이트 가로세로(픽셀, PPU와 무관한 실제 임포트
        // 크기)에 이 배율을 곱해 이 오브젝트의 크기를 정한다 — 예: 200x300 스프라이트에 1.5를
        // 주면 300x450, 0.1이면 20x30. 인스펙터의 "➤ Apply UI" 버튼(ImageUIEditor)을 눌러야
        // 적용된다 — GenericUI 계열이 이미 같은 방식(UpdateEditor.cs)이라 그대로 맞췄다
        // (2026-08-31, 사용자 지적 — 자동 적용(OnValidate)보다 이 방식이 낫다).
        public float nativeSizeMultiplier = 0f;

        // 두 상태의 색. targetSprite/disableSprite가 그림 두 장을 들고 있는 것과 같은 꼴로,
        // 이 그림이 "켜졌을 때"와 "죽었을 때" 색을 자기가 들고 있다. 둘 다 적혀 있어야 켜지고,
        // 알파가 0(기본값)이면 안 적은 것으로 본다 — nativeSizeMultiplier와 같은 "0이면 noop" 관례다.
        //
        // 바꾸는 쪽이 원래 색을 기억하지 않아도 되게 하려는 것이다. 기억하게 만들면 HeroCardUI가
        // 겪은 "기준값 1회 캡처" 함정이 그대로 재현된다(Variant가 자기 몫을 다시 캡처하지 않아
        // 조용히 깨진다). Variant가 다른 색을 쓰려면 자기 두 값을 적으면 되므로 상속으로도 안 어긋난다.
        //
        // 전역 한 쌍인 Resource.GetActiveTint(흰색/회색)와는 쓰는 자리가 다르다 — 그건 원본이
        // 흰색인 아이콘용이고, 자기 색이 칠해진 그림에 쓰면 그 색을 지운다(2026-09-14에 실제로 그랬다).
        // 지금 어느 쪽 색인가. 인스펙터에서 끄고 "➤ Apply UI"를 누르면 죽은 색을 게임을 돌리지
        // 않고 그 자리에서 볼 수 있다 — ImageSwitchUI.state·OutlineUI.outlineEnabled와 같은 방식이다.
        // ⚠ 에디터에서 누른 결과는 프리팹에 저장된다. 끈 채로 누르면 죽은 색이 박힌 채 저장되므로
        // (실행 중에는 SetPressable이 매번 바로잡지만 에디터에서는 그대로 보인다) 확인이 끝나면
        // 다시 켜고 한 번 더 누른다. isActive가 아니라 colorActive인 것은 ElementUI.SetActive
        // (게임오브젝트 켜고 끄기)와 헷갈리지 않게 하려는 것이다.
        [Header("Color")]
        public bool colorActive = true;
        public Color activeColor;
        public Color inactiveColor;

        // 둘 다 알파>0이어야 켜진다. 뒤집어 말하면 "알파 0으로 투명하게 죽이기"는 이걸로 못 한다 —
        // 그건 SetActive나 CanvasGroup 몫이다.
        //
        // 실행 중에 이걸 모는 건 지금 ButtonUI가 비용 판(UIName.Cost)에 거는 경로 하나뿐이다.
        // 다른 그림에 두 색을 채워 넣어도 부르는 쪽이 없으면 인스펙터 버튼으로만 바뀐다.
        public bool HasColorStates => activeColor.a > 0f && inactiveColor.a > 0f;

        public Sprite Sprite
        {
            get => image.sprite;
            set => image.sprite = value;
        }
        public Color Color
        {
            get => image.color;
            set => image.color = value;
        }
        public float FillAmount
        {
            set => image.fillAmount = value;
        }

        // nativeSizeMultiplier가 설정돼 있으면 지금 스프라이트 크기에 맞춰 이 RectTransform의
        // 크기를 정한다. 생성기나 이 컴포넌트를 감싸는 다른 컴포넌트(ShadowUI 등)가 스프라이트를
        // 정한 뒤 명시적으로 부른다 — 에디터에서는 ImageUIEditor의 "➤ Apply UI" 버튼이 이걸
        // isEditor:true로 부른다.
        public void ApplyNativeSize(bool isEditor = false)
        {
            if (nativeSizeMultiplier <= 0f || image == null || image.sprite == null) return;

            SetRectSizeFromSprite(RectTransform, image.sprite, nativeSizeMultiplier, isEditor);
        }

        // 상태를 정하고 그 색을 입힌다 — ImageSwitchUI.SetState·OutlineUI.SetOutlineEnabled와
        // 같은 꼴이다. ApplyColor와 이름을 나눠 뒀다(한 이름으로 겹쳐 두면 ApplyColor(true)가
        // "켜진 색으로" 인지 "에디터에서" 인지 부르는 자리마다 갈린다).
        public void SetColorActive(bool active, bool isEditor = false)
        {
            colorActive = active;

            ApplyColor(isEditor);
        }

        /// <summary>지금 <see cref="colorActive"/> 그대로 색을 입힌다. 두 색을 다 안 적었으면 아무것도 안 한다.</summary>
        public void ApplyColor(bool isEditor = false)
        {
            if (!HasColorStates || image == null) return;

            SetColor(this, colorActive ? activeColor : inactiveColor, isEditor);
        }
    }
}
