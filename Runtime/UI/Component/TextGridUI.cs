using System.Collections.Generic;
using UnityEngine;

namespace UnityTools.UI
{
    public class TextGridUI : GenericUI
    {
        private readonly List<TextUI> _textList = new();

        protected override bool Collect()
        {
            bool result = base.Collect();

            if (result)
            {
                for (int i = 0; i < RectTransform.childCount; i++)
                {
                    _textList.Add(RectTransform.GetChild(i).GetComponent<TextUI>());
                }
            }

            return result;
        }

        public TextUI GetText(int index)
        {
            Collect();

            if (index >= _textList.Count) return null;

            return _textList[index];
        }
    }
}
