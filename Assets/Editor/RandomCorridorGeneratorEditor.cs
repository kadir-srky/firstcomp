using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RandomCorridorGenerator))]
public sealed class RandomCorridorGeneratorEditor : Editor
{
    private const string WallsPath = "Assets/3d_ai_assets/walls";
    private const string DoorsPath = "Assets/3d_ai_assets/doors";
    private const string FloorsPath = "Assets/3d_ai_assets/floors";
    private const string DoorFramePath = "Assets/3d_ai_assets/doors/doorframe1/doorframe01.obj";

    [MenuItem("GameObject/3D Object/Random Corridor Generator", false, 10)]
    private static void CreateGenerator(MenuCommand command)
    {
        var generatorObject = new GameObject("Random Corridor Generator");
        Undo.RegisterCreatedObjectUndo(generatorObject, "Create random corridor generator");
        GameObjectUtility.SetParentAndAlign(generatorObject, command.context as GameObject);
        Selection.activeObject = generatorObject;

        var generator = generatorObject.AddComponent<RandomCorridorGenerator>();
        AssignProjectAssets(generator);
        EditorUtility.SetDirty(generator);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var generator = (RandomCorridorGenerator)target;
        EditorGUILayout.Space();

        if (GUILayout.Button("Projeden Duvar ve Kapilari Yukle"))
        {
            Undo.RecordObject(generator, "Assign corridor assets");
            AssignProjectAssets(generator);
            EditorUtility.SetDirty(generator);
        }

        using (new EditorGUI.DisabledScope(!CanGenerate(generator)))
        {
            if (GUILayout.Button("Bitisik Duvarli Koridoru Olustur"))
                Generate(generator);
        }

        if (!CanGenerate(generator))
            EditorGUILayout.HelpBox("Duvarlar, zeminler, doorframe01 ve en az bir kapi atanmalidir.", MessageType.Warning);

        if (GUILayout.Button("Olusturulan Koridoru Sil"))
            Clear(generator);
    }

    private static void AssignProjectAssets(RandomCorridorGenerator generator)
    {
        generator.WallPrefabs = FindModels(WallsPath);
        generator.DoorPrefabs = FindModels(DoorsPath)
            .Where(asset => AssetDatabase.GetAssetPath(asset) != DoorFramePath)
            .ToArray();
        generator.DoorFramePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorFramePath);
        generator.FloorPrefabs = FindModels(FloorsPath);
    }

    private static GameObject[] FindModels(string folder)
    {
        return AssetDatabase.FindAssets("t:Model", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path))
            .Where(asset => asset != null)
            .OrderBy(asset => AssetDatabase.GetAssetPath(asset), StringComparer.Ordinal)
            .ToArray();
    }

    private static bool CanGenerate(RandomCorridorGenerator generator)
    {
        return generator.WallPrefabs != null && generator.WallPrefabs.Length > 0 &&
               generator.DoorFramePrefab != null &&
               generator.DoorPrefabs != null && generator.DoorPrefabs.Length > 0 &&
               generator.FloorPrefabs != null && generator.FloorPrefabs.Length > 0;
    }

    private static void Generate(RandomCorridorGenerator generator)
    {
        Clear(generator);

        var container = new GameObject(generator.GeneratedContainerName).transform;
        Undo.RegisterCreatedObjectUndo(container.gameObject, "Create random corridor");
        container.SetParent(generator.transform, false);
        // Keep the generated Hall at its native size. A zero scale would hide
        // it entirely, so the neutral Unity scale is (1, 1, 1).
        container.localScale = Vector3.one;

        // The same random wall is used on both sides of a segment so the two
        // rows, and their door openings, remain aligned down the corridor.
        var wallChoices = ChooseNonRepeatingWalls(generator.WallPrefabs, generator.SegmentCount);

        var corridorLength = GenerateWallRow(generator, container, "Left", -generator.CorridorWidth * .5f, generator.WallYaw, wallChoices);
        // The right row is mirrored so its finished side faces into the corridor.
        GenerateWallRow(generator, container, "Right", generator.CorridorWidth * .5f, generator.WallYaw + 180f, wallChoices);

        if (generator.FloorPrefabs != null && generator.FloorPrefabs.Length > 0)
            GenerateFloor(generator, container, corridorLength);
    }

    private static float GenerateWallRow(RandomCorridorGenerator generator, Transform parent, string side, float x, float yaw, GameObject[] wallChoices)
    {
        var cursor = 0f;

        for (var segment = 0; segment < wallChoices.Length; segment++)
        {
            cursor = CreateWall(generator, parent, segment, side, x, yaw, cursor, wallChoices[segment]);

            // The last wall has no following wall, so it does not receive an
            // unnecessary door opening after it.
            var hasNextWall = segment < wallChoices.Length - 1;
            if (hasNextWall && (segment + 1) % generator.SegmentsPerDoor == 0)
                cursor = CreateDoorSet(generator, parent, side, x, yaw, cursor, segment + 1);
        }

        return cursor;
    }

    private static void GenerateFloor(RandomCorridorGenerator generator, Transform parent, float corridorLength)
    {
        var cursor = 0f;
        var tileIndex = 1;
        const int tilesAcross = 2;
        var targetTileWidth = generator.CorridorWidth / tilesAcross;

        while (cursor < corridorLength)
        {
            // One random floor model per row keeps the two crosswise tiles
            // aligned while varying the floor down the corridor.
            var prefab = generator.FloorPrefabs[UnityEngine.Random.Range(0, generator.FloorPrefabs.Length)];
            var firstTile = CreateFloorTile(generator, parent, prefab, tileIndex++, 0, targetTileWidth, cursor);
            CreateFloorTile(generator, parent, prefab, tileIndex++, 1, targetTileWidth, cursor);
            cursor += firstTile;
        }
    }

    private static float CreateFloorTile(RandomCorridorGenerator generator, Transform parent, GameObject prefab, int index, int column, float targetWidth, float z)
    {
        var tile = Instantiate(prefab, parent);
        Undo.RegisterCreatedObjectUndo(tile, "Create corridor floor tile");
        tile.name = $"Floor {index:00} ({prefab.name})";
        // The imported floor assets lie in their XY plane.  Flip them so the
        // underside faces up, then lay that face flat on the XZ ground.
        tile.transform.localRotation = Quaternion.Euler(-90f, generator.FloorYaw, 0f);

        var bounds = GetBoundsInParent(tile, parent);
        var width = Mathf.Max(0.01f, bounds.size.x);
        tile.transform.localScale = new Vector3(targetWidth / width, 1f, 1f);
        bounds = GetBoundsInParent(tile, parent);

        var xStart = -generator.CorridorWidth * .5f + column * targetWidth;
        // Keep the top of the tile exactly at ground level.  Its thickness is
        // therefore below y = 0 and cannot obstruct a door while opening.
        tile.transform.localPosition = new Vector3(
            xStart - bounds.min.x,
            generator.FloorHeight - bounds.max.y,
            z - bounds.min.z);
        AddMeshColliders(tile, generator.AddMeshColliders);
        return Mathf.Max(0.01f, bounds.size.z - generator.WallJoinOverlap);
    }

    private static GameObject[] ChooseNonRepeatingWalls(GameObject[] prefabs, int count)
    {
        var choices = new GameObject[count];
        for (var index = 0; index < count; index++)
        {
            var prefabIndex = UnityEngine.Random.Range(0, prefabs.Length);
            if (prefabs.Length > 1 && index > 0 && prefabs[prefabIndex] == choices[index - 1])
            {
                // Pick from every item except the preceding one.  The index
                // adjustment keeps all remaining walls equally likely.
                var previousIndex = System.Array.IndexOf(prefabs, choices[index - 1]);
                var offset = UnityEngine.Random.Range(1, prefabs.Length);
                prefabIndex = (previousIndex + offset) % prefabs.Length;
            }

            choices[index] = prefabs[prefabIndex];
        }

        return choices;
    }

    private static float CreateWall(RandomCorridorGenerator generator, Transform parent, int segment, string side, float x, float yaw, float cursor, GameObject prefab)
    {
        var wall = Instantiate(prefab, parent);
        Undo.RegisterCreatedObjectUndo(wall, "Create corridor wall");
        wall.name = $"Wall {segment + 1:00} {side} ({prefab.name})";
        wall.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var bounds = GetBoundsInParent(wall, parent);
        wall.transform.localPosition = new Vector3(x, -bounds.min.y, cursor - bounds.min.z);
        AddMeshColliders(wall, generator.AddMeshColliders);
        return cursor + Mathf.Max(0.01f, bounds.size.z - generator.WallJoinOverlap);
    }

    private static float CreateDoorSet(RandomCorridorGenerator generator, Transform parent, string side, float x, float yaw, float cursor, int afterWall)
    {
        var doorSet = new GameObject($"Door Set {side} after Wall {afterWall:00}").transform;
        Undo.RegisterCreatedObjectUndo(doorSet.gameObject, "Create corridor door set");
        doorSet.SetParent(parent, false);
        doorSet.localRotation = Quaternion.Euler(0f, yaw + generator.DoorYaw, 0f);

        var frame = Instantiate(generator.DoorFramePrefab, doorSet);
        Undo.RegisterCreatedObjectUndo(frame, "Create door frame");
        frame.name = "Door Frame";
        frame.transform.localRotation = Quaternion.identity;
        var frameBounds = GetBoundsInParent(frame, parent);
        doorSet.localPosition = new Vector3(x, -frameBounds.min.y, cursor - frameBounds.min.z);
        frame.transform.localPosition = Vector3.zero;
        AddMeshColliders(frame, generator.AddMeshColliders);

        var prefab = generator.DoorPrefabs[UnityEngine.Random.Range(0, generator.DoorPrefabs.Length)];
        var door = Instantiate(prefab, frame.transform);
        Undo.RegisterCreatedObjectUndo(door, "Create corridor door");
        door.name = $"Door ({prefab.name})";
        door.transform.localPosition = generator.DoorLocalOffset;
        door.transform.localRotation = Quaternion.identity;
        // The door's asset origin may not match the frame's origin. Align its
        // actual lowest mesh point to the Hall ground, not to the frame root.
        var doorBoundsInHall = GetBoundsInParent(door, parent);
        door.transform.localPosition += Vector3.up * -doorBoundsInHall.min.y;
        var doorBounds = GetBoundsInLocalSpace(door);
        AddMeshColliders(door, generator.AddMeshColliders);

        // Imported models normally rotate around their centre.  Move the
        // rotation pivot to the door's local left edge so it behaves like a
        // real door attached to the frame by a hinge.
        var hinge = new GameObject("Left Hinge").transform;
        Undo.RegisterCreatedObjectUndo(hinge.gameObject, "Create door hinge");
        hinge.SetParent(frame.transform, false);
        hinge.localPosition = door.transform.localPosition + new Vector3(doorBounds.min.x, 0f, 0f);
        hinge.localRotation = door.transform.localRotation;
        door.transform.SetParent(hinge, true);

        var interactable = hinge.GetComponent<Interactable>();
        if (interactable == null)
            interactable = Undo.AddComponent<Interactable>(hinge.gameObject);
        interactable.type = InteractType.Door;
        interactable.promptMessage = "Kapiyi Ac / Kapat";
        // The next wall overlaps the frame very slightly too, preventing a
        // hairline seam at either side of a doorway.
        return cursor + Mathf.Max(0.01f, frameBounds.size.z - generator.WallJoinOverlap);
    }

    // Returns bounds in the generated-corridor container's local space.  This
    // includes the model's rotation, so its Z size is the exact row length.
    private static Bounds GetBoundsInParent(GameObject root, Transform parent)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var hasBounds = false;
        var bounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (var renderer in renderers)
        {
            var matrix = parent.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var localBounds = renderer.localBounds;
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var corner = localBounds.center + Vector3.Scale(localBounds.extents, new Vector3(x, y, z));
                var point = matrix.MultiplyPoint3x4(corner);
                if (hasBounds)
                    bounds.Encapsulate(point);
                else
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasBounds = true;
                }
            }
        }

        return hasBounds ? bounds : new Bounds(Vector3.zero, new Vector3(1f, 1f, 1f));
    }

    private static Bounds GetBoundsInLocalSpace(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var hasBounds = false;
        var bounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (var renderer in renderers)
        {
            var matrix = root.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var localBounds = renderer.localBounds;
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var corner = localBounds.center + Vector3.Scale(localBounds.extents, new Vector3(x, y, z));
                var point = matrix.MultiplyPoint3x4(corner);
                if (hasBounds)
                    bounds.Encapsulate(point);
                else
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasBounds = true;
                }
            }
        }

        return hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.one);
    }

    private static void AddMeshColliders(GameObject root, bool shouldAdd)
    {
        if (!shouldAdd)
            return;

        foreach (var meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh != null && meshFilter.GetComponent<MeshCollider>() == null)
                meshFilter.gameObject.AddComponent<MeshCollider>();
        }
    }

    private static void Clear(RandomCorridorGenerator generator)
    {
        var container = generator.transform.Find(generator.GeneratedContainerName);
        if (container != null)
            Undo.DestroyObjectImmediate(container.gameObject);
    }
}
