using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

[DisableAutoCreation]
public class NewTargetsSystem : JobComponentSystem, ICustomUpdateSystem {

	private static NativeHashMap<int, NewPosition> TargetArray;
	private GameManager.GameSettings _settings;
	private float _deltaTime;

	protected override void OnCreate() {
		TargetArray = new NativeHashMap<int, NewPosition>(0, Allocator.Persistent);
		base.OnCreate();
	}
	
	public void SetSettings(GameManager.GameSettings settings) {
		_settings = settings;
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps) {
		EntityQuery targetQuery = GetEntityQuery(typeof(TargetComponent));

		var count = targetQuery.CalculateEntityCount();

		TargetArray.Clear();
		if (count > TargetArray.Capacity) {
			TargetArray.Capacity = count;
		}

		var tForWrite = TargetArray.AsParallelWriter();

		NativeHashMap<int, NewPosition> tForJob = TargetArray;

		NewTargetJob addComponentJob = new NewTargetJob {
			targetArray = tForWrite,
			Size = _settings.Size,
			MaxRadius = _settings.UnitsPerSecond * 5,
			Randoms = GameManager.Randoms
		};
		inputDeps = addComponentJob.Schedule(this, inputDeps);

		ChangeTargetPosition addComponentJob2 = new ChangeTargetPosition {
			targetArray = tForJob,
			Size = _settings.Size
		};
		inputDeps = addComponentJob2.Schedule(this, inputDeps);

		ChangeUnitDestination addComponentJob3 = new ChangeUnitDestination {
			targetArray = tForJob,
			Size = _settings.Size
		};
		inputDeps = addComponentJob3.Schedule(this, inputDeps);

		return inputDeps;
	}

	private struct NewPosition {
		public bool needGenerate;
		public bool changed;
		public int positionIndex;
	}

	[BurstCompile]
	struct NewTargetJob : IJobForEachWithEntity<UnitComponent> {
		[NativeDisableContainerSafetyRestriction]
		public NativeArray<Random> Randoms;

		[NativeSetThreadIndex]
		private int _threadId;

		[ReadOnly] public int Size;
		[ReadOnly] public int MaxRadius;
		public NativeHashMap<int, NewPosition>.ParallelWriter targetArray;

		public void Execute(Entity entity, int index, ref UnitComponent uc) {
			if (uc.reached) {
				var rnd = Randoms[_threadId];
				var randomPosition = RandomPoint(ref rnd, uc.destination, Size, MaxRadius);
				Randoms[_threadId] = rnd;
				var randomIndex = UnitsMoveSystem.GetIndexByPosition(randomPosition, Size);

				targetArray.TryAdd(uc.index, new NewPosition {
					changed = true,
					positionIndex = randomIndex
				});
			}
		}
	}

	private static float3 RandomPoint(ref Random rnd, float3 original, int size, int maxRadius) {
		var radius = Math.Sqrt(rnd.NextDouble()) * maxRadius;
						
		var angle = rnd.NextDouble() * Math.PI * 2;
        
		var x = original.x + ( radius * Math.Cos( angle ) );
		var z = original.z + ( radius * Math.Sin( angle ) );
		var result = new float3((float)x, 0, (float)z);
		UnitsMoveSystem.NewClamp(ref result, size);
		return result;
	}

	[BurstCompile]
	struct ChangeTargetPosition : IJobForEachWithEntity<TargetComponent> {
		[ReadOnly] public int Size;
		[ReadOnly] public NativeHashMap<int, NewPosition> targetArray;
		public EntityCommandBuffer.ParallelWriter entityCommandBuffer;

		public void Execute(Entity entity, int index, ref TargetComponent uc) {
			if (!uc.reached) {
				return;
			}

			var hasValue = targetArray.TryGetValue(uc.index, out var t);
			if (hasValue && t.changed) {
				uc.reached = false;
				uc.currentPositionIndex = t.positionIndex;
				var position = UnitsMoveSystem.GetPositionByIndex(t.positionIndex, Size);
				uc.destination = position;
			}
		}
	}

	[BurstCompile]
	struct ChangeUnitDestination : IJobForEachWithEntity<UnitComponent> {
		[ReadOnly] public int Size;
		[ReadOnly] public NativeHashMap<int, NewPosition> targetArray;

		public void Execute(Entity entity, int index, ref UnitComponent uc) {
			if (uc.reached) {
				var hasValue = targetArray.TryGetValue(uc.index, out var t);

				if (hasValue) {
					if (t.changed) {
						if (t.positionIndex == uc.currentPositionIndex) {
							return;
						}

						uc.currentPositionIndex = t.positionIndex;
						uc.destination = UnitsMoveSystem.GetPositionByIndex(t.positionIndex, Size);
						uc.reached = false;
					}
				}
			}
		}
	}

	protected override void OnDestroy() {
		TargetArray.Dispose();
	}

	public void Update(float deltaTime) {
		_deltaTime = deltaTime;
		Update();
	}
}