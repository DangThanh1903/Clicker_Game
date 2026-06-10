#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class ItemSprite3DMeshGenerator : EditorWindow
{
    private const string DefaultSpriteFolder = "Assets/DevWork/Graphics/UI-UX/Items/Materials";
    private const string DefaultOutputFolder = "Assets/DevWork/Graphics/3D models/Items";

    [SerializeField] private string spriteFolder = DefaultSpriteFolder;
    [SerializeField] private string outputFolder = DefaultOutputFolder;
    [SerializeField] private float thickness = 0.12f;
    [SerializeField] [Range(0.01f, 1f)] private float alphaThreshold = 0.1f;
    [SerializeField] private bool overwriteAssets = true;
    [SerializeField] private bool restoreTextureReadability = true;
    [SerializeField] private bool frontMaterialTransparent = false;

    [MenuItem("Tools/Items/Generate 3D Item Meshes")]
    public static void Open()
    {
        GetWindow<ItemSprite3DMeshGenerator>("Item 3D Meshes");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        spriteFolder = EditorGUILayout.TextField("Sprite Folder", spriteFolder);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mesh", EditorStyles.boldLabel);
        thickness = Mathf.Max(0.001f, EditorGUILayout.FloatField("Thickness", thickness));
        alphaThreshold = EditorGUILayout.Slider("Alpha Threshold", alphaThreshold, 0.01f, 1f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Assets", EditorStyles.boldLabel);
        overwriteAssets = EditorGUILayout.Toggle("Overwrite Assets", overwriteAssets);
        restoreTextureReadability = EditorGUILayout.Toggle("Restore Readable Flag", restoreTextureReadability);
        frontMaterialTransparent = EditorGUILayout.Toggle("Transparent Front Material", frontMaterialTransparent);

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate From Folder"))
            GenerateFromFolder(spriteFolder, outputFolder, thickness, alphaThreshold, overwriteAssets, restoreTextureReadability, frontMaterialTransparent);

        if (GUILayout.Button("Generate Selected Sprites/Textures"))
            GenerateSelected(outputFolder, thickness, alphaThreshold, overwriteAssets, restoreTextureReadability, frontMaterialTransparent);
    }

    private static void GenerateSelected(string outputFolder, float thickness, float alphaThreshold, bool overwriteAssets, bool restoreReadable, bool transparentFront)
    {
        var sprites = new List<Sprite>();
        foreach (var obj in Selection.objects)
        {
            if (obj is Sprite sprite)
            {
                sprites.Add(sprite);
                continue;
            }

            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
                continue;

            AddSpritesAtPath(path, sprites);
        }

        GenerateSprites(sprites, outputFolder, thickness, alphaThreshold, overwriteAssets, restoreReadable, transparentFront);
    }

    private static void GenerateFromFolder(string spriteFolder, string outputFolder, float thickness, float alphaThreshold, bool overwriteAssets, bool restoreReadable, bool transparentFront)
    {
        if (!AssetDatabase.IsValidFolder(spriteFolder))
        {
            Debug.LogWarning($"[ItemSprite3DMeshGenerator] Sprite folder not found: {spriteFolder}");
            return;
        }

        var sprites = new List<Sprite>();
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { spriteFolder });
        foreach (string guid in textureGuids)
        {
            AddSpritesAtPath(AssetDatabase.GUIDToAssetPath(guid), sprites);
        }

        GenerateSprites(sprites, outputFolder, thickness, alphaThreshold, overwriteAssets, restoreReadable, transparentFront);
    }

    private static void GenerateSprites(List<Sprite> sprites, string outputFolder, float thickness, float alphaThreshold, bool overwriteAssets, bool restoreReadable, bool transparentFront)
    {
        EnsureFolder(outputFolder);
        EnsureFolder($"{outputFolder}/Meshes");
        EnsureFolder($"{outputFolder}/Materials");

        Material sideMaterial = GetOrCreateSideMaterial($"{outputFolder}/Materials/Item3D_SideBlack.mat");
        int generated = 0;

        foreach (Sprite sprite in sprites)
        {
            if (sprite == null)
                continue;

            string texturePath = AssetDatabase.GetAssetPath(sprite);
            Texture2D sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (sourceTexture == null)
            {
                Debug.LogWarning($"[ItemSprite3DMeshGenerator] Source texture not found for sprite '{sprite.name}'.");
                continue;
            }

            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            bool oldReadable = importer != null && importer.isReadable;

            try
            {
                if (importer != null && !importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                }

                Mesh mesh = BuildPixelExtrudedMesh(sprite, sourceTexture, thickness, alphaThreshold);
                if (mesh == null)
                    continue;

                string safeName = MakeSafeFileName(sprite.name);
                string meshPath = $"{outputFolder}/Meshes/{safeName}_3D.asset";
                string frontMaterialPath = $"{outputFolder}/Materials/{safeName}_Front.mat";

                SaveAsset(mesh, meshPath, overwriteAssets);
                Material frontMaterial = GetOrCreateFrontMaterial(frontMaterialPath, sourceTexture, transparentFront);

                // Keep a deterministic material asset pair near the generated mesh for easy assignment.
                EditorUtility.SetDirty(frontMaterial);
                EditorUtility.SetDirty(sideMaterial);

                generated++;
            }
            finally
            {
                if (importer != null && restoreReadable && importer.isReadable != oldReadable)
                {
                    importer.isReadable = oldReadable;
                    importer.SaveAndReimport();
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ItemSprite3DMeshGenerator] Generated {generated} item 3D mesh(es) into {outputFolder}.");
    }

    private static Mesh BuildPixelExtrudedMesh(Sprite sprite, Texture2D texture, float thickness, float alphaThreshold)
    {
        Rect rect = sprite.rect;
        int rectX = Mathf.RoundToInt(rect.x);
        int rectY = Mathf.RoundToInt(rect.y);
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);

        Color32[] pixels = texture.GetPixels32();
        bool[,] opaque = new bool[width, height];
        int opaqueCount = 0;
        byte threshold = (byte)Mathf.RoundToInt(alphaThreshold * 255f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int textureIndex = (rectY + y) * texture.width + rectX + x;
                bool isOpaque = textureIndex >= 0 && textureIndex < pixels.Length && pixels[textureIndex].a >= threshold;
                opaque[x, y] = isOpaque;
                if (isOpaque)
                    opaqueCount++;
            }
        }

        if (opaqueCount == 0)
        {
            Debug.LogWarning($"[ItemSprite3DMeshGenerator] Sprite '{sprite.name}' has no opaque pixels above threshold.");
            return null;
        }

        var vertices = new List<Vector3>(opaqueCount * 8);
        var uvs = new List<Vector2>(opaqueCount * 8);
        var frontBackTriangles = new List<int>(opaqueCount * 12);
        var sideTriangles = new List<int>(opaqueCount * 12);

        float ppu = sprite.pixelsPerUnit > 0 ? sprite.pixelsPerUnit : 32f;
        Vector2 pivot = sprite.pivot;
        float zFront = -thickness * 0.5f;
        float zBack = thickness * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!opaque[x, y])
                    continue;

                float x0 = (x - pivot.x) / ppu;
                float x1 = (x + 1f - pivot.x) / ppu;
                float y0 = (y - pivot.y) / ppu;
                float y1 = (y + 1f - pivot.y) / ppu;

                float u0 = (rectX + x) / (float)texture.width;
                float u1 = (rectX + x + 1f) / texture.width;
                float v0 = (rectY + y) / (float)texture.height;
                float v1 = (rectY + y + 1f) / texture.height;

                AddFrontBackPixel(vertices, uvs, frontBackTriangles, x0, x1, y0, y1, zFront, zBack, u0, u1, v0, v1);

                if (IsTransparentNeighbor(opaque, x - 1, y, width, height))
                    AddSideLeft(vertices, uvs, sideTriangles, x0, y0, y1, zFront, zBack);
                if (IsTransparentNeighbor(opaque, x + 1, y, width, height))
                    AddSideRight(vertices, uvs, sideTriangles, x1, y0, y1, zFront, zBack);
                if (IsTransparentNeighbor(opaque, x, y - 1, width, height))
                    AddSideBottom(vertices, uvs, sideTriangles, x0, x1, y0, zFront, zBack);
                if (IsTransparentNeighbor(opaque, x, y + 1, width, height))
                    AddSideTop(vertices, uvs, sideTriangles, x0, x1, y1, zFront, zBack);
            }
        }

        var mesh = new Mesh
        {
            name = $"{sprite.name}_3D"
        };

        if (vertices.Count > 65535)
            mesh.indexFormat = IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(frontBackTriangles, 0);
        mesh.SetTriangles(sideTriangles, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddFrontBackPixel(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        float x0,
        float x1,
        float y0,
        float y1,
        float zFront,
        float zBack,
        float u0,
        float u1,
        float v0,
        float v1)
    {
        int start = vertices.Count;

        vertices.Add(new Vector3(x0, y0, zFront));
        vertices.Add(new Vector3(x1, y0, zFront));
        vertices.Add(new Vector3(x1, y1, zFront));
        vertices.Add(new Vector3(x0, y1, zFront));

        uvs.Add(new Vector2(u0, v0));
        uvs.Add(new Vector2(u1, v0));
        uvs.Add(new Vector2(u1, v1));
        uvs.Add(new Vector2(u0, v1));

        triangles.Add(start + 0);
        triangles.Add(start + 2);
        triangles.Add(start + 1);
        triangles.Add(start + 0);
        triangles.Add(start + 3);
        triangles.Add(start + 2);

        start = vertices.Count;

        vertices.Add(new Vector3(x0, y0, zBack));
        vertices.Add(new Vector3(x1, y0, zBack));
        vertices.Add(new Vector3(x1, y1, zBack));
        vertices.Add(new Vector3(x0, y1, zBack));

        uvs.Add(new Vector2(u0, v0));
        uvs.Add(new Vector2(u1, v0));
        uvs.Add(new Vector2(u1, v1));
        uvs.Add(new Vector2(u0, v1));

        triangles.Add(start + 0);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start + 0);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }

    private static void AddSideLeft(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, float x, float y0, float y1, float zFront, float zBack)
    {
        int start = AddSideVertices(vertices, uvs,
            new Vector3(x, y0, zFront),
            new Vector3(x, y1, zFront),
            new Vector3(x, y1, zBack),
            new Vector3(x, y0, zBack));

        triangles.Add(start + 0);
        triangles.Add(start + 2);
        triangles.Add(start + 1);
        triangles.Add(start + 0);
        triangles.Add(start + 3);
        triangles.Add(start + 2);
    }

    private static void AddSideRight(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, float x, float y0, float y1, float zFront, float zBack)
    {
        int start = AddSideVertices(vertices, uvs,
            new Vector3(x, y0, zFront),
            new Vector3(x, y0, zBack),
            new Vector3(x, y1, zBack),
            new Vector3(x, y1, zFront));

        triangles.Add(start + 0);
        triangles.Add(start + 2);
        triangles.Add(start + 1);
        triangles.Add(start + 0);
        triangles.Add(start + 3);
        triangles.Add(start + 2);
    }

    private static void AddSideBottom(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, float x0, float x1, float y, float zFront, float zBack)
    {
        int start = AddSideVertices(vertices, uvs,
            new Vector3(x0, y, zFront),
            new Vector3(x0, y, zBack),
            new Vector3(x1, y, zBack),
            new Vector3(x1, y, zFront));

        triangles.Add(start + 0);
        triangles.Add(start + 2);
        triangles.Add(start + 1);
        triangles.Add(start + 0);
        triangles.Add(start + 3);
        triangles.Add(start + 2);
    }

    private static void AddSideTop(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, float x0, float x1, float y, float zFront, float zBack)
    {
        int start = AddSideVertices(vertices, uvs,
            new Vector3(x0, y, zFront),
            new Vector3(x1, y, zFront),
            new Vector3(x1, y, zBack),
            new Vector3(x0, y, zBack));

        triangles.Add(start + 0);
        triangles.Add(start + 2);
        triangles.Add(start + 1);
        triangles.Add(start + 0);
        triangles.Add(start + 3);
        triangles.Add(start + 2);
    }

    private static int AddSideVertices(List<Vector3> vertices, List<Vector2> uvs, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);
        uvs.Add(Vector2.zero);
        uvs.Add(Vector2.right);
        uvs.Add(Vector2.one);
        uvs.Add(Vector2.up);
        return start;
    }

    private static bool IsTransparentNeighbor(bool[,] opaque, int x, int y, int width, int height)
    {
        return x < 0 || y < 0 || x >= width || y >= height || !opaque[x, y];
    }

    private static Material GetOrCreateFrontMaterial(string path, Texture2D texture, bool transparent)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = CreateUnlitMaterial(Path.GetFileNameWithoutExtension(path), Color.white, transparent);
            AssetDatabase.CreateAsset(material, path);
        }

        SetMaterialTexture(material, texture);
        ConfigureMaterialSurface(material, transparent);
        return material;
    }

    private static Material GetOrCreateSideMaterial(string path)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = CreateUnlitMaterial("Item3D_SideBlack", new Color(0.015f, 0.015f, 0.015f, 1f), false);
            AssetDatabase.CreateAsset(material, path);
        }

        SetMaterialColor(material, new Color(0.015f, 0.015f, 0.015f, 1f));
        ConfigureMaterialSurface(material, false);
        return material;
    }

    private static Material CreateUnlitMaterial(string name, Color color, bool transparent)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Standard");

        var material = new Material(shader)
        {
            name = name
        };

        SetMaterialColor(material, color);
        ConfigureMaterialSurface(material, transparent);
        return material;
    }

    private static void SetMaterialTexture(Material material, Texture2D texture)
    {
        if (material == null || texture == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void ConfigureMaterialSurface(Material material, bool transparent)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", transparent ? 1f : 0f);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 0f);

        material.renderQueue = transparent ? (int)RenderQueue.Transparent : -1;
        material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");

        if (transparent)
        {
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        else
        {
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
    }

    private static void SaveAsset(Object asset, string path, bool overwrite)
    {
        Object existing = AssetDatabase.LoadAssetAtPath<Object>(path);
        if (existing != null)
        {
            if (!overwrite)
                return;

            EditorUtility.CopySerialized(asset, existing);
            EditorUtility.SetDirty(existing);
            return;
        }

        AssetDatabase.CreateAsset(asset, path);
    }

    private static void AddSpritesAtPath(string path, List<Sprite> sprites)
    {
        if (string.IsNullOrEmpty(path))
            return;

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is Sprite sprite && !sprites.Contains(sprite))
                sprites.Add(sprite);
        }
    }

    private static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder))
            return;

        string[] parts = assetFolder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string MakeSafeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');

        return value;
    }
}
#endif
