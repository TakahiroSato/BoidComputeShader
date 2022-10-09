using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Random = Unity.Mathematics.Random;

[RequireComponent(typeof(VisualEffect))]
public class Boid : MonoBehaviour
{
    // [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    // public struct BoidState
    // {
    //     public Vector3 Position;
    //     public Vector3 Forward;
    //     public Vector3 Color;
    // }
    
    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct BoidState
    {
        public Vector3 Position;
        public Vector3 Forward;
        public Vector3 Color;
        public Vector3 Angle;
        public float Size;
    }

    [Serializable]
    public class BoidConfig
    {
        public float moveSpeed = 1f;

        [Range(0f, 1f)] public float separationWeight = .5f;

        [Range(0f, 1f)] public float alignmentWeight = .5f;

        [Range(0f, 1f)] public float targetWeight = .5f;

        public Transform boidTarget;
    }

    public int boidCount = 32;

    public float3 boidExtent = new(32f, 32f, 32f);

    public ComputeShader BoidComputeShader;

    public BoidConfig boidConfig;

    VisualEffect _boidVisualEffect;
    GraphicsBuffer _boidBuffer;
    int _kernelIndex;

    void OnEnable()
    {
        _boidBuffer = PopulateBoids(boidCount, boidExtent);
        _kernelIndex = BoidComputeShader.FindKernel("CSMain");
        BoidComputeShader.SetBuffer(_kernelIndex, "boidBuffer", _boidBuffer);
        BoidComputeShader.SetInt("numBoids", boidCount);

        _boidVisualEffect = GetComponent<VisualEffect>();
        _boidVisualEffect.SetGraphicsBuffer("Boids", _boidBuffer);
    }

    void OnDisable()
    {
        _boidBuffer?.Dispose();
    }

    void Update()
    {
        bool isTapped = Input.GetMouseButtonDown((0));
        Vector3 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        //Debug.Log(pos);
        
        UpdateBoids(pos);
    }

    void UpdateBoids(Vector3 pos)
    {
        var boidTarget = boidConfig.boidTarget != null
            ? boidConfig.boidTarget.position
            : transform.position;
        BoidComputeShader.SetFloat("deltaTime", Time.deltaTime);
        BoidComputeShader.SetFloat("separationWeight", boidConfig.separationWeight);
        BoidComputeShader.SetFloat("alignmentWeight", boidConfig.alignmentWeight);
        BoidComputeShader.SetFloat("targetWeight", boidConfig.targetWeight);
        BoidComputeShader.SetFloat("moveSpeed", boidConfig.moveSpeed);
        BoidComputeShader.SetVector("targetPosition", boidTarget);
        BoidComputeShader.SetVector("pos", pos);
        BoidComputeShader.GetKernelThreadGroupSizes(_kernelIndex, out var x, out var y, out var z);
        BoidComputeShader.Dispatch(_kernelIndex, (int) (boidCount / x), 1, 1);
    }

    public static GraphicsBuffer PopulateBoids(int boidCount, float3 boidExtent)
    {
        //var random = new Random(256);
        int c = (int)Math.Sqrt(boidCount);
        //Debug.Log(c);
        
        var boidArray =
            new NativeArray<BoidState>(boidCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        
        Debug.Log(boidArray.Length);
        for (int i = 0; i < c; i++)
        {
            for (int j = 0; j < c; j++)
            {
                boidArray[i*c+j] = new BoidState
                {
                    Position = new Vector3(0.125f * j, 0.125f * i, 0f),
                    Forward = new Vector3(0, 0, 30),
                    Angle = new Vector3(0, 0, 0),
                    Size = 0.1f,
                };
            }
        }

        var boidBuffer =
            new GraphicsBuffer(GraphicsBuffer.Target.Structured, boidArray.Length, Marshal.SizeOf<BoidState>());
        boidBuffer.SetData(boidArray);
        boidArray.Dispose();
        return boidBuffer;
    }
}