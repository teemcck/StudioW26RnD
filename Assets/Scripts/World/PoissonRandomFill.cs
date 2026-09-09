using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Poisson enemy fill algorithm: 
/// https://www.cs.ubc.ca/~rbridson/docs/bridson-siggraph07-poissondisk.pdf
/// </summary>
public static class Poisson 
{
    private static bool IsValid(List<Vector2> samples, int[,] grid, Vector2 sample, Vector2 sampleZone, float radius, float cellSize)
    {
        if(sample.x < sampleZone.x && sample.x >= 0 && sample.y < sampleZone.y && sample.y >= 0)
        {
            int x = (int)(sample.x / cellSize);
            int y = (int)(sample.y / cellSize);
            int offsetX = Mathf.Max(0, x - 2);
            int outX = Mathf.Min(x + 2, grid.GetLength(0) - 1);
            int offsetY = Mathf.Max(0, y - 2);
            int outY = Mathf.Min(y + 2, grid.GetLength(1) - 1);

            for (int i = offsetX; i < outX; i++)
            {
                for (int j = offsetY; j < outY; j++)
                {
                    int sampleIndex = grid[i, j] - 1;
                    if(sampleIndex != -1)
                    {
                        float sqrDistance = (sample - samples[sampleIndex]).sqrMagnitude;
                        if(sqrDistance < radius*radius) 
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }
        return false;
    } 
    public static List<Vector2> GeneratePoint(float radius, Vector2 gridSize, int k  = 30)
    {
        float cellSize = radius / Mathf.Sqrt(2);

        //to get the columns we gonna divide the width/ cellSize and rows ....
        int[,] grid = new int[Mathf.CeilToInt(gridSize.x / cellSize), Mathf.CeilToInt(gridSize.y / cellSize)];
        
        List<Vector2> samples = new List<Vector2>();
        List<Vector2> spawnSamples = new List<Vector2>();

        spawnSamples.Add(gridSize / 2);
        while (spawnSamples.Count > 0)
        {
            //int index = Random.Range(0, spawnSamples.Count);
            int index = spawnSamples.Count - 1;
            Vector2 currentSpawnSample = spawnSamples[index];
            bool rejectedSample = true;
            for (int i = 0; i < k; i++)
            {
                float angleOffset = Random.value * Mathf.PI * 2;
                //rotate a vector at a given angle
                float x = Mathf.Sin(angleOffset);
                float y = Mathf.Cos(angleOffset);
                Vector2 offsetDirection = new Vector2(x, y);
                
                float newMagnitude = Random.Range(radius, 2 * radius);
                offsetDirection *= newMagnitude;

                Vector2 sample = currentSpawnSample + offsetDirection;
                if (IsValid(samples, grid, sample, gridSize, radius, cellSize))
                {
                    samples.Add(sample);
                    spawnSamples.Add(sample);
                    grid[(int)(sample.x / cellSize), (int)(sample.y / cellSize)] = samples.Count;
                    rejectedSample = false;
                    break;
                }
            }

            if (rejectedSample)
            {
                spawnSamples.RemoveAt(index);
            }
        }
        return samples;
    }

}