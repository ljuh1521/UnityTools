using System;
using System.Collections;
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
    public class ButtonUI : OutlineShadowUI, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
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

        // 누르고 있는 동안 크기를 바꾸는 연출(2026-10-07, DefenceR 사용자 요청 — 모든 버튼에 기본으로).
        // 화면 전체를 덮는 딤처럼 찌그러지면 안 되는 판은 이 칸을 끈다.
        [Header("Press Effect")]
        public bool pressEffectEnabled = true;

        // 원래 크기에 곱하는 배율. 1이 아니라 **누른 순간의 크기**에 곱한다 — ItemUI처럼 버튼
        // 크기를 코드가 정하는 곳이 있어서, 1로 되돌리면 그 값을 지운다.
        public Vector2 pressedScale = new(0.8f, 1.2f);

        [Min(0f)] public float pressEffectTime = 0.08f;

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
            //
            // interactable 하나만 보면 안 된다 — IsEnable=false(유니티 Button 컴포넌트를 끔)나 부모
            // CanvasGroup 비활성으로 막은 버튼은 그 값이 그대로 true 라서, 눌러도 아무 일도 안 일어나는데
            // 찌그러지기만 했다(2026-10-07 코드 검토 — DefenceR 최대 레벨 레벨업 버튼 등). 유니티가
            // 실제로 클릭을 받는지와 같은 기준으로 본다.
            if (button != null && !(button.isActiveAndEnabled && button.IsInteractable())) return;

            BeginPress(eventData);

            onPointerDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData) => _held = false;

        // 손가락이 버튼 밖으로 나가면 뗀 것으로 친다 — 다시 들어와도 다시 누르지 않는다.
        public void OnPointerExit(PointerEventData eventData) => _held = false;

        // 연출 도중 꺼지면 찌그러진 채 남는다 — 그 자리에서 원래 크기로 돌려놓는다.
        // 컴포넌트만 끄면(enabled=false) 루틴이 저절로 안 멈추므로 여기서 멈춘다.
        protected override void OnDisable()
        {
            base.OnDisable();

            RestoreNow();
        }

        // ── 눌림 연출 ──────────────────────────────────────────────────────────────

        private Coroutine _press;
        private Transform _pressed;          // 지금 크기를 바꾸고 있는 대상. null이면 손대지 않은 상태
        private Vector3 _baseScale;
        private Vector3 _lastWritten;

        // 기준점이 가운데가 아닐 때만 쓴다 — 가운데면 위치를 아예 건드리지 않는다(줄 맞춤 부품이
        // 정한 자리와 싸우지 않게). 누르는 동안 다른 코드가 위치를 바꾸면 그걸 새 기준으로 삼는 건
        // 크기와 같다.
        //
        // 자리는 localPosition 이 아니라 **anchoredPosition** 으로 잰다. localPosition 은 부모 크기가
        // 바뀌면 같이 바뀌어서, 누른 사이 부모가 커지거나 줄면 그걸 "다른 코드가 넣은 값"으로 받아들여
        // 보정분이 섞인 채 기준이 됐다 — 떼고 나서도 밀린 채 남았다(2026-10-07 코드 검토).
        private bool _movesPosition;
        private Vector2 _basePosition;
        private Vector2 _lastWrittenPosition;

        // 버튼이 줄면 누르는 판정도 같이 준다. 그러면 가장자리를 누른 손가락이 판정 밖으로 빠져
        // 누르는 중에 연출이 풀리고, 빨리 떼면 클릭을 놓친다(뗀 자리에 버튼이 없어서) — DefenceR 이
        // 쓰는 StandaloneInputModule 코드로 확인했다(2026-10-07). 주 그림의 판정만 줄어든 만큼 넓혀
        // 원래 자리를 덮게 한다.
        private UnityEngine.UI.Graphic _hitGraphic;
        private Vector4 _basePadding;
        private Vector4 _lastWrittenPadding;
        private float _t;                    // 0 = 원래 크기, 1 = 눌린 크기
        private bool _held;
        private PointerEventData _pressData;
        private int _tickFrame;              // 루틴이 마지막으로 돈 프레임 — 살아 있는지 가늠한다

        private void BeginPress(PointerEventData data)
        {
            if (!pressEffectEnabled || !Application.isPlaying) return;

            // 크기를 바꾸는 건 늘 버튼 자신이다. 바깥(카드 등)을 대신 누르게 하는 칸이 있었는데 쓰는
            // 곳이 0이라 뺐다(2026-10-07) — 필요해지면 그때 근거와 함께 다시 넣는다.
            var target = transform;

            // 되돌아가는 도중에 다시 누르면 기준을 다시 잡지 않는다 — 그때의 크기는 찌그러진
            // 중간값이라, 그걸 기준으로 삼으면 누를 때마다 조금씩 쪼그라든다.
            if (_pressed == null)
            {
                _pressed = target;
                _baseScale = target.localScale;
                _lastWritten = _baseScale;

                _movesPosition = target is RectTransform rect && rect.rect.center.sqrMagnitude > 1e-8f;
                _basePosition = _movesPosition ? ((RectTransform)target).anchoredPosition : Vector2.zero;
                _lastWrittenPosition = _basePosition;

                _hitGraphic = button != null ? button.targetGraphic : null;

                // 대상 바깥의 그림이면 같이 줄지 않으니 넓힐 이유가 없다.
                if (_hitGraphic != null && !_hitGraphic.transform.IsChildOf(target)) _hitGraphic = null;

                if (_hitGraphic != null)
                {
                    _basePadding = _hitGraphic.raycastPadding;
                    _lastWrittenPadding = _basePadding;
                }
                _t = 0f;
            }

            _held = true;
            _pressData = data;

            // 돌고 있는 루틴이 없으면 새로 띄운다. **손잡이만 보면 안 된다** — 누가 StopAllCoroutines 로
            // 멈춘 루틴도 손잡이는 남아 있어서, 예전 코드에선 그 버튼이 찌그러진 채 남고 꺼졌다 켜질
            // 때까지 다시는 연출이 안 됐다(2026-10-07 코드 검토, SDK Host 에서 재현). 지난 프레임에
            // 돌았는지로 본다. (첫 걸음에서 바로 끝나는 루틴은 유니티가 손잡이를 null 로 돌려줘서 이
            // 문제가 없다 — 같은 날 실측.)
            if (_press == null || Time.frameCount - _tickFrame > 1)
                _press = StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            while (true)
            {
                _tickFrame = Time.frameCount;

                // 누른 채 끌기 시작하면(목록 스크롤) 뗀 것으로 친다. 끌기 받는 인터페이스를 버튼에
                // 붙이면 부모 ScrollRect가 받을 끌기를 버튼이 가로채 스크롤이 죽는다 — 그래서 붙이지
                // 않고, 누른 동안 이쪽에서 "지금 끌고 있나"만 본다.
                if (_held && _pressData != null && _pressData.dragging) _held = false;

                // 게임 시간을 멈춘 팝업 위에서도 눌려야 하므로 멈추지 않는 시간으로 센다.
                float step = pressEffectTime > 0f ? Time.unscaledDeltaTime / pressEffectTime : 1f;

                _t = Mathf.MoveTowards(_t, _held ? 1f : 0f, step);

                Apply();

                if (!_held && _t <= 0f)
                {
                    Clear();
                    yield break;
                }

                yield return null;
            }
        }

        private void Apply()
        {
            if (_pressed == null) return;

            // 누르는 동안 다른 코드가 크기를 새로 넣었으면(ItemUI가 칸 크기를 다시 맞출 때) 그걸
            // 새 기준으로 삼는다. 안 그러면 뗄 때 옛 크기로 되돌려 그 값을 지운다.
            if (_pressed.localScale != _lastWritten) _baseScale = _pressed.localScale;

            var factor = Vector2.LerpUnclamped(Vector2.one, pressedScale, Mathf.SmoothStep(0f, 1f, _t));
            var scale = new Vector3(_baseScale.x * factor.x, _baseScale.y * factor.y, _baseScale.z);

            _pressed.localScale = scale;
            _lastWritten = scale;

            if (_movesPosition) KeepCenter(scale);

            if (_hitGraphic != null) KeepHitArea(scale);
        }

        // 판정을 원래 자리로 되돌린다. 대상 안에서 본 원래 판정 사각형을, 대상이 줄어든 비율의
        // 거꾸로(원래 크기 ÷ 지금 크기)만큼 가운데 기준으로 키우면 줄어든 뒤에도 원래 자리를 덮는다
        // — 가운데를 지키려 옮긴 위치까지 이 식에 들어 있다. 그걸 그림 자신의 좌표로 돌려 판정
        // 여백(raycastPadding, 음수면 넓힘)으로 넣는다.
        private void KeepHitArea(Vector3 scale)
        {
            // 다른 코드가 넣은 값이라도 **계산 불가 값(NaN·무한)은 받지 않는다.** NaN 은 무엇과도
            // "다르다"가 나와 기준으로 받아들여지고, 끝날 때 그대로 되돌려 넣어져 그 버튼은 영영 판정이
            // 깨진다(2026-10-07 코드 검토). 같은 이유로 아래에서 쓸 값도 걸러 낸다.
            if (_hitGraphic.raycastPadding != _lastWrittenPadding && IsFinite(_hitGraphic.raycastPadding))
                _basePadding = _hitGraphic.raycastPadding;

            // 다 돌아왔으면 계산하지 않고 원래 값을 그대로 넣는다 — 좌표를 오가며 생기는 아주 작은
            // 오차가 여백에 남지 않게.
            if (_t <= 0f)
            {
                _hitGraphic.raycastPadding = _basePadding;
                _lastWrittenPadding = _basePadding;
                return;
            }

            if (Mathf.Approximately(scale.x, 0f) || Mathf.Approximately(scale.y, 0f)) return;

            var g = _hitGraphic.rectTransform;
            var rect = g.rect;
            var center = _pressed is RectTransform target ? (Vector3)target.rect.center : Vector3.zero;
            var grow = new Vector3(_baseScale.x / scale.x, _baseScale.y / scale.y, 1f);

            // 원래 판정 = 그림 사각형에서 원래 여백만큼 안으로 들인 것
            var from = new Vector2(rect.xMin + _basePadding.x, rect.yMin + _basePadding.y);
            var to = new Vector2(rect.xMax - _basePadding.z, rect.yMax - _basePadding.w);

            Vector3 Map(Vector2 local)
            {
                var inTarget = _pressed.InverseTransformPoint(g.TransformPoint(local));
                var grown = center + Vector3.Scale(grow, inTarget - center);
                return g.InverseTransformPoint(_pressed.TransformPoint(grown));
            }

            var a = Map(from);
            var b = Map(to);
            var lo = Vector2.Min(a, b);
            var hi = Vector2.Max(a, b);

            var padding = new Vector4(lo.x - rect.xMin, lo.y - rect.yMin, rect.xMax - hi.x, rect.yMax - hi.y);

            // 부모가 크기 0 이 되는 순간처럼 좌표를 되돌릴 수 없으면 이번 프레임은 건너뛴다.
            if (!IsFinite(padding)) return;

            _hitGraphic.raycastPadding = padding;
            _lastWrittenPadding = padding;
        }

        private static bool IsFinite(Vector4 v) =>
            float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z) && float.IsFinite(v.w);

        // 크기는 기준점 둘레로 바뀌므로, 기준점이 가운데가 아니면 버튼이 한쪽으로 쏠린다(닫기 버튼처럼
        // 모서리에 붙여 두려고 기준점을 옮긴 버튼이 많다). **눈에 보이는 가운데**가 부모 기준으로 같은
        // 자리에 있도록 위치를 그만큼 옮긴다.
        //
        // 가운데의 부모 기준 자리 = 위치 + 회전 × (크기 ⊙ 자기 안에서의 가운데). 크기가 바뀌어도 이게
        // 같으려면 위치 = 원래 위치 + 회전 × ((원래 크기 − 지금 크기) ⊙ 가운데).
        // **회전을 빼면 안 된다** — 왼쪽 화살표를 180도 돌려 오른쪽으로 쓰는 버튼이 실제로 있고,
        // 회전을 무시하면 반대쪽으로 밀린다.
        private void KeepCenter(Vector3 scale)
        {
            var rect = (RectTransform)_pressed;

            if (rect.anchoredPosition != _lastWrittenPosition) _basePosition = rect.anchoredPosition;

            // 부모 안에서 옮길 양. anchoredPosition 과 localPosition 은 x·y 가 같은 양만큼 움직이므로
            // 이 식의 결과를 그대로 더하면 된다.
            var center = (Vector3)rect.rect.center;
            var shrink = Vector3.Scale(_baseScale - scale, center);
            var position = _basePosition + (Vector2)(rect.localRotation * shrink);

            rect.anchoredPosition = position;
            _lastWrittenPosition = position;
        }

        private void RestoreNow()
        {
            if (_press != null) StopCoroutine(_press);

            // 다른 코드가 새로 넣은 값이면 그대로 둔다 — 그게 지금의 원래 크기·자리다.
            if (_pressed != null)
            {
                if (_pressed.localScale == _lastWritten) _pressed.localScale = _baseScale;

                if (_movesPosition && ((RectTransform)_pressed).anchoredPosition == _lastWrittenPosition)
                    ((RectTransform)_pressed).anchoredPosition = _basePosition;

                if (_hitGraphic != null && _hitGraphic.raycastPadding == _lastWrittenPadding)
                    _hitGraphic.raycastPadding = _basePadding;
            }

            Clear();
        }

        private void Clear()
        {
            _press = null;
            _pressed = null;
            _movesPosition = false;
            _hitGraphic = null;
            _held = false;
            _pressData = null;
            _t = 0f;
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
