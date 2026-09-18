using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Button = UnityEngine.UI.Button;

namespace UnityTools.UI
{
    // OutlineShadowUI를 상속해 테두리·그림자 기능을 흡수했다(2026-09-02, 사용자 요청 —
    // ButtonOutlineShadowUI와 API가 중복돼 있던 걸 ButtonUI 하나로 합침). 프리팹에 "Outline"·
    // "Shadow" 자식이 없는 기존 버튼은 OutlineUI/OutlineShadowUI 쪽 가드 덕분에 전혀 영향 없다 —
    // 그 자식들을 나중에 추가하기만 하면 이 버튼도 테두리·그림자를 쓸 수 있다.
    public class ButtonUI : OutlineShadowUI, IPointerDownHandler
    {
        [Header("UI")]
        public Button button;
        public ImageUI image;

        // 버튼 기능 자체를 껐다 켰다 하는 스위치 — outlineEnabled·shadowEnabled와 같은 방식
        // (2026-09-07, 사용자 요청). HeroCard.prefab처럼 "표시 전용으로도, 선택 가능한 카드로도"
        // 같은 프리팹을 쓸 때, 표시 전용 화면에서는 이걸 꺼서 클릭·호버 색 반응(Transition)이
        // 전혀 안 뜨게 한다 — Interactable만 끄면 UnityEngine.UI.Button 쪽은 막히지만, ButtonUI
        // 자신이 직접 구현한 OnPointerDown(onPointerDown 이벤트)은 그걸로 안 막혀서 따로 가드한다.
        public bool buttonEnabled = true;

        // 못 누르는 상태의 그림 — 누를 수 있는(able) 상태는 이미 있는 targetSprite(OutlineUI)를
        // 그대로 쓰고, 여긴 못 누를 때만 채운다(2026-09-10, 사용자 요청 — targetSprite를 이미
        // 프로젝트 전체 버튼에 채워둬서 별도 ableSprite 필드가 필요 없어졌다). SetPressable은
        // targetSprite 자체는 절대 안 건드린다 — 되돌릴 때 "원래 able 그림이 뭐였는지" 캐싱해둘
        // 필요가 없어서, HeroCardUI가 겪었던 "배율 기준값 캡처" 함정을 처음부터 피한다.
        [Header("Pressable Sprite")]
        // "지금 누를 수 있는가" — 재료 부족·최대 레벨처럼 매 갱신마다 바뀌는 상태다. buttonEnabled
        // (구조적 on/off)와는 별개고, 최종 interactable은 둘 다 참이어야 한다.
        //
        // 인스펙터에서 끄고 "➤ Apply UI"를 누르면 disableSprite와 비용 판 죽은 색을 게임을 돌리지
        // 않고 그 자리에서 볼 수 있다 — ImageUI.colorActive·ImageSwitchUI.state와 같은 방식이다.
        // ⚠ 누른 결과는 프리팹에 저장되므로 확인이 끝나면 다시 켜고 한 번 더 누른다.
        public bool pressable = true;
        public Sprite disableSprite;

        [Header("Info")]
        public Button.ButtonClickedEvent onClick;
        public Button.ButtonClickedEvent onPointerDown;

        public bool IsEnable
        {
            get => button.enabled;
            set => button.enabled = value;
        }

        public bool IsInteractable
        {
            get => button.interactable;
            set => button.interactable = value;
        }

        // OutlineUI/OutlineShadowUI가 "Image" 자식을 이름·UIName으로 찾는 대신, 이미 인스펙터에
        // 저장돼 있는 이 필드를 그대로 쓰게 한다 — FarmInfoPopupUI.cs·SynergySceneUI.cs 등 기존
        // 코드가 이 필드를 직접 읽고 쓰므로 그대로 둬야 한다.
        protected override ImageUI ResolveCoreImage(bool isEditor) => image;

        // image(ImageUI)에 이미 있는 nativeSizeMultiplier를 여기서 대신 켜준다 — ButtonUI는
        // GenericUI 계열이라 인스펙터 "➤ Apply UI" 버튼이 UpdateGenericUI를 부르는데(UpdateEditor.cs
        // UpdateUI), ImageUI 전용 버튼(UpdateImage)은 GenericUI를 안 타는 타입이라 안 걸린다.
        // 그래서 지금까진 ButtonUI에 붙은 image의 크기 자동 지정이 눌러도 아무 반응이 없었다.
        public override void UpdateGenericUI(bool isEditor)
        {
            base.UpdateGenericUI(isEditor);

            image?.ApplyNativeSize(isEditor);

            // 못 누르는 상태면 disableSprite를 마지막에 덮어쓴다 — targetSprite 필드 자체는 절대
            // 안 건드린다. base.UpdateGenericUI(OutlineUI)가 targetSprite로 이미지를 먼저 그리므로
            // 순서를 반대로 하면 방금 넣은 disableSprite가 지워진다.
            // ⚠ 되돌리는 것도 그 base가 하므로 targetSprite가 비어 있는 버튼은 한 번 덮이면
            // 안 돌아온다 — disableSprite를 쓰려면 targetSprite도 채워야 한다.
            if (!pressable && disableSprite != null && image != null)
                SetSprite(image, disableSprite, isEditor);

            // 버튼 안 비용 판(UIId.Of("Cost"))도 같이 죽인다 — 유니티 버튼의 색 전이는 targetGraphic
            // 한 장에만 닿아서 자식인 판은 그대로 밝게 남는다. 무슨 색으로 할지는 판이 알고 있고
            // (ImageUI의 두 상태 색) 버튼은 "지금 누를 수 있나"만 넘긴다 — 판마다 색이 달라서
            // (주황·초록) 버튼이 그걸 들고 있으면 안 된다. 비용이 글자뿐인 곳은 걸러지고,
            // 두 색을 안 적어둔 판은 ImageUI 쪽에서 알아서 넘어간다.
            var costImage = ResolveCostImage(isEditor);

            if (costImage != null)
            {
                costImage.SetColorActive(pressable, isEditor);

                // colorActive는 그냥 대입이라 이게 없으면 색만 저장되고 상태 플래그는 날아간다 —
                // 다음에 프리팹을 열면 "켜졌다는데 회색"인 상태가 파일에 남는다(2026-09-14 코드 검토).
                if (isEditor) EndEditor(costImage);
            }

            // outlineEnabled 등과 같은 방식으로, "➤ Apply UI"를 누르면 두 값이 바로
            // button.interactable/enabled에 반영돼 프리팹에 저장된다.
            if (button != null)
            {
                bool interactable = buttonEnabled && pressable;

                button.interactable = interactable;
                button.enabled = interactable;

                // this도 같이 — pressable을 코드에서 isEditor:true로 바꿨을 때 저장되게 한다.
                if (isEditor) EndEditor(button, this);
            }
        }

        // 비용 판 안쪽 그림을 찾는다. 에디터에서는 _elementMap을 안 쓴다 — Collect는 한 번 돌고
        // 잠기는데(GenericUI._isCollected) OutlineUI에 [ExecuteAlways]가 붙어 있어 프리팹을 여는
        // 순간 이미 돌아 있다. 그 뒤 Cost 판을 새로 넣거나 id를 고치면 목록이 옛것이라 조용히
        // 못 찾는다 — OutlineUI.ResolveChild가 같은 이유로 같은 방식을 쓴다(2026-09-14 코드 검토).
        private ImageUI ResolveCostImage(bool isEditor)
        {
            if (isEditor)
            {
                var costNode = RectTransform.Find("Cost");

                if (costNode == null) return null;

                // 판의 이름은 부품마다 다르다(Image_Text는 "ImageUnit", Image_Mask는 "Image") —
                // 이름 대신 "Cost 바로 아래 첫 그림"으로 찾는다.
                foreach (Transform child in costNode)
                {
                    if (child.TryGetComponent<ImageUI>(out var found)) return found;
                }

                return null;
            }

            return TryGet<GenericUI>(UIId.Of("Cost"), out var costBoard) &&
                   costBoard.TryGet<ImageUI>(UIId.Of("Image"), out var costImage)
                ? costImage
                : null;
        }

        // public abstract bool IsActive { set; }
        //
        // public abstract bool IsEnable { set; }
        //
        // public virtual bool IsDot { set { } }

        // 런타임에서 버튼 기능을 켜고 끈다 — OutlineUI.SetOutlineEnabled와 같은 방식(2026-09-07,
        // 사용자 요청 — "기본은 켜져있게 두고 버튼이 필요없는 씬에서 직접 끈다"). 필드만 바꾸는
        // 것과 달리 button.interactable/enabled에 바로 반영된다.
        public void SetButtonEnabled(bool enabled, bool isEditor = false)
        {
            buttonEnabled = enabled;

            UpdateGenericUI(isEditor);
        }

        // "지금 누를 수 있는가"를 정하고 그림·비용 판·interactable에 반영한다 — SetButtonEnabled·
        // OutlineUI.SetOutlineEnabled와 같은 꼴이다. 무엇을 어떻게 바꾸는지는 UpdateGenericUI에
        // 모여 있다 — 예전엔 여기서 따로 처리했는데, 그러면 "➤ Apply UI"로 눌렀을 때와 실행 중에
        // 하는 일이 갈려서 에디터에서는 상태를 확인할 수가 없었다(2026-09-14).
        public void SetPressable(bool able, bool isEditor = false)
        {
            pressable = able;

            UpdateGenericUI(isEditor);
        }

        public void SetListener(UnityAction action)
        {
            // buttonEnabled(구조적 on/off)는 여기서 안 건드린다 — 리스너를 다는 것과 버튼을
            // 구조적으로 켜는 것은 별개다. 여기서 켜면 프리팹에서 표시 전용으로 꺼둔 버튼이
            // 다시 눌리게 되고, 필드만 바뀌어 OnPointerDown 가드·실제 button.interactable과
            // 어긋난다(2026-09-11 코드 리뷰).
            if (button != null) IsEnable = true;

            onClick.RemoveAllListeners();
            onClick.AddListener(action);
        }
        public void AddListener(UnityAction action)
        {
            onClick.AddListener(action);
        }

        public void SetPointerDown(UnityAction action)
        {
            onPointerDown.RemoveAllListeners();
            onPointerDown.AddListener(action);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!buttonEnabled) return;

            // SetPressable이 "지금 누를 수 있는가"를 button.interactable에 써 둔다 — 그걸 안 보면
            // 재료 부족처럼 꺼둔 버튼도 눌림 연출과 onPointerDown이 그대로 나간다(2026-09-11 코드 리뷰).
            if (button != null && !button.interactable) return;

            onPointerDown?.Invoke();
        }

        public void OnClick()
        {
            onClick?.Invoke();
        }

        // ── 인스펙터 클릭 연결용 자리 ─────────────────────────────────────────────
        //
        // 이 넷은 무엇을 할지 게임마다 다르므로 몸통을 비워 두고 프로젝트가 꽂는다. 다만
        // **이름은 반드시 ButtonUI 안에 그대로 있어야 한다** — 인스펙터의 클릭 연결은 메서드
        // 이름을 글자로 저장해 두고 그 대상 컴포넌트 타입에서 찾는다. 확장 메서드나 다른
        // 클래스로 옮기면 유니티가 못 찾고, **오류도 없이 그냥 아무 일도 안 한다.**
        //
        // 2026-09-18에 실제로 확장 메서드로 옮겼다가 되돌렸다. 프리팹 13개·연결 23곳(팝업 닫기
        // 16 · 화면 닫기 2 · 로비로 3 · 화면 크기 2)이 조용히 죽을 뻔했다. "연결된 곳 0"으로
        // 봤던 건 검색이 틀려서였다 — 프리팹 인스턴스 오버라이드는 이름이 `m_MethodName:` 뒤가
        // 아니라 다음 줄 `value:` 에 저장돼서 안 걸렸다.

        public static Action ClosePopup;
        public static Action CloseScene;
        public static Action GoLobby;
        public static Action SwitchViewSize;

        public void Button_ClosePopup() => ClosePopup?.Invoke();

        public void Button_CloseScene() => CloseScene?.Invoke();

        public void Button_GoLobby() => GoLobby?.Invoke();

        public void Button_SetViewSize() => SwitchViewSize?.Invoke();
    }
}
