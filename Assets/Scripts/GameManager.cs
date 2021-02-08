using Pool;
using UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = Unity.Mathematics.Random;

public class GameManager : SystemBase {
	public static readonly string SceneName = "light_and_events";
	private EntityManager _entityManager;
	private UnitsSpawner _spawnerManager;
	private GridFieldPlane _grid;
	public static NativeArray<Random> Randoms;
	private GameSettings _currentSettings;
	private UnitsMoveSystem _unitSystem;
	private ScrollCamera _camera;
	private NewTargetsSystem _newTargetsSystem;
	private SettingsWriter _settingsWriter;
	private UIManager _uiManager;
	private bool _isPlaying;
	private ParticleSystemPool _effectsPool;
	private int _speed = 1;
	private bool _gameCreated;
	private EntityArchetype _effectArchetype;
	private EndSimulationEntityCommandBufferSystem  _entityCommandBuffer;

	public class GameSettings {
		public readonly int Size;
		public readonly int TotalCells;
		public readonly int UnitsCount;
		public readonly int UnitsPerSecond;
		public readonly bool ResolveCollisions;

		public GameSettings(int size, int unitsCount, int totalCells, int unitsPerSecond, bool resolveCollisions) {
			Size = size;
			UnitsCount = unitsCount;
			TotalCells = totalCells;
			UnitsPerSecond = unitsPerSecond;
			ResolveCollisions = resolveCollisions;
		}
	}

	protected override void OnCreate() {
		_entityCommandBuffer = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
		_entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

		var activeScene = SceneManager.GetActiveScene();
		if (activeScene.isLoaded && activeScene.name == SceneName) {
			Initialize();
		} else {
			SceneManager.sceneLoaded += HandleSceneLoaded;
		}
	}

	private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) {
		if (scene.name == SceneName) {
			Initialize();
		}
	}

	private void Initialize() {
		CreateRandom();
		CreateEnvironment();
		OpenSettings(true);
		_effectArchetype = _entityManager.CreateArchetype(
			typeof(EffectEventComponent)
		);
	}

	private void Start() {
		_gameCreated = true;

		_grid.SetSize(_currentSettings.Size);
		_camera.SetSize(_currentSettings.Size);

		_spawnerManager = new UnitsSpawner(_entityManager);
		_spawnerManager.Start(_currentSettings);

		_unitSystem = World.GetOrCreateSystem<UnitsMoveSystem>();
		_unitSystem.SetSettings(_currentSettings);

		_newTargetsSystem = World.GetOrCreateSystem<NewTargetsSystem>();
		_newTargetsSystem.SetSettings(_currentSettings);
		_isPlaying = true;
	}

	private void OpenSettings(bool onInit = false) {
		_settingsWriter = new SettingsWriter();
		var regulators = new SettingsItemRegulation[3];
		var defaultSize = 100;

		var size = new SettingsItemRegulation("Size", 2, 1000, defaultSize, _settingsWriter.WriteSize);
		_settingsWriter.WriteSize(size.Current);
		regulators[0] = size;

		var units = new SettingsItemRegulation("Units", 1, defaultSize * defaultSize / 2, defaultSize * defaultSize / 2,
			_settingsWriter.WriteUnits);
		_settingsWriter.WriteUnits(units.Current);
		regulators[1] = units;

		var speed = new SettingsItemRegulation("Units speed", 1, 100, 5, _settingsWriter.WriteSpeed);
		_settingsWriter.WriteSpeed(speed.Current);
		regulators[2] = speed;

		size.OnValueChanged += HandleSizeChanged;

		void HandleSizeChanged() {
			units.ChangeMaxValue(size.Current * size.Current / 2);
		}

		_uiManager.ShowMenu(regulators, HandlePlayClick, HandleMenuClose, onInit);

		void HandlePlayClick() {
			size.OnValueChanged -= HandleSizeChanged;
			_currentSettings = _settingsWriter.GetNewSettings();

			Destroy();
			Start();
		}
	}

	private void HandleMenuClose() {
		Resume();
	}

	private void Resume() {
		_isPlaying = true;
	}

	private void Pause() {
		_isPlaying = false;
	}

	private void CreateEnvironment() {
		var gridPrefab = Resources.Load<GridFieldPlane>("grid");
		_grid = Object.Instantiate(gridPrefab);
		_grid.SetSize(100);

		var cameraPrefab = Resources.Load<ScrollCamera>("scroll_camera");
		_camera = Object.Instantiate(cameraPrefab);
		_camera.SetSize(100);
		_camera.UpdateCamera();

		_uiManager = UIManager.Create(_camera);
		_uiManager.OnMenuClick += () => OpenSettings();
		var gameSpeed = new SettingsItemRegulation("Speed", 1, 1000, 1, ChangeSpeed);
		_uiManager.SetSpeedSetting(gameSpeed);

		var effectPrefab = Resources.Load<ParticleSystem>("effect");
		_effectsPool = ParticleSystemPool.Create(effectPrefab, 10);
	}

	private void ChangeSpeed(int value) {
		_speed = value;
	}

	private void CreateRandom() {
		if (Randoms.IsCreated) {
			Randoms.Dispose();
		}

		Randoms = new NativeArray<Random>(JobsUtility.MaxJobThreadCount, Allocator.Persistent,
			NativeArrayOptions.UninitializedMemory);
		var r = (uint) UnityEngine.Random.Range(int.MinValue, int.MaxValue);
		for (int i = 0; i < JobsUtility.MaxJobThreadCount; i++)
			Randoms[i] = new Random(r == 0 ? r + 1 : r);
	}

	protected override void OnUpdate() {
		if (!_isPlaying) {
			return;
		}

		if (Input.GetKey(KeyCode.Escape)) {
			Pause();
			OpenSettings();
			return;
		}

		CreateRandom();

		var deltaTime = Time.DeltaTime * _speed;


		_camera.UpdateCamera();
		_unitSystem.Update(deltaTime);
		UpdateTargets();
		UpdateEffects();
		_newTargetsSystem.Update(deltaTime);
	}

	private void UpdateEffects() {
		Entities
			.WithStructuralChanges()
			.WithoutBurst()
			.ForEach((ref Entity e, ref EffectEventComponent ec) => {
				_effectsPool.PlayParticle(ec.position);
				_entityManager.DestroyEntity(e);
			}).Run();
	}

	private void UpdateTargets() {
		var buffer = _entityCommandBuffer.CreateCommandBuffer().AsParallelWriter();
		var effectArchetype = _effectArchetype;
		Entities
			.ForEach((ref TargetComponent targetComponent, ref Translation trans) => {
				if (!targetComponent.reached) {
					trans.Value = targetComponent.destination;
					targetComponent.reached = true;

					var entity = buffer.CreateEntity(0,effectArchetype);

					var eventComponent = new EffectEventComponent() {
						position = new float3(targetComponent.destination.x, 1, targetComponent.destination.z)
					};

					buffer.AddComponent(0,entity,
						eventComponent
					);
				}
			}).ScheduleParallel();
	}

	protected override void OnDestroy() {
		Randoms.Dispose();
	}

	private void Destroy() {
		if (!_gameCreated) {
			return;
		}

		_gameCreated = false;

		_unitSystem.World.DestroySystem(_unitSystem);
		_newTargetsSystem.World.DestroySystem(_newTargetsSystem);

		foreach (var e in _entityManager.GetAllEntities()) {
			_entityManager.DestroyEntity(e);
		}
	}
}