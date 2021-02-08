using System;
using UI;
using UnityEngine;
using UnityEngine.UI;

public class UICanvas : MonoBehaviour {
	[SerializeField] private Button _menuButton;
	[SerializeField] private SettingsRegulator _speedRegulator;

	public event Action OnMenuClick; 

	private void Awake() {
		_menuButton.onClick.AddListener(HandleMenuClick);
	}

	private void HandleMenuClick() {
		OnMenuClick?.Invoke();
	}

	public void SetSettingSpeed(SettingsItemRegulation gameSpeed) {
		_speedRegulator.SetData(gameSpeed);
	}
}
