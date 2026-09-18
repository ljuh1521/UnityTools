using UnityEngine;

namespace UnityTools.UI
{
    /// <summary>
    /// bool 값 하나로 이미지 두 장 중 하나만 보여주는 공용 파츠(2026-09-07). 잠김/열림,
    /// 성공/실패처럼 "상태 하나 = 그림 하나"인 화면에 재사용한다. Image_Switch.prefab이 이 파츠다.
    ///
    /// 코드에서는 <see cref="SetState"/>를 부르면 바로 반영된다. 인스펙터에서 <see cref="state"/>를
    /// 바꾼 경우엔 다른 GenericUI와 같은 방식으로 "➤ Apply UI" 버튼(UpdateEditor.cs)을 눌러야
    /// 반영된다 — 자동 적용(OnValidate)보다 이 방식이 낫다고 확정된 프로젝트 관례를 그대로 따랐다
    /// (ImageUI.cs 참고).
    /// </summary>
    public class ImageSwitchUI : GenericUI
    {
        [Header("State")]
        public bool state;

        [Header("Image")]
        public ImageUI trueImage;
        public ImageUI falseImage;

        // OutlineUI.SetOutlineEnabled·ShadowUI.SetShadowEnabled와 같은 방식.
        public void SetState(bool value, bool isEditor = false)
        {
            state = value;
            UpdateGenericUI(isEditor);
        }

        public override void UpdateGenericUI(bool isEditor)
        {
            base.UpdateGenericUI(isEditor);

            if (trueImage != null) trueImage.SetActive(state);
            if (falseImage != null) falseImage.SetActive(!state);
        }

        protected override void Awake()
        {
            base.Awake();

            UpdateGenericUI(false);
        }
    }
}
