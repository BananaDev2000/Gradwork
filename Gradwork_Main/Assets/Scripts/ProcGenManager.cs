using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProcGenManager : MonoBehaviour
{
    [SerializeField]
    private ProcGenConfig_SO _procGenConfig;
    [SerializeField] private Terrain _targetTerrain;

#if UNITY_EDITOR
    byte[,] BiomeMap;
    float[,] BiomeStrengths;
#endif
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

#if UNITY_EDITOR
    public void RegenerateWorld()
    {
        // cache the map resolution
        int mapResolution = _targetTerrain.terrainData.heightmapResolution;

        Perform_BiomeGeneration(mapResolution);
    }

    private void Perform_BiomeGeneration(int mapResolution)
    {
        // allocate the biome map and strength map
        BiomeMap = new byte[mapResolution, mapResolution];
        BiomeStrengths = new float[mapResolution, mapResolution];

        // setup space for the seed points
        int numSeedPoints = Mathf.FloorToInt(mapResolution * mapResolution * _procGenConfig.BiomeSeedPointDensity);
        List<byte> biomesToSpawn = new List<byte>(numSeedPoints);

        // populate biomes to spawn based on weghthings
        float totalBiomeWeighthing = _procGenConfig.TotalWeighthing;
        for (int biomeIndex = 0; biomeIndex < _procGenConfig.NumBiomes; ++biomeIndex)
        {
            int numEntries = Mathf.RoundToInt(numSeedPoints * _procGenConfig.Biomes[biomeIndex].Weighting / totalBiomeWeighthing);
            Debug.Log("Will spawn: " + numEntries + " seedpoints for " + _procGenConfig.Biomes[biomeIndex].Biome.Name);

            for (int entryIndex = 0; entryIndex < numEntries; ++entryIndex)
            {
                biomesToSpawn.Add((byte)biomeIndex);
            }

        }
        // spawn individual biomes
        while (biomesToSpawn.Count > 0)
        {
            // pick a random seed point
            int seedPointIndex = Random.Range(0, biomesToSpawn.Count);
            // extract the biome index
            byte biomeIndex = biomesToSpawn[seedPointIndex];
            //remove seed point
            biomesToSpawn.RemoveAt(seedPointIndex);
            Perform_SpanwIndividualBiome(biomeIndex, mapResolution);
        }

        Texture2D biomeMap = new Texture2D(mapResolution, mapResolution, TextureFormat.RGB24, false);
        for (int y = 0; y < mapResolution; ++y)
        {
            for (int x = 0; x < mapResolution; ++x)
            {
                float hue = ((float)BiomeMap[x, y] / (float)_procGenConfig.NumBiomes);
                biomeMap.SetPixel(x, y, Color.HSVToRGB(hue, 0.75f,0.75f));
            }
        }
        biomeMap.Apply();
        System.IO.File.WriteAllBytes("BiomeMap.png", biomeMap.EncodeToPNG());
    }

    Vector2Int[] NeighbourOffsets = new Vector2Int[]
    {
        new Vector2Int(0,1),
        new Vector2Int(0,-1),
          new Vector2Int(1,0),
           new Vector2Int(-1,0),
            new Vector2Int(1,1),
             new Vector2Int(-1,-1),
          new Vector2Int(1,-1),
             new Vector2Int(-1,1)
    };

    // Use Ooze based generation, link here: procjam
    private void Perform_SpanwIndividualBiome(byte biomeIndex, int mapResolution)
    {
        //chache biome config
        BiomeConfig_SO biomeConfig = _procGenConfig.Biomes[biomeIndex].Biome;
        // Pick spawn location
        Vector2Int spawnLocation = new Vector2Int(Random.Range(0,mapResolution),Random.Range(0,mapResolution));

        // pick starting intensity
        float startIntensity = Random.Range(biomeConfig.MinIntensity,biomeConfig.MaxIntensity);

        //setup working list
        Queue<Vector2Int> workingList = new Queue<Vector2Int>();
        workingList.Enqueue(spawnLocation);
        //setup visited map and target intensity map
        bool[,] visited = new bool[mapResolution,mapResolution];
        float[,] targetIntensity = new float[mapResolution,mapResolution];

        //set target intensity
        targetIntensity[spawnLocation.x,spawnLocation.y] = startIntensity;

        // let the oozing begin
        while (workingList.Count > 0)
        {
            Vector2Int workingLocation = workingList.Dequeue();

            // set biome
            BiomeMap[workingLocation.x, workingLocation.y] = biomeIndex;
            visited[workingLocation.x,workingLocation.y] = true;
            BiomeStrengths[workingLocation.x,workingLocation.y] = targetIntensity[workingLocation.x,workingLocation.y];
            // traverse neighbours

            for (int neighbourIndex = 0; neighbourIndex < NeighbourOffsets.Length; ++neighbourIndex)
            {
                Vector2Int neighbourLocation = workingLocation + NeighbourOffsets[neighbourIndex];

                //skip if invalid
                if (neighbourLocation.x < 0 || neighbourLocation.y < 0 || neighbourLocation.x >= mapResolution || neighbourLocation.y >= mapResolution) continue;
                //skip if visited
                if (visited[neighbourLocation.x,neighbourLocation.y]) continue;

                // flag as visited
                visited[neighbourLocation.x, neighbourLocation.y] = true;

                //work out neighbour strength
                float decayAmount = Random.Range(biomeConfig.MinDecayRate,biomeConfig.MaxDecayRate) * NeighbourOffsets[neighbourIndex].magnitude;
                float neighbourStrength = targetIntensity[workingLocation.x,workingLocation.y] - decayAmount;
                targetIntensity[neighbourLocation.x,neighbourLocation.y] = neighbourStrength;

                //if strength to low, stop
                if (neighbourStrength <= 0) continue;

                //Random.Range(0f,1f) > neighbourStrength
                workingList.Enqueue(neighbourLocation);
            }
        }
    }
#endif
}
