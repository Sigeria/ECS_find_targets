using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

public class UnitsSpawner {
	private float minDistanceReached = 1f;
	private int rotationSpeed = 20;
	private Mesh _capsuleMesh;
	private Mesh _cubeMesh;
	private Material _unitMaterial;
	private Material _targetMaterial;

	private EntityManager _entityManager;
	private EntityArchetype _unitArchetype;
	private EntityArchetype _targetArchetype;

	private Random _spawnRandom;
	private GameManager.GameSettings _settings;
	private bool _initialized;

	public UnitsSpawner(EntityManager entityManager) {
		_entityManager = entityManager;
	}

	private void Initialize() {
		GameObject unitPrefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
		_capsuleMesh = unitPrefab.GetComponent<MeshFilter>().sharedMesh;
		Object.Destroy(unitPrefab);
		
		GameObject targetPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
		_cubeMesh = targetPrefab.GetComponent<MeshFilter>().sharedMesh;
		Object.Destroy(targetPrefab);
		
		_unitMaterial = Resources.Load<Material>("unit");
		_targetMaterial = Resources.Load<Material>("target");

		_initialized = true;
	}

	public void Start(GameManager.GameSettings settings) {
		_settings = settings;

		if (!_initialized) {
			Initialize();
		}
		
		_spawnRandom = new Random();

		_unitArchetype = _entityManager.CreateArchetype(
			typeof(Translation),
			typeof(Rotation),
			typeof(LocalToWorld),
			typeof(RenderMesh),
			typeof(RenderBounds),
			typeof(UnitComponent)
		);
		
		_targetArchetype = _entityManager.CreateArchetype(
			typeof(Translation),
			typeof(LocalToWorld),
			typeof(RenderMesh),
			typeof(RenderBounds),
			typeof(TargetComponent)
		);

		SpawnUnits();
	}

	private void SpawnUnits() {
		for (int index = 0; index < _settings.UnitsCount; index++) {
			var randomPositionIndex = _spawnRandom.Next(_settings.TotalCells);
			var positionIndex = -1;

			positionIndex = randomPositionIndex;
			
			var positionToSpawn = UnitsMoveSystem.GetPositionByIndex(positionIndex, _settings.Size);
			SpawnUnit(positionToSpawn, index);
		}

		void SpawnUnit(float3 positionToSpawn, int index) {
			var position = positionToSpawn;
			Entity e = _entityManager.CreateEntity(_unitArchetype);
			_entityManager.AddComponentData(e, new Translation {
				Value = position
			});
			_entityManager.AddComponentData(e, new Rotation {
				Value = Quaternion.identity
			});

			var positionIndex = UnitsMoveSystem.GetIndexByPosition(position, _settings.Size);
			
			SpawnTarget(index, out var te,out var tc, out var trans);

			_entityManager.AddComponentData(e, new UnitComponent {
				minDistanceReached = minDistanceReached,
				rotationSpeed = rotationSpeed,
				currentPositionIndex = positionIndex,
				index = index,
				destination = trans.Value
			});
			_entityManager.AddSharedComponentData(e, new RenderMesh {
				mesh = _capsuleMesh,
				material = _unitMaterial,
				castShadows = ShadowCastingMode.Off,
				});
		}
	}

	private void SpawnTarget(int index, out Entity entity, out TargetComponent tc, out Translation trans) {
		var randomPositionIndex = _spawnRandom.Next(_settings.TotalCells);
		var positionIndex = randomPositionIndex;
		var positionToSpawn = UnitsMoveSystem.GetPositionByIndex(positionIndex, _settings.Size);
		SpawnTarget(index, positionToSpawn,out entity, out tc, out trans);
	}
	
	private void SpawnTarget(int index, float3 positionToSpawn,out Entity entity, out TargetComponent tc, out Translation trans) {
		var position = positionToSpawn;
		entity = _entityManager.CreateEntity(_targetArchetype);

		trans = new Translation {
			Value = position
		};
		_entityManager.AddComponentData(entity, trans);

		var positionIndex = UnitsMoveSystem.GetIndexByPosition(position, _settings.Size);

		tc = new TargetComponent() {
			currentPositionIndex = positionIndex,
			index = index,
			destination = position,
			created = true
		};
			
		_entityManager.AddComponentData(entity, 
			tc
		);
		_entityManager.AddSharedComponentData(entity, new RenderMesh {
			mesh = _cubeMesh,
			material = _targetMaterial,
			castShadows = ShadowCastingMode.Off,
		});
	}
}