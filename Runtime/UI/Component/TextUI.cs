using TMPro;
using UnityEngine;

namespace UnityTools.UI
{
    public class TextUI : ElementUI
    {
        [Space(10)]
        [SerializeField] private TextMeshProUGUI text;

        public string Text
        {
            get => text.text;
            set => text.text = value;
        }      
        public Color Color
        {
            get => text.color;
            set => text.color = value;
        }
        public float FontSize
        {
            get => text.fontSize;
            set => text.fontSize = value;
        }
        public float FontSizeMin
        {
            get => text.fontSizeMin;
            set => text.fontSizeMin = value;
        }
        public float FontSizeMax
        {
            get => text.fontSizeMax;
            set => text.fontSizeMax = value;
        }

        /// <summary>살 수 있으면 흰색, 모자라면 빨간색. 판단은 부르는 쪽에서 한다(<see cref="BattleTextTool"/>).</summary>
        public void SetPayAbleColor(bool isAble)
        {
            Color = isAble ? Color.white : Color.red;
        }
    }
}
