using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CorridorWallRow))]
public sealed class CorridorWallRowEditor : Editor
{
    private const string DefaultWallPath = "Assets/Scenes/ZNS3D/VintageLivingRoom/Meshes/wall_default.fbx";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var row = (CorridorWallRow)target;
        if (row.WallPrefab == null)
        {
            if (GUILayout.Button("Varsay\u0131lan wall_default prefab\u0131n\u0131 se\u00e7"))
            {
                Undo.RecordObject(row, "Assign default wall prefab");
                row.WallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultWallPath);
                EditorUtility.SetDirty(row);
            }
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(row.WallPrefab == null))
        {
            if (GUILayout.Button("Duvar Sat\u0131r\u0131n\u0131 Olu\u015ftur"))
                Generate(row);
        }

        if (GUILayout.Button("Olu\u015fturulan Duvarlar\u0131 Sil"))
            Clear(row);
    }

    private static void Generate(CorridorWallRow row)
    {
        Clear(row);

        var container = new GameObject(row.GeneratedContainerName).transform;
        Undo.RegisterCreatedObjectUndo(container.gameObject, "Create wall row");
        container.SetParent(row.transform, false);

        var rotation = Quaternion.Euler(0f, row.WallYaw, 0f);
        var direction = row.Direction;
        var step = GetWallLength(row.WallPrefab, rotation, direction) + row.Gap;

        for (var i = 0; i < row.WallCount; i++)
        {
            var wall = (GameObject)PrefabUtility.InstantiatePrefab(row.WallPrefab, container);
            Undo.RegisterCreatedObjectUndo(wall, "Create corridor wall");
            wall.name = $"Wall {i + 1:00}";
            wall.transform.localPosition = direction * step * i;
            wall.transform.localRotation = rotation;
        }
    }

    private static void Clear(CorridorWallRow row)
    {
        var container = row.transform.Find(row.GeneratedContainerName);
        if (container != null)
            Undo.DestroyObjectImmediate(container.gameObject);
    }

    private static float GetWallLength(GameObject prefab, Quaternion rotation, Vector3 direction)
    {
        // FBX model assets are not .prefab files, so their mesh bounds are
        // measured directly instead of using PrefabUtility.LoadPrefabContents.
        var renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return 1f;

        var modelRotation = Matrix4x4.Rotate(rotation);
        var hasBounds = false;
        var bounds = new Bounds();

        foreach (var renderer in renderers)
        {
            var localBounds = renderer.localBounds;
            var matrix = modelRotation * renderer.transform.localToWorldMatrix;

            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var corner = localBounds.center + Vector3.Scale(
                    localBounds.extents, new Vector3(x, y, z));
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

        if (!hasBounds)
            return 1f;

        return Mathf.Abs(direction.x) > 0.5f ? bounds.size.x : bounds.size.z;
    }
}
