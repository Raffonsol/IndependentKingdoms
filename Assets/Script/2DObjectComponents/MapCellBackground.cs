using System.Collections.Generic;
using UnityEngine;

public class MapCellBackground : MonoBehaviour
{
    private const int CellsPerMesh = 16000;

    private readonly List<Mesh> generatedMeshes = new List<Mesh>();
    private Material generatedMaterial;

    private void Start()
    {
        SpriteRenderer sourceRenderer = GetComponent<SpriteRenderer>();
        if (sourceRenderer == null || sourceRenderer.sprite == null)
        {
            Debug.LogError("Map cell background requires a SpriteRenderer with a sprite.", this);
            enabled = false;
            return;
        }

        MapManager mapManager = MapManager.Instance;
        if (mapManager == null || mapManager.GridCellSize <= 0f)
        {
            Debug.LogError("Map cell background requires an initialized map grid.", this);
            enabled = false;
            return;
        }

        Vector2 mapMinimum = (Vector2)mapManager.MapCenter - mapManager.MapSize * 0.5f;
        Vector2 mapMaximum = (Vector2)mapManager.MapCenter + mapManager.MapSize * 0.5f;
        float cellSize = mapManager.GridCellSize;
        int minimumCellX = Mathf.FloorToInt(mapMinimum.x / cellSize);
        int minimumCellY = Mathf.FloorToInt(mapMinimum.y / cellSize);
        int maximumCellX = Mathf.CeilToInt(mapMaximum.x / cellSize) - 1;
        int maximumCellY = Mathf.CeilToInt(mapMaximum.y / cellSize) - 1;
        int columns = maximumCellX - minimumCellX + 1;
        int rows = maximumCellY - minimumCellY + 1;
        long totalCellCount = (long)columns * rows;
        if (columns <= 0 || rows <= 0 || totalCellCount > int.MaxValue)
        {
            Debug.LogError("Map cell background received invalid map dimensions.", this);
            enabled = false;
            return;
        }

        Material sourceMaterial = sourceRenderer.sharedMaterial;
        if (sourceMaterial == null)
        {
            Debug.LogError("Map cell background requires a SpriteRenderer material.", this);
            enabled = false;
            return;
        }

        generatedMaterial = new Material(sourceMaterial);
        generatedMaterial.name = "Map Cell Background Material";
        generatedMaterial.mainTexture = sourceRenderer.sprite.texture;

        System.Random random = new System.Random();
        Rect spriteRect = sourceRenderer.sprite.textureRect;
        Vector2 textureSize = new Vector2(
            sourceRenderer.sprite.texture.width,
            sourceRenderer.sprite.texture.height);
        Rect spriteUvRect = new Rect(
            spriteRect.x / textureSize.x,
            spriteRect.y / textureSize.y,
            spriteRect.width / textureSize.x,
            spriteRect.height / textureSize.y);
        Vector3 mapCenter = mapManager.MapCenter;

        for (int firstCell = 0; firstCell < totalCellCount; firstCell += CellsPerMesh)
        {
            int chunkCellCount = Mathf.Min(CellsPerMesh, (int)totalCellCount - firstCell);
            CreateMeshChunk(
                sourceRenderer,
                random,
                spriteUvRect,
                mapCenter.z,
                minimumCellX,
                minimumCellY,
                columns,
                firstCell,
                chunkCellCount,
                cellSize);
        }

        sourceRenderer.enabled = false;
    }

    private void CreateMeshChunk(
        SpriteRenderer sourceRenderer,
        System.Random random,
        Rect spriteUvRect,
        float zPosition,
        int minimumCellX,
        int minimumCellY,
        int columns,
        int firstCell,
        int cellCount,
        float cellSize)
    {
        Vector3[] vertices = new Vector3[cellCount * 4];
        Vector2[] uvs = new Vector2[cellCount * 4];
        Vector3[] normals = new Vector3[cellCount * 4];
        int[] triangles = new int[cellCount * 6];
        Vector2[] unitUvs =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };

        for (int chunkCell = 0; chunkCell < cellCount; chunkCell++)
        {
            int cellIndex = firstCell + chunkCell;
            int cellX = minimumCellX + cellIndex % columns;
            int cellY = minimumCellY + cellIndex / columns;
            float left = cellX * cellSize;
            float bottom = cellY * cellSize;
            int vertexIndex = chunkCell * 4;
            int triangleIndex = chunkCell * 6;

            vertices[vertexIndex] = transform.InverseTransformPoint(
                new Vector3(left, bottom, zPosition));
            vertices[vertexIndex + 1] = transform.InverseTransformPoint(
                new Vector3(left + cellSize, bottom, zPosition));
            vertices[vertexIndex + 2] = transform.InverseTransformPoint(
                new Vector3(left + cellSize, bottom + cellSize, zPosition));
            vertices[vertexIndex + 3] = transform.InverseTransformPoint(
                new Vector3(left, bottom + cellSize, zPosition));

            int rotation = random.Next(4);
            bool flipX = random.Next(2) == 0;
            bool flipY = random.Next(2) == 0;
            for (int uvIndex = 0; uvIndex < unitUvs.Length; uvIndex++)
            {
                Vector2 uv = unitUvs[uvIndex];
                if (flipX)
                {
                    uv.x = 1f - uv.x;
                }

                if (flipY)
                {
                    uv.y = 1f - uv.y;
                }

                for (int quarterTurn = 0; quarterTurn < rotation; quarterTurn++)
                {
                    uv = new Vector2(uv.y, 1f - uv.x);
                }

                uvs[vertexIndex + uvIndex] = new Vector2(
                    spriteUvRect.xMin + uv.x * spriteUvRect.width,
                    spriteUvRect.yMin + uv.y * spriteUvRect.height);
                normals[vertexIndex + uvIndex] = Vector3.back;
            }

            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = vertexIndex + 2;
            triangles[triangleIndex + 2] = vertexIndex + 1;
            triangles[triangleIndex + 3] = vertexIndex;
            triangles[triangleIndex + 4] = vertexIndex + 3;
            triangles[triangleIndex + 5] = vertexIndex + 2;
        }

        Mesh mesh = new Mesh
        {
            name = "Map Cell Background Mesh"
        };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        generatedMeshes.Add(mesh);

        GameObject meshObject = new GameObject("Map Cell Background Chunk");
        meshObject.layer = gameObject.layer;
        meshObject.transform.SetParent(transform, false);
        MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;
        MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = generatedMaterial;
        meshRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        meshRenderer.sortingOrder = sourceRenderer.sortingOrder;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in generatedMeshes)
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }

        if (generatedMaterial != null)
        {
            Destroy(generatedMaterial);
        }
    }
}