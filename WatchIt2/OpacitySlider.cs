using ColossalFramework.UI;
using ModsCommon.UI;
using ModsCommon.Utilities;
using System;
using UnityEngine;

namespace WatchIt2
{
    public class VerticalLayoutToggle : ToggleSettingsItem
    {
        private bool InProcess { get; set; }

        public bool Value
        {
            get => Control.Value;
            set => SetValue(value);
        }

        public event Action<bool> OnValueChanged;

        protected override void InitControl()
        {
            base.InitControl();
            Control.OnValueChanged += OnControlValueChanged;
        }
        public override void Update()
        {
            base.Update();

            if (Control != null && !InProcess && Control.Value != Settings.InformationPanelVerticalLayout)
                SetValue(Settings.InformationPanelVerticalLayout);
        }
        private void OnControlValueChanged(bool value)
        {
            if (InProcess)
                return;

            Settings.InformationPanelVerticalLayout.value = value;
            OnValueChanged?.Invoke(value);
        }
        private void SetValue(bool newValue)
        {
            if (InProcess)
                return;

            try
            {
                InProcess = true;
                Control.Value = newValue;
            }
            finally
            {
                InProcess = false;
            }
        }
    }

    public class OpacitySlider : ControlSettingsItem<CustomUISlider>
    {
        private CustomUILabel ValueLabel { get; set; }
        private bool InProcess { get; set; }

        private int value;
        public int Value
        {
            get => value;
            set => SetValue(value, false);
        }

        public event Action<int> OnValueChanged;

        protected override RectOffset ItemsPadding => new RectOffset(20, 20, 10, 10);

        protected override void InitControl()
        {
            Control.name = "Opacity slider";
            Control.size = new Vector2(260f, 18f);
            Control.Orientation = UIOrientation.Horizontal;
            Control.MinValue = 0f;
            Control.MaxValue = 100f;
            Control.StepSize = 1f;
            Control.ScrollWheelAmount = 1f;

            Control.BgAtlas = CommonTextures.Atlas;
            Control.BgSprite = CommonTextures.PanelSmall;
            Control.BgColor = ComponentStyle.SettingsColor30;
            Control.ThumbAtlas = CommonTextures.Atlas;
            Control.ThumbSprites = CommonTextures.Circle;
            Control.ThumbColors = new ColorSet(
                ComponentStyle.SettingsColor85,
                ComponentStyle.SettingsColor95,
                ComponentStyle.NormalBlue,
                ComponentStyle.SettingsColor85,
                ComponentStyle.SettingsColor30);
            Control.ThumbSize = new Vector2(18f, 18f);
            Control.OnSliderValueChanged += OnSliderValueChanged;

            ValueLabel = Content.AddUIComponent<CustomUILabel>();
            ValueLabel.name = "Opacity value";
            ValueLabel.AutoSize = AutoSize.None;
            ValueLabel.size = new Vector2(46f, 18f);
            ValueLabel.textScale = 0.8f;
            ValueLabel.HorizontalAlignment = UIHorizontalAlignment.Right;
            ValueLabel.VerticalAlignment = UIVerticalAlignment.Middle;
            ValueLabel.Padding = new RectOffset(0, 0, 2, 0);
        }
        protected override void RefreshItems()
        {
            if (ValueLabel == null)
            {
                base.RefreshItems();
                return;
            }

            LabelItem.width = Content.width - Control.width - ValueLabel.width - Content.AutoLayoutSpace * 2f;
        }
        public override void Update()
        {
            base.Update();

            if (Control != null && !InProcess && value != Settings.InformationPanelDragBackgroundOpacity.value)
                Value = Settings.InformationPanelDragBackgroundOpacity.value;
        }
        private void OnSliderValueChanged(float sliderValue)
        {
            SetValue(Mathf.RoundToInt(sliderValue), true);
        }
        private void SetValue(int newValue, bool callEvent)
        {
            if (InProcess)
                return;

            try
            {
                InProcess = true;

                value = Mathf.Clamp(newValue, 0, 100);
                Control.Value = value;
                ValueLabel.text = $"{value}%";
                tooltip = $"Draggable background transparency: {value}%";

                if (callEvent)
                    OnValueChanged?.Invoke(value);
            }
            finally
            {
                InProcess = false;
            }
        }
    }
}
