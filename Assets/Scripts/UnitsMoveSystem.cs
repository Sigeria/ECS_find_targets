using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[DisableAutoCreation]
public class UnitsMoveSystem : SystemBase, ICustomUpdateSystem {
	public static NativeMultiHashMap<int, float3> EntityPositionsByCells;
	public static NativeMultiHashMap<int, int> EntitiesByIndexes;
	public static int totalCollisions;
	private GameManager.GameSettings _settings;
	private float _deltaTime;

	protected override void OnCreate() {
		totalCollisions = 0;
		EntityPositionsByCells = new NativeMultiHashMap<int, float3>(0, Allocator.Persistent);
		EntitiesByIndexes = new NativeMultiHashMap<int, int>(0, Allocator.Persistent);
	}

	public void SetSettings(GameManager.GameSettings settings) {
		_settings = settings;
	}

	public static int GetUniqueKeyForPosition(float3 position, int cellSize) {
		return (int) (19 * math.floor(position.x / cellSize) + (17 * math.floor(position.z / cellSize)));
	}

	public static int GetIndexByPosition(float3 position, int size) {
		var pX = position.x - 0.5f;
		var pZ = position.z - 0.5f;

		pX = Math.Min(size, pX);
		pX = Math.Max(0, pX);

		pZ = Math.Min(size, pZ);
		pZ = Math.Max(0, pZ);

		int x = (int) (pX);
		int z = (int) (pZ);
		return (int) (z * size + x);
	}

	public static float3 GetPositionByIndex(int positionIndex, int size) {
		var x = positionIndex % size + 0.5f;
		var z = positionIndex / size + 0.5f;
		return new float3(x, 0, z);
	}

	protected override void OnUpdate() {
		float deltaTime = _deltaTime;
		var size = _settings.Size;

		var resolveCollisions = _settings.ResolveCollisions;

		if (resolveCollisions) {
			var unitsCount = _settings.UnitsCount;

			EntityPositionsByCells.Clear();
			if (unitsCount > EntityPositionsByCells.Capacity) {
				EntityPositionsByCells.Capacity = unitsCount;
			}

			NativeMultiHashMap<int, float3>.ParallelWriter cellVsEntityPositionsParallel =
				EntityPositionsByCells.AsParallelWriter();
			Entities
				.ForEach((ref UnitComponent uc, ref Translation trans) => {
					cellVsEntityPositionsParallel.Add(GetUniqueKeyForPosition(trans.Value, 25), trans.Value);
				}).ScheduleParallel();

			//Resolve nearest collision
			NativeMultiHashMap<int, float3> cellVsEntityPositionsForJob = EntityPositionsByCells;
			Entities
				.WithReadOnly(cellVsEntityPositionsForJob)
				.ForEach((ref UnitComponent uc, ref Translation trans) => {
					int key = GetUniqueKeyForPosition(trans.Value, 10);
					float currentDistance = 1.5f;
					int total = 0;
					uc.avoidanceDirection = float3.zero;
					uc.avoiding = false;
					if (cellVsEntityPositionsForJob.TryGetFirstValue(key, out var currentLocationToCheck,
						out var nmhKeyIterator)) {
						do {
							if (!trans.Value.Equals(currentLocationToCheck)) {
								uc.avoiding = true;
								var distance = math.sqrt(math.lengthsq(trans.Value - currentLocationToCheck));
								if (currentDistance > distance) {
									currentDistance = distance;
									float3 distanceFromTo = trans.Value - currentLocationToCheck;
									uc.avoidanceDirection = math.normalize(distanceFromTo / currentDistance);
									total++;
								}

								//Debug
								if (distance < 0.05f) {
									uc.timeStamp = 60;
									uc.collided = true;
								}

								if (uc.collided) {
									uc.timeStamp = uc.timeStamp - deltaTime;
								}

								if (uc.timeStamp <= 0) {
									uc.collided = false;
								}
							}
						} while (cellVsEntityPositionsForJob.TryGetNextValue(out currentLocationToCheck,
							ref nmhKeyIterator));

						if (total > 0) {
							uc.avoidanceDirection = uc.avoidanceDirection / total;
						}
					}
				}).ScheduleParallel();
		}

		var speed = _settings.UnitsPerSecond;

		Entities
			.ForEach((ref UnitComponent uc, ref Translation trans, ref Rotation rot) => {
				if (!uc.reached) {
					uc.waypointDirection = math.normalize(uc.destination - trans.Value);
					uc.waypointDirection = uc.waypointDirection + uc.avoidanceDirection;
					trans.Value += uc.waypointDirection * speed * deltaTime;

					Clamp(ref trans.Value, size);

					//rot.Value = math.slerp(rot.Value, quaternion.LookRotation(uc.waypointDirection, math.up()), deltaTime * uc.rotationSpeed);
					if (math.distance(trans.Value, uc.destination) <= uc.minDistanceReached) {
						uc.reached = true;
					}
				}
			}).ScheduleParallel();

	}

	private static void Clamp(ref float3 trans, int size) {
		var x = Mathf.Clamp(trans.x, 0, size);
		var z = Mathf.Clamp(trans.z, 0, size);
		var max = new float3(x, 0, z);
		trans = max;
	}

	public static void NewClamp(ref float3 trans, int size) {
		var x = Mathf.RoundToInt(trans.x - 0.5f) + 0.5f;
		var z = Mathf.RoundToInt(trans.z - 0.5f) + 0.5f;
		var result = new float3(x, 0, z);
		Clamp(ref result, size);
		trans = result;
	}

	protected override void OnDestroy() {
		EntityPositionsByCells.Dispose();
		EntitiesByIndexes.Dispose();
	}
	public void Update(float deltaTime) {
		_deltaTime = deltaTime;
		Update();
	}
}