using Unity.Entities;
using Unity.Mathematics;

public struct UnitComponent : IComponentData {
    public bool reached;
    //Movement
    public float3 waypointDirection;
    public float3 destination;
    public float minDistanceReached;
    public int rotationSpeed;
    //Collision Avoidance
    public float3 avoidanceDirection;
    //Debug
    public bool collided;
    public bool avoiding;
    public int hashKey;
    public float timeStamp;
    public int currentPositionIndex;
    public int index;
}