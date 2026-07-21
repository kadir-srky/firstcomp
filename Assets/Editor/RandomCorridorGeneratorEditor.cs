using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RandomCorridorGenerator))]
public sealed class RandomCorridorGeneratorEditor : Editor
{
    private const string WallsPath = "Assets/3d_ai_assets/walls";
    private const string DoorsPath = "Assets/3d_ai_assets/doors";
    private const string FloorsPath = "Assets/3d_ai_assets/floors";
    private const string CeilingsPath = "Assets/3d_ai_assets/ceiling";
    private const string CeilingLightPath = "Assets/3d_ai_assets/lights/light4/light04.obj";
    private const string ExcludedFloorPath = "Assets/3d_ai_assets/floors/floor1/floor01.obj";
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

        if (GUILayout.Button("Projeden Koridor Assetlerini Yukle"))
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
            EditorGUILayout.HelpBox("Duvarlar, zeminler, tavanlar, light04, doorframe01 ve en az bir kapi atanmalidir.", MessageType.Warning);

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
        generator.FloorPrefabs = FindModels(FloorsPath)
            .Where(floor => !IsExcludedFloor(floor))
            .ToArray();
        generator.CeilingPrefabs = FindPrefabs(CeilingsPath);
        generator.CeilingLightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CeilingLightPath);
    }

    private static GameObject[] FindPrefabs(string folder)
    {
        return AssetDatabase.FindAssets("t:Prefab", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path))
            .Where(asset => asset != null)
            .OrderBy(asset => AssetDatabase.GetAssetPath(asset), StringComparer.Ordinal)
            .ToArray();
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
               generator.CeilingPrefabs != null && generator.CeilingPrefabs.Length > 0 &&
               generator.CeilingLightPrefab != null &&
               generator.FloorPrefabs != null && generator.FloorPrefabs.Any(floor => floor != null && !IsExcludedFloor(floor));
    }

    private static void Generate(RandomCorridorGenerator generator)
    {
        Clear(generator);

        var container = new GameObject(generator.GeneratedContainerName).transform;
        Undo.RegisterCreatedObjectUndo(container.gameObject, "Create random corridor");
        container.SetParent(generator.transform, false);
        container.localPosition = new Vector3(0f, 0.1f, 0f);
        // Keep the complete generated Hall at a consistent gameplay scale.
        // Floors, walls, frames, and doors share this root so they scale together.
        container.localScale = Vector3.one * 1.7f;

        // The same random wall is used on both sides of a segment so the two
        // rows, and their door openings, remain aligned down the corridor.
        var wallChoices = ChooseNonRepeatingWalls(generator.WallPrefabs, generator.SegmentObjectCount);

        var wallSpanEnds = new List<float>();
        var corridorLength = GenerateWallRow(generator, container, "Left", -generator.CorridorWidth * .5f, generator.WallYaw, wallChoices, wallSpanEnds);
        // The right row is mirrored so its finished side faces into the corridor.
        GenerateWallRow(generator, container, "Right", generator.CorridorWidth * .5f, generator.WallYaw + 180f, wallChoices, null);

        var corridorBounds = GetBoundsInParent(container.gameObject, container);
        GenerateCeiling(generator, container, wallSpanEnds, corridorBounds.max.y);

        var availableFloors = generator.FloorPrefabs == null
            ? Array.Empty<GameObject>()
            : generator.FloorPrefabs.Where(floor => floor != null && !IsExcludedFloor(floor)).ToArray();
        if (availableFloors.Length > 0)
            GenerateFloor(generator, container, corridorLength, availableFloors);
    }

    private static float GenerateWallRow(RandomCorridorGenerator generator, Transform parent, string side, float x, float yaw, GameObject[] wallChoices, List<float> wallSpanEnds)
    {
        var cursor = 0f;

        for (var segment = 0; segment < wallChoices.Length; segment++)
        {
            var isLastWall = segment == wallChoices.Length - 1;
            var fractionalPart = generator.SegmentCount - Mathf.Floor(generator.SegmentCount);
            var lengthScale = isLastWall && fractionalPart > 0.0001f ? fractionalPart : 1f;
            cursor = CreateWall(generator, parent, segment, side, x, yaw, cursor, wallChoices[segment], lengthScale);

            // The last wall has no following wall, so it does not receive an
            // unnecessary door opening after it.
            var hasNextWall = segment < wallChoices.Length - 1;
            if (hasNextWall && (segment + 1) % generator.SegmentsPerDoor == 0)
                cursor = CreateDoorSet(generator, parent, side, x, yaw, cursor, segment + 1);

            wallSpanEnds?.Add(cursor);
        }

        return cursor;
    }

    private static void GenerateCeiling(RandomCorridorGenerator generator, Transform parent, List<float> wallSpanEnds, float wallTop)
    {
        if (wallSpanEnds.Count == 0)
            return;

        var wallIndex = 0;
        var start = 0f;
        var ceilingIndex = 1;
        GameObject previousPrefab = null;

        while (wallIndex < wallSpanEnds.Count)
        {
            var remaining = wallSpanEnds.Count - wallIndex;
            // These choices never leave a one-wall remainder, so every
            // generated ceiling always spans exactly two or three walls.
            var wallCount = remaining == 2 || remaining == 4 ? 2 :
                remaining == 3 ? 3 : UnityEngine.Random.Range(2, 4);

            var end = wallSpanEnds[wallIndex + wallCount - 1];
            var prefab = ChooseDifferentPrefab(generator.CeilingPrefabs, previousPrefab);
            previousPrefab = prefab;
            CreateCeilingTile(generator, parent, prefab, ceilingIndex++, wallCount, start, end, wallTop);

            start = end;
            wallIndex += wallCount;
        }
    }

    private static void CreateCeilingTile(RandomCorridorGenerator generator, Transform parent, GameObject prefab, int index, int wallCount, float start, float end, float wallTop)
    {
        var ceiling = Instantiate(prefab, parent);
        Undo.RegisterCreatedObjectUndo(ceiling, "Create corridor ceiling");
        ceiling.name = $"Ceiling {index:00} - {wallCount} Walls ({prefab.name})";
        ceiling.transform.localRotation = Quaternion.Euler(0f, generator.CeilingYaw, 0f);

        var bounds = GetBoundsInParent(ceiling, parent);
        var targetLength = Mathf.Max(0.01f, end - start);
        ceiling.transform.localScale = new Vector3(
            ceiling.transform.localScale.x * generator.CorridorWidth / Mathf.Max(0.01f, bounds.size.x),
            ceiling.transform.localScale.y,
            ceiling.transform.localScale.z * targetLength / Mathf.Max(0.01f, bounds.size.z));
        bounds = GetBoundsInParent(ceiling, parent);
        ceiling.transform.localPosition += new Vector3(
            -bounds.center.x,
            wallTop + generator.CeilingHeightOffset - bounds.min.y,
            start - bounds.min.z);
        AddMeshColliders(ceiling, generator.AddMeshColliders);

        bounds = GetBoundsInParent(ceiling, parent);
        CreateCeilingLight(generator, parent, index, bounds);
    }

    private static void CreateCeilingLight(RandomCorridorGenerator generator, Transform parent, int index, Bounds ceilingBounds)
    {
        var fixture = Instantiate(generator.CeilingLightPrefab, parent);
        Undo.RegisterCreatedObjectUndo(fixture, "Create ceiling light fixture");
        fixture.name = $"Ceiling Light {index:00} ({generator.CeilingLightPrefab.name})";
        fixture.transform.localRotation = Quaternion.Euler(0f, generator.CeilingLightYaw, 0f);
        fixture.transform.localScale = Vector3.one * generator.CeilingLightScale;

        // Centre the fixture under the panel and touch its topmost mesh point
        // to the corridor-facing (bottom) surface of the ceiling.
        var fixtureBounds = GetBoundsInParent(fixture, parent);
        fixture.transform.localPosition += new Vector3(
            ceilingBounds.center.x - fixtureBounds.center.x,
            ceilingBounds.min.y - fixtureBounds.max.y,
            ceilingBounds.center.z - fixtureBounds.center.z);

        var placedBounds = GetBoundsInParent(fixture, parent);
        var lightObject = new GameObject("Corridor Point Light");
        Undo.RegisterCreatedObjectUndo(lightObject, "Create corridor point light");
        lightObject.transform.SetParent(fixture.transform, true);
        lightObject.transform.position = parent.TransformPoint(new Vector3(
            placedBounds.center.x,
            placedBounds.min.y,
            placedBounds.center.z));

        var pointLight = Undo.AddComponent<Light>(lightObject);
        pointLight.type = LightType.Point;
        pointLight.color = generator.CeilingLightColor;
        pointLight.intensity = generator.CeilingLightIntensity;
        pointLight.range = generator.CeilingLightRange;
        pointLight.shadows = LightShadows.Soft;
        pointLight.renderMode = LightRenderMode.Auto;

        AddMeshColliders(fixture, generator.AddMeshColliders);
    }

    private static void GenerateFloor(RandomCorridorGenerator generator, Transform parent, float corridorLength, GameObject[] floorPrefabs)
    {
        var cursor = 0f;
        var tileIndex = 1;
        GameObject previousPrefab = null;
        const int tilesAcross = 1;
        var targetTileWidth = generator.CorridorWidth / tilesAcross;

        while (cursor < corridorLength)
        {
            // One random floor model per row keeps a single continuous floor
            // strip along the corridor while varying the surface down its length.
            var prefab = ChooseDifferentPrefab(floorPrefabs, previousPrefab);
            previousPrefab = prefab;
            var randomYaw = generator.FloorYaw + (UnityEngine.Random.Range(0, 2) * 180f);
            var rowLength = 0f;
            for (var column = 0; column < tilesAcross; column++)
            {
                var remainingLength = corridorLength - cursor;
                var tileLength = CreateFloorTile(generator, parent, prefab, tileIndex++, column, targetTileWidth, cursor, randomYaw, remainingLength);
                if (column == 0)
                    rowLength = tileLength;
            }

            cursor += rowLength;
        }
    }

    private static float CreateFloorTile(RandomCorridorGenerator generator, Transform parent, GameObject prefab, int index, int column, float targetWidth, float z, float yaw, float maximumLength)
    {
        var tile = Instantiate(prefab, parent);
        Undo.RegisterCreatedObjectUndo(tile, "Create corridor floor tile");
        tile.name = $"Floor {index:00} ({prefab.name})";
        // These floor models are already imported flat in this project. Keep
        // their native pitch and only allow an optional horizontal yaw.
        tile.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        var bounds = GetBoundsInParent(tile, parent);
        var width = Mathf.Max(0.01f, bounds.size.x);
        tile.transform.localScale = new Vector3(targetWidth / width, 1f, 1f);
        bounds = GetBoundsInParent(tile, parent);

        // Only the final row is shortened.  This makes the floor end exactly
        // where the final generated wall ends, with neither an overhang nor a gap.
        if (bounds.size.z > maximumLength)
        {
            tile.transform.localScale = new Vector3(
                tile.transform.localScale.x,
                tile.transform.localScale.y,
                tile.transform.localScale.z * (maximumLength / bounds.size.z));
            bounds = GetBoundsInParent(tile, parent);
        }

        var xStart = -generator.CorridorWidth * .5f + column * targetWidth;
        // Keep the top of the tile exactly at ground level.  Its thickness is
        // therefore below y = 0 and cannot obstruct a door while opening.
        tile.transform.localPosition = new Vector3(
            xStart - bounds.min.x,
            generator.FloorHeight - bounds.max.y,
            z - bounds.min.z);
        AddMeshColliders(tile, generator.AddMeshColliders);
        return Mathf.Max(0.01f, bounds.size.z);
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

    private static GameObject ChooseDifferentPrefab(GameObject[] prefabs, GameObject previousPrefab)
    {
        if (prefabs.Length == 1 || previousPrefab == null)
            return prefabs[UnityEngine.Random.Range(0, prefabs.Length)];

        var candidates = prefabs.Where(prefab => prefab != previousPrefab).ToArray();
        return candidates[UnityEngine.Random.Range(0, candidates.Length)];
    }

    private static bool IsExcludedFloor(GameObject floor)
    {
        return AssetDatabase.GetAssetPath(floor).Replace('\\', '/') == ExcludedFloorPath;
    }

    private static float CreateWall(RandomCorridorGenerator generator, Transform parent, int segment, string side, float x, float yaw, float cursor, GameObject prefab, float lengthScale)
    {
        var wall = Instantiate(prefab, parent);
        Undo.RegisterCreatedObjectUndo(wall, "Create corridor wall");
        wall.name = $"Wall {segment + 1:00} {side} ({prefab.name})";
        wall.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        ScaleAlongCorridor(wall.transform, lengthScale);
        var bounds = GetBoundsInParent(wall, parent);
        wall.transform.localPosition = new Vector3(x, -bounds.min.y, cursor - bounds.min.z);
        AddMeshColliders(wall, generator.AddMeshColliders);
        return cursor + Mathf.Max(0.01f, bounds.size.z - generator.WallJoinOverlap);
    }

    private static void ScaleAlongCorridor(Transform wall, float lengthScale)
    {
        // Convert the corridor's forward axis into the rotated model's local
        // space, then shorten whichever horizontal model axis runs lengthwise.
        var localForward = Quaternion.Inverse(wall.localRotation) * Vector3.forward;
        var scale = wall.localScale;
        if (Mathf.Abs(localForward.x) >= Mathf.Abs(localForward.z))
            scale.x *= lengthScale;
        else
            scale.z *= lengthScale;
        wall.localScale = scale;
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
        interactable.openAwayFromInteractor = true;
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
