using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI {
    public class SettingsRegulator : MonoBehaviour {
        [SerializeField] private Slider _slider;
        [SerializeField] private TextMeshProUGUI _valueTF;
        [SerializeField] private TextMeshProUGUI _nameTF;
        private SettingsItemRegulation _data;

        private void Awake() {
        }

        private void HandleValueChange(float value) {
            _data.Current = (int)value;
        }

        public void SetData(SettingsItemRegulation data) {
            _data = data;
            UpdateView();
            _data.OnValueChanged += HandleDataValueChanged;
            _slider.onValueChanged.AddListener(HandleValueChange);
        }

        public void Clear() {
            if (_data is null) {
                return;
            }
            _data.OnValueChanged -= HandleDataValueChanged;
            _slider.onValueChanged.RemoveListener(HandleValueChange);
            _data = null;

        }

        private void HandleDataValueChanged() {
            UpdateView();
        }

        private void UpdateView() {
            _nameTF.text = _data.Name;
            _valueTF.text = _data.Current.ToString();
            _slider.maxValue = _data.Max;
            _slider.minValue = _data.Min;
            _slider.value = _data.Current;
        }
    }
}
