using UnityEngine;

public class SettingsWriter {
	private int _size;
	private int _speed;
	private int _units;
        
	public void WriteSize(int value) {
		_size = value;
	}

	public void WriteSpeed(int value) {
		_speed = value;
	}

	public void WriteUnits(int value) {
		_units = Mathf.Clamp(value, 1, _size * _size / 2);
	}

	public GameManager.GameSettings GetNewSettings() {
		return new GameManager.GameSettings(_size, _units, _size * _size, _speed, _units < 20000);
	}
}