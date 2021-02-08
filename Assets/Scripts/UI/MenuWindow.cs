using System;
using System.Linq;
using UI;
using UnityEngine;
using UnityEngine.UI;

public class MenuWindow : MonoBehaviour {
	[SerializeField] private SettingsRegulator[] _regulators;
	[SerializeField] private Button _playButton;
	[SerializeField] private Button _closeButton;
	private bool _isOpen;
	private Action _onPlayClick;
	private Action _onCloseClick;

	private void Awake() {
		_playButton.onClick.AddListener(HandlePlayClick);
		_closeButton.onClick.AddListener(HandleCloseClick);
	}

	private void HandleCloseClick() {
		_onCloseClick?.Invoke();
		Close();
	}

	private void HandlePlayClick() {
		_onPlayClick?.Invoke();
		Close();
	}

	private void Close() {
		_isOpen = false;
		gameObject.SetActive(false);
		Clear();
	}

	private void Clear() {
		foreach (var regulator in _regulators) {
			regulator.Clear();
		}
	}

	public void Show(SettingsItemRegulation[] settings, Action onPlayClick, Action onClose, bool onInit) {
		_closeButton.gameObject.SetActive(!onInit);
		
		_onPlayClick = onPlayClick;
		_onCloseClick = onClose;
		
		gameObject.SetActive(true);
		for (int i = 0; i < _regulators.Length; i++) {
			var view = _regulators[i];
			var data = settings.ElementAtOrDefault(i);
			if (data is null) {
				view.gameObject.SetActive(false);
				continue;
			}
			
			view.SetData(data);
			view.gameObject.SetActive(true);
		}

		_isOpen = true;
	}
}

public class SettingsItemRegulation {
	public readonly string Name;
	public readonly int Min;
	public int Max;
	private int _current;
	private readonly Action<int> _write;
	private float _oldRatio;
	public event Action OnValueChanged;

	public SettingsItemRegulation(string name, int min, int max, int current, Action<int> write) {
		Name = name;
		Min = min;
		Max = max;
		_current = current;
		_write = write;
	}

	public int Current {
		get => _current;
		set {
			var newValue  = Mathf.Clamp(value, Min, Max);
			_current = newValue;
			_write?.Invoke(_current);
			OnValueChanged?.Invoke();
		}
	}

	public void ChangeMaxValue(int value) {
		_oldRatio = (float)(Current - Min)/ (Max - Min);
		Max = value;
		Current = Mathf.FloorToInt((Max - Min) * _oldRatio) + Min;
	}
}
