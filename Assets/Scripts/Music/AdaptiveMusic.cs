using System;
using UnityEngine;

public class AdaptiveMusic : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] world1Layers;
    [SerializeField] private AudioClip[] world2Layers;

    private AudioSource[] sources;
    private bool isWorld1 = true;
    private float _scale = 0;
    private float numLayers;

    private void Start()
    {
        int numLayers = isWorld1 ? world1Layers.Length : world2Layers.Length;
        
        for (int i = 0; i < numLayers; ++i)
        {
            sources[i] = new AudioSource();
            sources[i].clip = isWorld1 ? world1Layers[i] : world2Layers[i];
            sources[i].loop = true;
            sources[i].Play();
            sources[i].volume = 0f;
        }
    }

    public void updateScale(float scale)
    {
        if (scale == _scale) return;

        float intensityLayers = scale / numLayers;

        for (int i = 0; i < intensityLayers; ++i)
        {
            
        }
        
        _scale = scale;
        
    }
}
