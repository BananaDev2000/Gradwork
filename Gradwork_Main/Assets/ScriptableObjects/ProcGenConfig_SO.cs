using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UIElements;


[System.Serializable]
public class BiomeConfig
{
    public BiomeConfig_SO Biome;
    [Range(0f, 1f)] public float Weighting = 1f;
}

[CreateAssetMenu(fileName = "ProcGen Config", menuName = "Procedural Generation/ProcGen Configuration", order = -1)]
public class ProcGenConfig_SO : ScriptableObject
{
    public List<BiomeConfig> Biomes;
    [Range(0f, 1f)] public float BiomeSeedPointDensity = 0.1f;

    public int NumBiomes => Biomes.Count;

    public float TotalWeighthing
    {
        get
        {
            float sum = 0f;

            foreach (var config in Biomes) 
            {
            sum += config.Weighting;
            }

            return sum;
        }
    }
}
