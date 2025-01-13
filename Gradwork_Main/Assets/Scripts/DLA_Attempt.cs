using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System;
using Random = UnityEngine.Random;

public class DLA_Attempt : MonoBehaviour
{
    public int width = 256;
    public int height = 256;
    public int walkerCount = 1000;
    public int maxSteps = 10000;
    public int maxJiggle = 2;

    public Terrain terrain;

    public Texture2D heightmapTexture;

    private float[,] mainHeightMap;
    private float[,] blurredHeightmap;
    float[,] boneHeightmap;
    float[,] combinedHeightmap;
    private float[,] updateMap;

    private int walkCounter = 0;
    private int cycleCounter = 0;

    [SerializeField] private int cycleAmount = 2;

    private Dictionary<Vector2Int, List<Vector2Int>> connections;

    private int amountGenerated;

    Vector2Int[] Offset = new Vector2Int[]
    {
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0)
    };

    private void Setup()
    {
        mainHeightMap = new float[width, height];
        connections = new Dictionary<Vector2Int, List<Vector2Int>>();

        int centerX = width / 2;
        int centerY = height / 2;
        Vector2Int center = new Vector2Int(centerX, centerY);
        mainHeightMap[centerX, centerY] = 1;
        connections[center] = new List<Vector2Int>();
    }

    void Start()
    {
        cycleAmount--;
        int cycleAt = 0;
        Setup();
        GenerateInitialHeightmap();
        blurredHeightmap = GenerateUpscaledBlurredHeightmap(mainHeightMap, 2);
        SaveHeightmapAsImage(blurredHeightmap, "BlurredHeightmap0.png");
        UpscaleConnections(2);
        boneHeightmap = GenerateBoneStructure(mainHeightMap, 2);
        SaveHeightmapAsImage(boneHeightmap, "BoneHeightmap0.png");
        UpdateCrispMap(boneHeightmap);
        SaveHeightmapAsImage(boneHeightmap, "CrispImage0.png");
        combinedHeightmap = CombineMaps(blurredHeightmap, boneHeightmap);
        SaveHeightmapAsImage(combinedHeightmap, "CombinedHeightmap0.png");



        while (cycleAt < cycleAmount)
        {
            Cycle(cycleAt);
            cycleAt++;
        }
        SaveHeightmapAsImage(combinedHeightmap, "CombinedHeightmapFinal.png");
        ApplyHeightmapToTerrain(terrain, combinedHeightmap);
        SaveHeightmapAsImage(NormalizeHeightmap(combinedHeightmap), "CombinedHeightmapFinal_Normalized.png");
        //SaveHeightmapAsImage(SmoothHeightmap(combinedHeightmap), "CombinedHeightmapFinal_Smooth.png");
       // ApplyHeightmapToTerrain(terrain, combinedHeightmap);
    }

    private float[,] SmoothHeightmap(float[,] heightmap, int iterations = 2)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        float[,] smoothedMap = new float[width, height];

        for (int iter = 0; iter < iterations; iter++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    // Average surrounding pixels
                    float sum = 0;
                    sum += heightmap[x, y];
                    sum += heightmap[x - 1, y];
                    sum += heightmap[x + 1, y];
                    sum += heightmap[x, y - 1];
                    sum += heightmap[x, y + 1];
                    smoothedMap[x, y] = sum / 5f;
                }
            }
            // Copy back
            Array.Copy(smoothedMap, heightmap, width * height);
        }

        return heightmap;
    }


    void UpscaleConnections(int scaleFactor)
    {
        var newConnections = new Dictionary<Vector2Int, List<Vector2Int>>();

        foreach (var pair in connections)
        {
            Vector2Int newStart = pair.Key * scaleFactor;

            if (!newConnections.ContainsKey(newStart))
            {
                newConnections[newStart] = new List<Vector2Int>();
            }

            foreach (var end in pair.Value)
            {
                Vector2Int newEnd = end * scaleFactor;

                if (!newConnections.ContainsKey(newEnd))
                {
                    newConnections[newEnd] = new List<Vector2Int>();
                }

                // Preserve connectivity
                newConnections[newStart].Add(newEnd);
                newConnections[newEnd].Add(newStart);
            }
        }

        connections = newConnections;
    }

    private void MoveWalkerTowardsCenter(ref int x, ref int y)
    {
        // Calculate center point
        int centerX = width / 2;
        int centerY = height / 2;

        // Determine movement probabilities
        float[] weights = new float[4]; // Up, Down, Left, Right

        if (x < centerX) // Walker is to the left of the center
        {
            weights[2] = 15; // Left (away)
            weights[3] = 35; // Right (towards center)
        }
        else // Walker is to the right
        {
            weights[2] = 35; // Left (towards center)
            weights[3] = 15; // Right (away)
        }

        if (y < centerY) // Walker is below the center
        {
            weights[0] = 35; // Up (towards center)
            weights[1] = 15; // Down (away)
        }
        else // Walker is above
        {
            weights[0] = 15; // Up (away)
            weights[1] = 35; // Down (towards center)
        }

        float totalWeight = weights[0] + weights[1] + weights[2] + weights[3];
        for (int i = 0; i < 4; i++)
        {
            weights[i] /= totalWeight;
        }

        float randomValue = Random.Range(0f, 1f);
        float cumulative = 0;

        for (int i = 0; i < 4; i++)
        {
            cumulative += weights[i];
            if (randomValue <= cumulative)
            {
                switch (i)
                {
                    case 0: y = Mathf.Clamp(y + 1, 0, height - 1); break;
                    case 1: y = Mathf.Clamp(y - 1, 0, height - 1); break;
                    case 2: x = Mathf.Clamp(x - 1, 0, width - 1); break;
                    case 3: x = Mathf.Clamp(x + 1, 0, width - 1); break; 
                }
                return;
            }
        }
    }


    float[,] NormalizeHeightmap(float[,] heightmap)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);

        float minVal = float.MaxValue;
        float maxVal = float.MinValue;

        // Find min and max values in the heightmap
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float value = heightmap[x, y];
                if (value < minVal) minVal = value;
                if (value > maxVal) maxVal = value;
            }
        }

        // Avoid division by zero if the heightmap is flat
        if (maxVal - minVal == 0)
            return heightmap;

        // Normalize the heightmap to 0-1 range
        float[,] normalizedMap = new float[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                normalizedMap[x, y] = (heightmap[x, y] - minVal) / (maxVal - minVal);
            }
        }

        return normalizedMap;
    }



    private void Cycle(int cycleNr)
    {
        Debug.Log("Started Cycle");
        width = width * 2;
        height = height * 2;
       // ApplyWeightedHeightmap(boneHeightmap);
        blurredHeightmap = GenerateUpscaledBlurredHeightmap(combinedHeightmap, 2);
        SaveHeightmapAsImage(blurredHeightmap, "BlurredHeightmap" + (cycleNr + 1) + ".png");
        UpscaleConnections(2);
        boneHeightmap = GenerateBoneStructure(boneHeightmap, 2);
        SaveHeightmapAsImage(boneHeightmap, "BoneHeightmap" + (cycleNr + 1) + ".png");
        UpdateCrispMap(boneHeightmap);
        SaveHeightmapAsImage(boneHeightmap, "CrispImage" + (cycleNr + 1) + ".png");
        combinedHeightmap = CombineMaps(blurredHeightmap, boneHeightmap);
        SaveHeightmapAsImage(combinedHeightmap, "CombinedHeightmap" + (cycleNr + 1) + ".png");
        Debug.Log("Completed Cycle");
        // connections = new Dictionary<Vector2Int, List<Vector2Int>>();
    }

    void GenerateInitialHeightmap()
    {
        amountGenerated = 0;
        while (amountGenerated < walkerCount)
        {
            InitialWalk();
        }
        SaveHeightmapAsImage(mainHeightMap, "InitialHeightMap.png");
        walkerCount = walkerCount * 2;
    }


    private void InitialWalk()
    {
        int x = 0, y = 0;
        int edge = Random.Range(0, 4);

        switch (edge)
        {
            case 0: x = Random.Range(0, width); y = height - 1; break;
            case 1: x = Random.Range(0, width); y = 0; break;
            case 2: x = 0; y = Random.Range(0, height); break;
            case 3: x = width - 1; y = Random.Range(0, height); break;
        }

        for (int step = 0; step < maxSteps; step++)
        {
            if (IsAdjacentToStructure(x, y, out Vector2Int connectedTo, mainHeightMap))
            {
                Vector2Int current = new Vector2Int(x, y);
                mainHeightMap[x, y] += 1;

                if (!connections.ContainsKey(current))
                {
                    connections[current] = new List<Vector2Int>();
                }

                if (!connections.ContainsKey(connectedTo))
                {
                    connections[connectedTo] = new List<Vector2Int>();
                }

                connections[current].Add(connectedTo);
                connections[connectedTo].Add(current);

                amountGenerated++;
                return;
            }

            MoveWalkerTowardsCenter(ref x, ref y);

        }
    }


    void UpdateCrispMap(float[,] useHeightMap)
    {
        amountGenerated = 0;
        SaveHeightmapAsImage(useHeightMap, "CrispImage_Before.png");
        while (amountGenerated < walkerCount)
        {
            CrispWalk(useHeightMap);
        }
        SaveHeightmapAsImage(useHeightMap, "CrispImage_After.png");
    }

    private void CrispWalk(float[,] useHeightMap)
    {

        int correctSize = Convert.ToInt32(Mathf.Sqrt(useHeightMap.Length));
        int x = 0, y = 0;

        int edge = Random.Range(0, 4);

        switch (edge)
        {
            case 0: x = Random.Range(0, correctSize); y = correctSize - 1; break;
            case 1: x = Random.Range(0, correctSize); y = 0; break;
            case 2: x = 0; y = Random.Range(0, correctSize); break;
            case 3: x = correctSize - 1; y = Random.Range(0, correctSize); break;
        }

        for (int step = 0; step < maxSteps; step++)
        {
            Debug.Log(x + " " + y);
            if (IsAdjacentToStructure(x, y, out Vector2Int connectedTo, useHeightMap))
            {
                walkCounter++;
                amountGenerated++;
                Vector2Int current = new Vector2Int(x, y);

                useHeightMap[x, y] = 1;
                if (!connections.ContainsKey(current))
                {
                    connections[current] = new List<Vector2Int>();
                }

                if (!connections.ContainsKey(connectedTo))
                {
                    connections[connectedTo] = new List<Vector2Int>();
                }

                connections[current].Add(connectedTo);
                connections[connectedTo].Add(current);


                return;
            }

            MoveWalkerTowardsCenter(ref x, ref y);

        }
    }


    bool IsAdjacentToStructure(int x, int y, out Vector2Int connectedTo, float[,] useHeightMap)
    {
        //SaveHeightmapAsImage(useHeightMap,"Yey.png");
        int correctSize = Convert.ToInt32(Mathf.Sqrt(useHeightMap.Length));
        foreach (Vector2Int pos in Offset)
        {
            int nx = Mathf.Clamp(x + pos.x, 0, correctSize - 1);
            int ny = Mathf.Clamp(y + pos.y, 0, correctSize - 1);
            if (useHeightMap[nx, ny] > 0)
            {
                connectedTo = new Vector2Int(nx, ny);
                return true;
            }
        }
        connectedTo = Vector2Int.zero;
        return false;
    }

    void ApplyHeightmapToTexture()
    {
        heightmapTexture = new Texture2D(width, height);
        float maxHeight = 0;

        foreach (float value in mainHeightMap)
        {
            if (value > maxHeight) maxHeight = value;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float normalizedHeight = mainHeightMap[x, y] / maxHeight;
                heightmapTexture.SetPixel(x, y, new Color(normalizedHeight, normalizedHeight, normalizedHeight));
            }
        }

        heightmapTexture.Apply();
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.mainTexture = heightmapTexture;
        }
    }

    void ApplyWeightedHeightmap(float[,] heightmap)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        float[,] weightMap = new float[width, height];

        int centerX = width / 2;
        int centerY = height / 2;
        float maxDistance = Mathf.Max(centerX, centerY); // Max distance from the center

        // Step 1: Assign weights based on distance from center
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float distance = Mathf.Sqrt(Mathf.Pow(x - centerX, 2) + Mathf.Pow(y - centerY, 2));
                float weight = (maxDistance - distance) / maxDistance; // Normalize 0-1 range
                weightMap[x, y] = 1 - (1 / (1 + weight * 10)); // Apply falloff formula
            }
        }

        // Step 2: Apply weight to heightmap
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                heightmap[x, y] *= weightMap[x, y]; // Multiply original heights by weight
            }
        }
    }


    float[,] GenerateUpscaledBlurredHeightmap(float[,] originalHeightmap, int scaleFactor)
{
    int oldWidth = originalHeightmap.GetLength(0);
    int oldHeight = originalHeightmap.GetLength(1);
    int newWidth = oldWidth * scaleFactor;
    int newHeight = oldHeight * scaleFactor;

    float[,] upscaledHeightmap = new float[newWidth, newHeight];

    for (int x = 0; x < newWidth; x++)
    {
        for (int y = 0; y < newHeight; y++)
        {

            float gx = (float)x / (float)(newWidth - 1) * (oldWidth - 1);
            float gy = (float)y / (float)(newHeight - 1) * (oldHeight - 1);

            int x0 = Mathf.FloorToInt(gx);
            int x1 = Mathf.Min(x0 + 1, oldWidth - 1);
            int y0 = Mathf.FloorToInt(gy);
            int y1 = Mathf.Min(y0 + 1, oldHeight - 1);

            float tx = gx - x0;
            float ty = gy - y0;
    
            float a = Mathf.Lerp(originalHeightmap[x0, y0], originalHeightmap[x1, y0], tx);
            float b = Mathf.Lerp(originalHeightmap[x0, y1], originalHeightmap[x1, y1], tx);
            upscaledHeightmap[x, y] = Mathf.Lerp(a, b, ty);
        }
    }

    return upscaledHeightmap;
}


    float[,] AdaptiveBlur(float[,] heightmap, int iterations = 2)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        float[,] result = new float[width, height];

        for (int iter = 0; iter < iterations; iter++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    float heightVal = heightmap[x, y];

                    // Compute local variance (sharpness of terrain)
                    float variance = Mathf.Abs(heightmap[x + 1, y] - heightVal) +
                                     Mathf.Abs(heightmap[x - 1, y] - heightVal) +
                                     Mathf.Abs(heightmap[x, y + 1] - heightVal) +
                                     Mathf.Abs(heightmap[x, y - 1] - heightVal);

                    // Apply stronger blur if variance is high
                    float blurFactor = Mathf.Clamp01(1.0f - variance * 5.0f);

                    result[x, y] = (heightmap[x, y] * (1 - blurFactor)) +
                                   ((heightmap[x - 1, y] + heightmap[x + 1, y] +
                                     heightmap[x, y - 1] + heightmap[x, y + 1]) / 4) * blurFactor;
                }
            }
        }

        return result;
    }


    float[,] GenerateBoneStructure(float[,] originalHeightmap, int scaleFactor)
    {
        int newWidth = originalHeightmap.GetLength(0) * scaleFactor;
        int newHeight = originalHeightmap.GetLength(1) * scaleFactor;
        float[,] upscaledHeightmap = new float[newWidth, newHeight];

        foreach (var pair in connections)
        {
            if (pair.Value == null) continue;

            Vector2Int start = pair.Key;
            foreach (Vector2Int end in pair.Value)
            {
                Vector2Int scaledEnd = end;

                Vector2Int midPoint = (start + scaledEnd) / 2;

                // Jiggle midpoint slightly, constrained by maxJiggle
                int jiggleX = Random.Range(-maxJiggle, maxJiggle + 1);
                int jiggleY = Random.Range(-maxJiggle, maxJiggle + 1);

                // Apply jiggling to midpoint
                midPoint.x = Mathf.Clamp(midPoint.x + jiggleX, Mathf.Min(start.x, scaledEnd.x), Mathf.Max(start.x, scaledEnd.x));
                midPoint.y = Mathf.Clamp(midPoint.y + jiggleY, Mathf.Min(start.y, scaledEnd.y), Mathf.Max(start.y, scaledEnd.y));

                // Draw the lines with jiggled midpoint
                DrawLine(upscaledHeightmap, start, midPoint);
                DrawLine(upscaledHeightmap, midPoint, scaledEnd);
            }
        }

        return upscaledHeightmap;
    }


    float[,] CombineMaps(float[,] blurredMap, float[,] crispMap)
    {
        int width = blurredMap.GetLength(0);
        int height = blurredMap.GetLength(1);
        float[,] combinedMap = new float[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                combinedMap[x, y] = Mathf.Clamp01(blurredMap[x, y] + crispMap[x, y]);
            }
        }

        return combinedMap;
    }

    void DrawLine(float[,] heightmap, Vector2Int start, Vector2Int end)
    {
        int dx = Mathf.Abs(end.x - start.x);
        int dy = Mathf.Abs(end.y - start.y);
        int sx = start.x < end.x ? 1 : -1;
        int sy = start.y < end.y ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            heightmap[start.x, start.y] = 1;

            if (start == end) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                start.x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                start.y += sy;
            }
        }
    }

    void SaveHeightmapAsImage(float[,] heightmap, string filename)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        Texture2D texture = new Texture2D(width, height);

        float maxHeight = 0;
        foreach (float value in heightmap)
        {
            if (value > maxHeight) maxHeight = value;
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float normalizedHeight = heightmap[x, y] / maxHeight;
                texture.SetPixel(x, y, new Color(normalizedHeight, normalizedHeight, normalizedHeight));
            }
        }

        texture.Apply();
        byte[] bytes = texture.EncodeToPNG();
        File.WriteAllBytes(Application.dataPath + "/" + filename, bytes);
        Debug.Log("Saved heightmap as " + filename);
    }

    private void ApplyHeightmapToTerrain(Terrain terrain, float[,] heightmap)
    {
        TerrainData terrainData = terrain.terrainData;

        int terrainWidth = terrainData.heightmapResolution;
        int terrainHeight = terrainData.heightmapResolution;

        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);

        // Unity requires a 2D float array [y, x] (not [x, y]!)
        float[,] unityHeightmap = new float[terrainHeight, terrainWidth];

        // Resize the heightmap to fit the terrain resolution
        for (int x = 0; x < terrainWidth; x++)
        {
            for (int y = 0; y < terrainHeight; y++)
            {
                // Scale indices
                int sourceX = Mathf.Clamp(Mathf.RoundToInt((x / (float)terrainWidth) * width), 0, width - 1);
                int sourceY = Mathf.Clamp(Mathf.RoundToInt((y / (float)terrainHeight) * height), 0, height - 1);

                unityHeightmap[y, x] = heightmap[sourceX, sourceY]; // Swap x and y
            }
        }

        // Apply to Unity Terrain
        terrainData.SetHeights(0, 0, unityHeightmap);
    }

}