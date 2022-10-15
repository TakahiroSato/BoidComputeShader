using System;
using System.Runtime.InteropServices;
using Audio;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Random = Unity.Mathematics.Random;

[RequireComponent(typeof(VisualEffect))]
public class Boid : MonoBehaviour
{
    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct BoidState
    {
        public Vector3 orgPosition;
        public Vector3 Position;
        public Vector3 Forward;
        public Vector3 Color;
        public Vector3 Angle;
        public float Size;
        public float rad;
    }

    public struct AudioState
    {
        public float spectram;
        public int frequency;
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

    [SerializeField] private AudioClipManager _audioClipManager;

    [SerializeField] private float _radius = 5;
    
    public int boidCount = 32;

    public float3 boidExtent = new(32f, 32f, 32f);

    public ComputeShader BoidComputeShader;

    public BoidConfig boidConfig;

    VisualEffect _boidVisualEffect;
    GraphicsBuffer _boidBuffer;
    private GraphicsBuffer _audioBuffer;
    int _kernelIndex;

    void OnEnable()
    {
        _boidBuffer = PopulateBoids(boidCount, boidExtent);
        _audioBuffer = PopulateAudioData();
        _kernelIndex = BoidComputeShader.FindKernel("CSMain");
        BoidComputeShader.SetBuffer(_kernelIndex, "boidBuffer", _boidBuffer);
        BoidComputeShader.SetBuffer(_kernelIndex, "audioBuffer", _audioBuffer);
        
        BoidComputeShader.SetInt("numBoids", boidCount);
        BoidComputeShader.SetInt("fftResolution", _audioClipManager.FFT_RESOLUTION);

        _boidVisualEffect = GetComponent<VisualEffect>();
        _boidVisualEffect.SetGraphicsBuffer("Boids", _boidBuffer);
    }

    void OnDisable()
    {
        _boidBuffer?.Dispose();
        _audioBuffer?.Dispose();
    }

    void Update()
    {
        bool isTapped = Input.GetMouseButtonDown((0));
        Vector3 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        //Debug.Log(pos);
        
        UpdateAudioBuffer();
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
        //BoidComputeShader.SetVector("pos", pos);
        BoidComputeShader.GetKernelThreadGroupSizes(_kernelIndex, out var x, out var y, out var z);
        BoidComputeShader.Dispatch(_kernelIndex, (int) (boidCount / x), 1, 1);
    }

    public GraphicsBuffer PopulateBoids(int boidCount, float3 boidExtent)
    {
        //var random = new Random(256);
        int c = (int)Math.Sqrt(boidCount);
        //Debug.Log(c);
        
        var boidArray =
            new NativeArray<BoidState>(boidCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

        for (int i = 0; i < boidCount; i++)
        {
            double rad = (i/(boidCount/_audioClipManager.FFT_RESOLUTION)) * Math.PI / 180f;
            Vector3 pos = new Vector3((float) Math.Cos(rad) * _radius, (float) Math.Sin(rad) * _radius, 0f);
            //Vector3 pos = new Vector3((float) Math.Cos(rad) * _radius, 0.0125f*i, 0f);
            boidArray[i] = new BoidState
            {
                orgPosition = pos,
                Position = pos,
                Forward = new Vector3(0, 0, 30),
                Angle = new Vector3(0, 0, i%360),
                Size = 0.1f,
                rad = (float)rad,
            };
        }

        var boidBuffer =
            new GraphicsBuffer(GraphicsBuffer.Target.Structured, boidArray.Length, Marshal.SizeOf<BoidState>());
        boidBuffer.SetData(boidArray);
        boidArray.Dispose();
        return boidBuffer;
    }

    public GraphicsBuffer PopulateAudioData()
    {
        var arr = new NativeArray<AudioState>(_audioClipManager.FFT_RESOLUTION, Allocator.Temp,
            NativeArrayOptions.ClearMemory);

        var spectramBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _audioClipManager.FFT_RESOLUTION, Marshal.SizeOf<AudioState>());
        spectramBuffer.SetData(arr);
        arr.Dispose();
        return spectramBuffer;
    }

    public void UpdateAudioBuffer()
    {
        _audioBuffer.SetData(_audioClipManager.GetAudioStateData());
    }
}