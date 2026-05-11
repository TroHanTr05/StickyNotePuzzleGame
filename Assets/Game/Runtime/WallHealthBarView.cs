using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Runtime
{
    public class WallHealthBarView : ObserverMonoBehaviour
    {
        [Header("Target")]
        public WallHealthViewModel Target;

        [Header("UI References")]
        public GameObject Root;
        public Slider Slider;
        public Image FillImage;
        public TMP_Text HealthText;

        [Header("Display")]
        public bool HideWhenFull = false;
        public bool HideWhenDead = false;

        protected override void Start()
        {
            if (Target == null)
                Target = GetComponentInParent<WallHealthViewModel>();

            base.Start();

            if (Target != null)
                HandleHealthChanged(Target.CurrentHealth, Target.Max);
        }

        protected override void Subscribe()
        {
            if (Target == null)
                Target = GetComponentInParent<WallHealthViewModel>();

            if (Target == null) return;

            Target.OnHealthChanged += HandleHealthChanged;
        }

        protected override void Unsubscribe()
        {
            if (Target == null) return;

            Target.OnHealthChanged -= HandleHealthChanged;
        }

        private void HandleHealthChanged(int current, int max)
        {
            float normalized = max <= 0 ? 0f : Mathf.Clamp01((float)current / max);

            if (Slider != null)
            {
                Slider.minValue = 0;
                Slider.maxValue = max;
                Slider.wholeNumbers = true;
                Slider.value = current;
            }

            if (FillImage != null)
                FillImage.fillAmount = normalized;

            if (HealthText != null)
                HealthText.text = $"{current} / {max}";

            if (Root != null)
            {
                bool shouldShow = true;

                if (HideWhenFull && current >= max)
                    shouldShow = false;

                if (HideWhenDead && current <= 0)
                    shouldShow = false;

                Root.SetActive(shouldShow);
            }
        }
    }
}