using UnityEngine;

/// <summary>
/// Editor-assisted generator for a straight corridor.  Walls in each side row
/// are edge-to-edge.  A framed, random door replaces the space between every
/// <see cref="segmentsPerDoor"/> walls and the following wall.
/// </summary>
public sealed class RandomCorridorGenerator : MonoBehaviour
{
    [Header("Assets")]
    [SerializeField] private GameObject[] wallPrefabs;
    [SerializeField] private GameObject doorFramePrefab;
    [SerializeField] private GameObject[] doorPrefabs;
    [SerializeField] private GameObject[] floorPrefabs;
    [SerializeField] private GameObject[] ceilingPrefabs;
    [SerializeField] private GameObject ceilingLightPrefab;

    [Header("Corridor")]
    [Tooltip("Ondalik kisim, son duvarin kullanilacak uzunluk oranidir. Ornek: 10.5 = 10 tam + yarim duvar.")]
    [SerializeField, Min(2f)] private float segmentCount = 10f;
    [SerializeField, Min(1)] private int segmentsPerDoor = 2;
    [SerializeField, Min(0.01f)] private float corridorWidth = 2.4f;
    [Tooltip("Kucuk bindirme payi; duvar ek yerlerinden arkanin gorunmesini engeller.")]
    [SerializeField, Min(0f)] private float wallJoinOverlap = 0.03f;
    [SerializeField] private float wallYaw = 90f;
    [SerializeField] private float doorYaw = 0f;
    [SerializeField] private Vector3 doorLocalOffset = Vector3.zero;
    [SerializeField] private float floorYaw = 0f;
    [SerializeField] private float floorHeight = 0f;
    [Tooltip("Tavanin duvarlarin en ust noktasindan yuksekligi.")]
    [SerializeField] private float ceilingHeightOffset = 0f;
    [SerializeField] private float ceilingYaw = 0f;
    [Header("Ceiling Lights")]
    [SerializeField] private float ceilingLightYaw = 0f;
    [SerializeField, Min(0.01f)] private float ceilingLightRange = 8f;
    [SerializeField] private Color ceilingLightColor = new Color(1f, 0.82f, 0.62f);
    [SerializeField] private bool addMeshColliders = true;
    [SerializeField] private string generatedContainerName = "Generated Random Corridor";

    public GameObject[] WallPrefabs { get => wallPrefabs; set => wallPrefabs = value; }
    public GameObject DoorFramePrefab { get => doorFramePrefab; set => doorFramePrefab = value; }
    public GameObject[] DoorPrefabs { get => doorPrefabs; set => doorPrefabs = value; }
    public GameObject[] FloorPrefabs { get => floorPrefabs; set => floorPrefabs = value; }
    public GameObject[] CeilingPrefabs { get => ceilingPrefabs; set => ceilingPrefabs = value; }
    public GameObject CeilingLightPrefab { get => ceilingLightPrefab; set => ceilingLightPrefab = value; }
    public float SegmentCount => segmentCount;
    public int SegmentObjectCount => Mathf.CeilToInt(segmentCount);
    public int SegmentsPerDoor => segmentsPerDoor;
    public float CorridorWidth => corridorWidth;
    public float WallJoinOverlap => wallJoinOverlap;
    public float WallYaw => wallYaw;
    public float DoorYaw => doorYaw;
    public Vector3 DoorLocalOffset => doorLocalOffset;
    public float FloorYaw => floorYaw;
    public float FloorHeight => floorHeight;
    public float CeilingHeightOffset => ceilingHeightOffset;
    public float CeilingYaw => ceilingYaw;
    public float CeilingLightScale => 0.3f;
    public float CeilingLightYaw => ceilingLightYaw;
    public float CeilingLightIntensity => 2f;
    public float CeilingLightRange => ceilingLightRange;
    public Color CeilingLightColor => ceilingLightColor;
    public bool AddMeshColliders => addMeshColliders;
    public string GeneratedContainerName => generatedContainerName;

    private void OnValidate()
    {
        segmentCount = Mathf.Max(2f, segmentCount);
        segmentsPerDoor = Mathf.Max(1, segmentsPerDoor);
        corridorWidth = Mathf.Max(0.01f, corridorWidth);
        wallJoinOverlap = Mathf.Max(0f, wallJoinOverlap);
        ceilingLightRange = Mathf.Max(0.01f, ceilingLightRange);
    }
}
