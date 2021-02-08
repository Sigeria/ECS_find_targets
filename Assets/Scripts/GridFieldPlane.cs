using UnityEngine;

[RequireComponent(typeof(DrawGizmoGrid))]
public class GridFieldPlane : MonoBehaviour {
    [SerializeField] private int _size;
    [SerializeField] private Transform _plane;
    
    private DrawGizmoGrid _grid;
    private int _cachedSize;

    private void Awake() {
        _grid = GetComponent<DrawGizmoGrid>();
    }

    private void UpdateGrid() {
        var grid = _grid;
        if (Application.isEditor && grid is null) {
            grid = GetComponent<DrawGizmoGrid>();
        }
        _cachedSize = _size;
        grid.minX = grid.minY = 0;
        grid.maxX = grid.maxY = _size;
        _plane.localScale = new Vector3((float)_size / 10, 1, (float)_size / 10);
        _plane.localPosition = new Vector3((float)_size / 2, 0, (float)_size / 2);
    }

    private void OnDrawGizmos() {
        if (_cachedSize != _size) {
            UpdateGrid();
        }
    }

    public void SetSize(int value) {
        _size = value;
        if (_cachedSize != _size) {
            UpdateGrid();
        }
    }
}
