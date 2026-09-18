using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.UI
{
    public class ToggleUI : GenericUI
    {
        public Toggle toggle;
        public TextUI label;

        public bool IsOn
        {
            set => toggle.isOn = value;
            get => toggle.isOn;
        }
        
        public string LabelText
        {
            set => label.Text = value;
        } 
        
        public override void UpdateGenericUI(bool isEditor)
        {
            base.UpdateGenericUI(isEditor);
        }
        
    }
}
