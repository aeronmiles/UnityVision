using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using System.Collections.Generic;

public class PointTracker : MonoBehaviour
{
    [SerializeField] CameraImage image = null;
    public Color Color;
    public float GammaCorrection = 2.2f;
    public float Threshold = 0.5f;
    public float MinSpacingThreshold = 0.01f;
    public float MergeDistance = 2f;

    [Header("Debug")]
    public float Width;
    public float Height;
    public GameObject pointObj;
    private List<GameObject> pointObjs = new List<GameObject>();

    private void OnEnable()
    {
        image.imageUpdate += onImageUpdate;
    }

    private void OnDisable()
    {
        image.imageUpdate -= onImageUpdate;
    }

    private void onImageUpdate(WebCamTexture tex)
    {

        TrackPointsJob pointJob = trackPointJob(tex);
        JobHandle handle = pointJob.Schedule();
        handle.Complete();

        int l = pointJob.r[1];
        while (pointObjs.Count < l) pointObjs.Add(Instantiate(pointObj, transform));

        int x = 0;
        foreach (var o in pointObjs)
        {
            o.SetActive(false);
            if (x < l)
            {
                o.transform.position = new Vector3(pointJob.result[x].x * Width, pointJob.result[x].y * Height, 0f);
                o.SetActive(true);
                x++;
            }
        }

        pointJob.Dispose();
    }

    private Color32[] values;
    private TrackPointsJob trackPointJob(WebCamTexture tex)
    {
        TrackPointsJob pointsJob = new TrackPointsJob();

        pointsJob.color = math.pow(new float3(Color.r, Color.g, Color.b), GammaCorrection);
        pointsJob.threshold = Threshold;
        pointsJob.mergeDistance = MergeDistance;
        pointsJob.minSpacingThreshold = MinSpacingThreshold;
        pointsJob.width = tex.width;
        pointsJob.height = tex.height;

        values = tex.GetPixels32(values);
        
        int l = values.Length;

        pointsJob.values = new NativeArray<Color32>(values, Allocator.TempJob);
        pointsJob.result = new NativeArray<float2>(l, Allocator.TempJob);
        pointsJob.r = new NativeArray<int>(l, Allocator.TempJob);
        pointsJob.r2 = new NativeArray<float>(l, Allocator.TempJob);
        pointsJob.pixels = new NativeArray<float2>(l, Allocator.TempJob);
        pointsJob.positions = new NativeArray<PointValues>(l, Allocator.TempJob);

        return pointsJob;
    }
}

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
        int posMaxIndex = positions.Length - 1;
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