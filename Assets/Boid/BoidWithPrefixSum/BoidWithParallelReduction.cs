using System.Runtime.InteropServices;
using Audio;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(VisualEffect))]
public class BoidWithParallelReduction : MonoBehaviour
{
    [SerializeField] private AudioClipManager _audioClipManager;
    
    public int boidCount = 32;

    public float3 boidExtent = new(32f, 32f, 32f);

    public Boid.BoidConfig boidConfig;

    public ComputeShader boidParallelReductionComputeShader;
    public int prefixSumBlockSize = 32;

    public ComputeShader boidSteerComputeShader;

    VisualEffect _boidVisualEffect;
    int _boidCountPoT;
    GraphicsBuffer _boidBuffer;
    GraphicsBuffer _boidPrefixSumBuffer;
    private GraphicsBuffer _audioBuffer;

    void OnEnable()
    {
        _boidCountPoT = math.ceilpow2(boidCount);
        _boidBuffer = Boid.PopulateBoids(_boidCountPoT, boidExtent);
        _boidPrefixSumBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _boidCountPoT,
            Marshal.SizeOf<Boid.BoidState>());
        _audioBuffer = PopulateAudioData();
        
        InitializeBoids_Aggregate();
        
        InitializeBoids_Steer();
        
        _boidVisualEffect = GetComponent<VisualEffect>();
        _boidVisualEffect.SetGraphicsBuffer("Boids", _boidBuffer);
        _boidVisualEffect.SetUInt("BoidCount", (uint) _boidCountPoT);
        _boidVisualEffect.enabled = true;
    }

    void OnDisable()
    {
        _boidVisualEffect.enabled = false;
        _boidBuffer?.Dispose();
        _boidPrefixSumBuffer?.Dispose();
        _audioBuffer?.Dispose();
    }

    void Update()
    {
        bool isTapped = Input.GetMouseButtonDown((0));
        UpdateBoids_Aggregate();
        UpdateBoids_Steer(Camera.main.ScreenToWorldPoint(Input.mousePosition), isTapped);
        UpdateAudioBuffer();
    }

    void InitializeBoids_Aggregate()
    {
        boidParallelReductionComputeShader.SetInt("numBoids", _boidCountPoT);
    }

    void InitializeBoids_Steer()
    {
        var kernelIndex = boidSteerComputeShader.FindKernel("CSMain");
        boidSteerComputeShader.SetBuffer(kernelIndex, "boidBuffer", _boidBuffer);
        boidSteerComputeShader.SetBuffer(kernelIndex, "boidPrefixSumBuffer", _boidPrefixSumBuffer);
        boidSteerComputeShader.SetBuffer(kernelIndex, "audioBuffer", _audioBuffer);
        boidSteerComputeShader.SetInt("numBoids", _boidCountPoT);
        boidSteerComputeShader.SetInt("fftResolution", _audioClipManager.FFT_RESOLUTION);
    }

    void UpdateBoids_Aggregate()
    {
        var kernelIndex = boidParallelReductionComputeShader.FindKernel("CSMain");
        boidParallelReductionComputeShader.GetKernelThreadGroupSizes(kernelIndex, out var x, out var y, out var z);
        boidParallelReductionComputeShader.SetBuffer(kernelIndex, "boidBuffer", _boidBuffer);
        boidParallelReductionComputeShader.SetBuffer(kernelIndex, "boidPrefixSumBuffer", _boidPrefixSumBuffer);
        for (var n = _boidCountPoT; n >= prefixSumBlockSize; n /= prefixSumBlockSize)
        {
            boidParallelReductionComputeShader.Dispatch(kernelIndex, (int) (n / x), 1, 1);
            boidParallelReductionComputeShader.SetBuffer(kernelIndex, "boidBuffer", _boidPrefixSumBuffer);
        }
    }

    void UpdateBoids_Steer(Vector3 tapPos, bool isTapped = false)
    {
        var boidTarget = boidConfig.boidTarget != null
            ? boidConfig.boidTarget.position
            : transform.position;
        var kernelIndex = boidSteerComputeShader.FindKernel("CSMain");
        boidSteerComputeShader.SetFloat("deltaTime", Time.deltaTime);
        boidSteerComputeShader.SetInt("numBoids", _boidCountPoT);
        boidSteerComputeShader.SetFloat("separationWeight", boidConfig.separationWeight);
        boidSteerComputeShader.SetFloat("alignmentWeight", boidConfig.alignmentWeight);
        boidSteerComputeShader.SetFloat("targetWeight", boidConfig.targetWeight);
        boidSteerComputeShader.SetFloat("moveSpeed", boidConfig.moveSpeed);
        boidSteerComputeShader.SetVector("targetPosition", boidTarget);
        boidSteerComputeShader.GetKernelThreadGroupSizes(kernelIndex, out var x, out var y, out var z);
        boidSteerComputeShader.Dispatch(kernelIndex, (int) (_boidCountPoT / x), 1, 1);
        
        boidSteerComputeShader.SetVector("tapPos", tapPos);
        boidSteerComputeShader.SetBool("isTaped", isTapped);
    }
    
    public GraphicsBuffer PopulateAudioData()
    {
        var arr = new NativeArray<Boid.AudioState>(_audioClipManager.FFT_RESOLUTION, Allocator.Temp,
            NativeArrayOptions.ClearMemory);

        var spectramBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _audioClipManager.FFT_RESOLUTION, Marshal.SizeOf<Boid.AudioState>());
        spectramBuffer.SetData(arr);
        arr.Dispose();
        return spectramBuffer;
    }
    
    public void UpdateAudioBuffer()
    {
        _audioBuffer.SetData(_audioClipManager.GetAudioStateData());
    }
}