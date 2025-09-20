using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControllDissolve : MonoBehaviour
{
    public bool useSlider;
    private Vector2 noise1Vec2;
    [Range(-1.0f, 1.0f)] 
    public float noise1Vec2x;
    [Range(-1.0f, 1.0f)] 
    public float noise1Vec2y;
    private Vector2 noise2Vec2;
    [Range(-1.0f, 1.0f)] 
    public float noise2Vec2x;
    [Range(-1.0f, 1.0f)] 
    public float noise2Vec2y;
    [Range(0f, 30.0f)] 
    public float seqForFractionf;
    private string simplenoise1Vec2 = "_simplenoise1Vec2";
    private string simplenoise2Vec2 = "_simplenoise2Vec2";
    private string seqForFraction = "_seqForFraction";
    
    private Material material;

    // Start is called before the first frame update
    void Start()
    {
        noise1Vec2 = new Vector2(noise1Vec2x, noise1Vec2y);
        noise2Vec2 = new Vector2(noise2Vec2x, noise2Vec2y);
        material = GetComponent<Renderer>().material;
        material.SetVector(simplenoise1Vec2, noise1Vec2);
        material.SetVector(simplenoise2Vec2, noise2Vec2);
        material.SetFloat(seqForFraction, seqForFractionf);
    }

    // Update is called once per frame
    void Update()
    {
        if (useSlider)
        {
            noise1Vec2 = new Vector2(noise1Vec2x, noise1Vec2y);
            noise2Vec2 = new Vector2(noise2Vec2x, noise2Vec2y);
            material.SetVector(simplenoise1Vec2, noise1Vec2);
            material.SetVector(simplenoise2Vec2, noise2Vec2);
            material.SetFloat(seqForFraction, seqForFractionf);
        }
        else
        {
            noise1Vec2 = new Vector2(Mathf.Sin(Time.time * 0.5f), Mathf.Cos(Time.time * 0.5f));
            noise2Vec2 = new Vector2(Mathf.Cos(Time.time * 0.5f), Mathf.Sin(Time.time * 0.5f));
            seqForFractionf = (Mathf.Sin(Time.time * 0.5f)+1)/2+0.2f; // Normalized to [0, 1]
            //Debug.Log(noise1Vec2);
            material.SetVector(simplenoise1Vec2, noise1Vec2);
            material.SetVector(simplenoise2Vec2, noise2Vec2);
            //material.SetFloat(seqForFraction, seqForFractionf);
        }
        
    }
}
