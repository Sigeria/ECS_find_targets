using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UI {
    public class UIManager {
        private UICanvas _uiCanvas;
        private MenuWindow _menuWindow;
        private readonly Camera _worldCamera;
        private RawImage _cameraImage;
        private bool _isWindowOpen;
        private ScrollCamera _scrollCamera;
        public event Action OnMenuClick; 

        private UIManager(ScrollCamera worldCamera) {
            _worldCamera = worldCamera.UnityCamera;
            _scrollCamera = worldCamera;
        }

        public void ShowMenu(SettingsItemRegulation[] settings, Action onPlayClick, Action onClose, bool onInit) {
            if (_isWindowOpen) {
                return;
            }
            
            _menuWindow.Show(settings, HandlePlayClick, HandleCloseClick, onInit);
            HandleWindowOpen();
            
            void HandlePlayClick() {
                HandleWindowClose();
                onPlayClick?.Invoke();
            }

            void HandleCloseClick() {
                HandleWindowClose();
                onClose?.Invoke();
            }

        }

        private void HandleWindowClose() {
            _scrollCamera.DecreaseLock();
            _isWindowOpen = false;
            SetCameraActive(true);
        }
        
        private void HandleWindowOpen() {
            _scrollCamera.Lock();
            _isWindowOpen = true;
            SetCameraActive(false);
        }

        private void SetCameraActive(bool value) {
            if (value) {
                _cameraImage.enabled = false;
                _worldCamera.cullingMask = 215;
            } else {
                var texture = GetCameraTexture();
                _cameraImage.enabled = true;
                _cameraImage.texture = texture;
                _worldCamera.cullingMask = 0;
            }
        }

        public static UIManager Create(ScrollCamera worldCamera) {
            var result = new UIManager(worldCamera);
            result.Initialize();

            return result;
        }

        private void Initialize() {
            var uiPrefab = Resources.Load<UICanvas>("ui_canvas");
            _uiCanvas = Object.Instantiate(uiPrefab);
            _uiCanvas.OnMenuClick += HandleMenuClick;

            var menuPrefab = Resources.Load<MenuWindow>("menu_window");
            _menuWindow = Object.Instantiate(menuPrefab, _uiCanvas.transform);
        
            var imagePrefab = Resources.Load<RawImage>("camera_image"); 
            _cameraImage = Object.Instantiate(imagePrefab, _uiCanvas.transform);
            _cameraImage.transform.SetAsFirstSibling();
            _cameraImage.enabled = false;
        }

        private void HandleMenuClick() {
            if (_isWindowOpen) {
                return;
            }
            OnMenuClick?.Invoke();
        }

        Texture2D GetCameraTexture()
        {
            var renderTexture = new RenderTexture(Screen.width, Screen.height, 10000, RenderTextureFormat.ARGB32);
            renderTexture.hideFlags = HideFlags.HideAndDontSave;
            renderTexture.Create();
            _worldCamera.targetTexture = renderTexture;
            
            var currentRT = RenderTexture.active;
            RenderTexture.active = _worldCamera.targetTexture;

            _worldCamera.Render();

            Texture2D texture = new Texture2D(_worldCamera.targetTexture.width, _worldCamera.targetTexture.height);
            texture.ReadPixels(new Rect(0, 0, _worldCamera.targetTexture.width, _worldCamera.targetTexture.height), 0, 0);
            texture.Apply();

            RenderTexture.active = currentRT;
            
            _worldCamera.targetTexture = null;
            Object.Destroy(renderTexture);
            
            return texture;
        }

        public void SetSpeedSetting(SettingsItemRegulation gameSpeed) {
            _uiCanvas.SetSettingSpeed(gameSpeed);
        }
    }
}
