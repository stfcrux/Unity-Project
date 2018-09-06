﻿using System.Collections.Generic;
using UnityEngine;

public class CreateMountainPlane : MonoBehaviour
{

    public float sideLength;
    public float roughness;
    public float initialHeightRange;
    public int nIterations;
    public float cornerRandomRange;
    public bool standardizePlaneHeight;
    public float grassStartingHeight;
    public float rockStartingHeight;
    public float snowStartHeight;
    public PointLight pointLight;

    private Color MOUNTAIN_GRASS =
        new Color(187.0f / 255, 217.0f / 255, 95.0f / 255, 1.0f);
    private Color MOUNTAIN_SNOW =
        new Color(230.0f / 255, 240.0f / 255, 239.0f / 255, 1.0f);
    private Color MOUNTAIN_ROCK =
        new Color(110.0f / 255, 117.0f / 255, 146.0f / 255, 1.0f);
    private Color MOUNTAIN_SAND =
        new Color(240.0f / 255, 222.0f / 255, 180.0f / 255, 1.0f);

    private Vector3[] vertices;
    private int[] triangles;


    void Start()
    {
        // generate height maps
        float[,] ys = DiamondSquare(nIterations);
        if (standardizePlaneHeight) {
            // get avg
            float tot = 0;
            for (int x = 0; x < ys.GetLength(0); x++)
            {
                for (int z = 0; z < ys.GetLength(0); z++)
                {
                    tot += ys[x, z];
                }
            }

            float avg = tot / ys.Length;
            print(avg);
            for (int x = 0; x < ys.GetLength(0); x++)
            {
                for (int z = 0; z < ys.GetLength(0); z++)
                {
                    ys[x, z] -= avg;
                }
            }
        }

        // generate triangle verticies from the heightmap
        SetTriangles(ys);

        // create the mesh
        MeshFilter mountainMesh = this.gameObject.AddComponent<MeshFilter>();
        mountainMesh.mesh = this.CreateMountainMesh();

        // set the shader on the game object
        MeshRenderer meshRenderer = this.gameObject.AddComponent<MeshRenderer>();

        meshRenderer.material.shader =
                Shader.Find("Unlit/PhongShader");
                
        // Add MeshCollider for mesh object to avoid camera object
        gameObject.AddComponent<MeshCollider>();
        
    }

    // Called each frame
    void Update()
    {

        // Update point light (sun)
        pointLight.Update();

        // Get renderer component (in order to pass params to shader)
        MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();

        // Pass updated light positions to shader
        renderer.material.SetColor("_PointLightColor", this.pointLight.color);
        renderer.material.SetVector("_PointLightPosition", this.pointLight.GetWorldPosition());
    }

    private Mesh CreateMountainMesh()
    {
        Mesh m = new Mesh
        {
            name = "Mountain",
            vertices = vertices
        };

        Color[] colors = new Color[m.vertices.Length];

        // assign the triangles and the color for each vertex
        for (int i = 0; i < m.vertices.Length; i++)
        {
            float height = vertices[i].y;
            if (height < grassStartingHeight)
            {
                colors[i] = MOUNTAIN_SAND;
            }
            else if (height < rockStartingHeight)
            {
                colors[i] = MOUNTAIN_GRASS;
            }
            else if (height < snowStartHeight)
            {
                colors[i] = MOUNTAIN_ROCK;
            }
            else
            {
                colors[i] = MOUNTAIN_SNOW;
            }

        }

        // copy the traingles and colors to the mesh
        m.triangles = this.triangles;
        m.colors = colors;
        m.RecalculateNormals();

        return m;
    }

    private void SetTriangles(float[,] ys)
    {
        int ss = ys.GetLength(0);
        vertices = new Vector3[ss * ss];
        triangles = new int[(ys.Length - ss - 1) * 2 * 3];

        // determine the x and z increment using the square sidelength
        float increment = sideLength / (ys.GetLength(0) - 1);

        int i = 0;
        for (int x = 0; x < ys.GetLength(0); x++)
        {
            for (int z = 0; z < ys.GetLength(0); z++)
            {
                vertices[i++] = new Vector3(x * increment, ys[x, z], z * increment);
            }
        }

        // gen left triangles
        i = 0;
        for (int j = 0; j < ys.Length - ss - 1; j++)
        {
            if (j % ss == ss - 1)
            {
                continue;
            }
            triangles[i++] = j;
            triangles[i++] = j + 1;
            triangles[i++] = j + ss;
        }
        // gen right triangles
        for (int j = 1; j < ys.Length - ss; j++)
        {
            if (j % ss == 0)
            {
                continue;
            }
            triangles[i++] = j;
            triangles[i++] = j + ss;
            triangles[i++] = j + ss - 1;
        }
    }

    private float[,] DiamondSquare(int iterations)
    {
        int maxIndex = Power(2, iterations);
        float[,] ys = new float[maxIndex + 1, maxIndex + 1];

        float range = initialHeightRange;

        // generate corners randomnly
        ys[0, 0] = Random.Range(-cornerRandomRange, cornerRandomRange);
        ys[maxIndex, 0] = Random.Range(-cornerRandomRange, cornerRandomRange);
        ys[0, maxIndex] = Random.Range(-cornerRandomRange, cornerRandomRange);
        ys[maxIndex, maxIndex] = Random.Range(-cornerRandomRange, cornerRandomRange);

        for (int currSize = maxIndex; currSize > 1; currSize /= 2)
        {

            int half = currSize / 2;

            // diamond step
            for (int x = half; x < maxIndex; x += currSize)
            {
                for (int z = half; z < maxIndex; z += currSize)
                {
                    ys[x, z] = Diamond(x, z, half, ys, range);
                }
            }

            // square step
            int startz = half;
            for (int x = 0; x <= maxIndex; x += half)
            {
                for (int z = startz; z <= maxIndex; z += currSize)
                {
                    ys[x, z] = Square(x, z, half, ys, range);
                }

                startz = (startz + half) % currSize;
            }

            // reduce the range at each iteration to stop the surface from
            // looking jaggad
            range = (range / 2) * roughness;
        }

        return ys;
    }



    float Square(int x, int z, int half, float[,] ys, float range)
    {
        List<float> relevantYs = new List<float>();
        if (z - half >= 0)
        {
            // append north
            relevantYs.Add(ys[x, z - half]);
        }
        if (z + half < ys.GetLength(0))
        {
            // append south
            relevantYs.Add(ys[x, z + half]);
        }
        if (x - half >= 0)
        {
            // append west
            relevantYs.Add(ys[x - half, z]);
        }
        if (x + half < ys.GetLength(0))
        {
            // append east
            relevantYs.Add(ys[x + half, z]);
        }
        return AvgRandom(relevantYs, range);
    }

    float Diamond(int x, int z, int half, float[,] ys, float range)
    {
        List<float> relevantYs = new List<float>
        {
            // add NW
            ys[x - half, z - half],
            // add NE
            ys[x + half, z - half],
            // add SW
            ys[x - half, z + half],
            // add SE
            ys[x + half, z + half]
        };
        return AvgRandom(relevantYs, range);
    }

    float AvgRandom(List<float> inputs, float range)
    {
        float tot = 0.0f;
        foreach (float input in inputs)
        {
            tot += input;
        }
        return tot / inputs.Count + Random.Range(-range, range);
    }

    int Power(int b, int e)
    {
        int res = 1;
        for (int i = 0; i < e; i++)
        {
            res *= b;
        }
        return res;
    }
}

