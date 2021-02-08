using Unity.Entities;
using Unity.Mathematics;

public struct TargetComponent : IComponentData {
	public bool created;
	public bool reached;
	public int index;
	public int currentPositionIndex;
	public float3 destination;
}