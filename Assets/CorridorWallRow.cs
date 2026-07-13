using UnityEngine;

/// <summary>
/// Creates one continuous row of wall prefabs. Add this component to an empty
/// GameObject, set its position to the beginning of the wall, then use the
/// Generate button in the Inspector.
/// </summary>
public sealed class CorridorWallRow : MonoBehaviour
{
    [SerializeField] private GameObject wallPrefab;
    [SerializeField, Min(1)] private int wallCount = 8;
    [SerializeField] private Axis rowAxis = Axis.Z;
    [SerializeField] private float wallYaw = 90f;
    [SerializeField, Min(0f)] private float gap = 0f;
    [SerializeField] private string generatedContainerName = "Generated Wall Row";

    public GameObject WallPrefab
    {
        get => wallPrefab;
        set => wallPrefab = value;
    }

    public enum Axis { X, Z }

    public Vector3 Direction => rowAxis == Axis.X ? Vector3.right : Vector3.forward;
    public int WallCount => wallCount;
    public float WallYaw => wallYaw;
    public float Gap => gap;
    public string GeneratedContainerName => generatedContainerName;
}
