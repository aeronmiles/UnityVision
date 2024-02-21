using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using System.Collections.Generic;

// @TODO: refactor
public class PointTracker : MonoBehaviour
{
    [SerializeField] private CameraImage _image = null;
    public Color color;
    public float gammaCorrection = 2.2f;
    public float threshold = 0.5f;
    public float minSpacingThreshold = 0.01f;
    public float mergeDistance = 2f;

    [Header("Debug")]
    public float width;
    public float height;
    public GameObject pointObj;
    private readonly List<GameObject> _pointObjs = new List<GameObject>();

    private void OnEnable()
    {
        _image.imageUpdate += OnImageUpdate;
    }

    private void OnDisable()
    {
        _image.imageUpdate -= OnImageUpdate;
    }

    private void OnImageUpdate(WebCamTexture tex)
    {

        TrackPointsJob pointJob = TrackPointJob(tex);
        JobHandle handle = pointJob.Schedule();
        handle.Complete();

        int l = pointJob.r[1];
        while (_pointObjs.Count < l) _pointObjs.Add(Instantiate(pointObj, transform));

        int x = 0;
        foreach (var o in _pointObjs)
        {
            o.SetActive(false);
            if (x < l)
            {
                o.transform.position = new Vector3(pointJob.result[x].x * width, pointJob.result[x].y * height, 0f);
                o.SetActive(true);
                x++;
            }
        }

        pointJob.Dispose();
    }

    private Color32[] _values;
    private TrackPointsJob TrackPointJob(WebCamTexture tex)
    {
        TrackPointsJob pointsJob = new TrackPointsJob
        {
            color = math.pow(new float3(color.r, color.g, color.b), gammaCorrection),
            threshold = threshold,
            mergeDistance = mergeDistance,
            minSpacingThreshold = minSpacingThreshold,
            width = tex.width,
            height = tex.height
        };

        _values = tex.GetPixels32(_values);

        int l = _values.Length;

        pointsJob.values = new NativeArray<Color32>(_values, Allocator.TempJob);
        pointsJob.result = new NativeArray<float2>(l, Allocator.TempJob);
        pointsJob.r = new NativeArray<int>(l, Allocator.TempJob);
        pointsJob.r2 = new NativeArray<float>(l, Allocator.TempJob);
        pointsJob.pixels = new NativeArray<float2>(l, Allocator.TempJob);
        pointsJob.positions = new NativeArray<PointValues>(l, Allocator.TempJob);

        return pointsJob;
    }
}

// TODO : optimize as IJobParallelFor
[BurstCompile(CompileSynchronously = true)]
public struct TrackPointsJob : IJob
{
    [ReadOnly] public float3 color;
    [ReadOnly] public float threshold, minSpacingThreshold, width, height, mergeDistance;
    [ReadOnly] public NativeArray<Color32> values;

    public NativeArray<float2> pixels;
    public NativeArray<PointValues> positions;

    [WriteOnly] public NativeArray<float2> result;
    [WriteOnly] public NativeArray<int> r;
    [WriteOnly] public NativeArray<float> r2;

    public void Execute()
    {
        // find all pixels within threshold
        int l = values.Length;
        int nPixels = 0;

        float minSpacing = float.MaxValue;
        float2 lastPos = float2.zero;
        for (int i = 0; i < l; i++)
        {
            var cI = new float3(values[i].r / 255f, values[i].g / 255f, values[i].b / 255f);
            if (math.distance(cI, color) < threshold)
            {
                float y = math.floor(i / width);
                float2 pos = new float2((i - y * width) / width, y / height);
                if (math.distance(lastPos, pos) > minSpacingThreshold)
                {
                    minSpacing = math.min(math.distance(lastPos, pos), minSpacing);
                }
                lastPos = pixels[nPixels++] = pos;
            }
        }

        r[0] = nPixels;
        r2[0] = minSpacing;
        if (nPixels == 0) return;

        // collect summed pixel values of pixels within the merge distance

        mergeDistance *= minSpacing;
        positions[0] = new PointValues(pixels[0], pixels[0], 1);
        int nPositions = 1;
        // int posMaxIndex = positions.Length - 1;
        for (int pI = 1; pI < nPixels; pI++)
        {
            int closestInd = 0;
            float closesDistance = float.MaxValue;
            for (int posI = 0; posI < nPositions; posI++)
            {
                float d = math.distance(positions[posI].Position, pixels[pI]);
                if (d < closesDistance)
                {
                    closesDistance = d;
                    closestInd = posI;
                }
            }
            if (closesDistance < mergeDistance)
            {
                positions[closestInd] = new PointValues(
                    positions[closestInd].Position,
                    positions[closestInd].Cummulative + pixels[pI],
                    positions[closestInd].Count + 1
                );
            }
            else
            {
                positions[nPositions++] = new PointValues(
                    pixels[pI],
                    pixels[pI],
                    1
                );
            }
        }

        r[1] = nPositions;

        // calculate the average merged pixel point positions
        for (int i = 0; i < nPositions; i++)
        {
            result[i] = positions[i].Cummulative / positions[i].Count;
        }
    }

    public void Dispose()
    {
        values.Dispose();
        result.Dispose();
        r.Dispose();
        r2.Dispose();
        pixels.Dispose();
        positions.Dispose();
    }
}

public struct PointValues
{
    public float2 Position;
    public float2 Cummulative;
    public float Count;

    public PointValues(float2 pos, float2 pos2, float count)
    {
        Position = pos;
        Cummulative = pos2;
        Count = count;
    }
}
