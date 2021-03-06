#if(UNITY_EDITOR)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using Distributions.PoissonDiscSampling_VariableDensity;
using Random = Unity.Mathematics.Random;
using UnityEditor;
using System.Runtime.InteropServices;
using Unity.Collections;

[Serializable]
public struct RemovalParams
{
    public float NoiseScale;
    [Range(0, 1)]
    public float Margin;
    [Range(0, 1)]
    public float Threshold;
}

[Serializable]
public struct NOISE_LAYER_PARAMS
{
    public Vector3 offset;
    [Range(0, 10)]
    public float influence;
    public Vector3 scale;
    [Range(-1, 1)]
    public float heightMax;
    [Range(0, 15)]
    public float warpInfluence;
    public Vector3 warpScale;
};

public class MarchingCubes : MonoBehaviour
{
    [Header("General")]
    public uint seed;
    public float size;
    [Space(20)]
    [Range(10, 300)]
    public int resolution = 10;
    [Range(1, 10)]
    public int split = 1;

    [Header("Shader")]
    public ComputeShader computeNoise;
    public ComputeShader polygonise;
    public ComputeShader SDFGen;
    public ComputeShader NoiseGen;
    public ComputeShader DiffuseSDF;

    [Header("Poisson disc sampling")]
    [Range(0.1f, 10)]
    public float minDistance = 1;
    [Range(0.1f, 10)]
    public float maxDistance = 1.5f;
    public AnimationCurve distanceDistribution;
    public float poisson_noiseScale = 1;
    public RemovalParams sitesSelectionParams;

    public float poisson_radiusMultiplier=1;
    public Vector2 poisson_Height = new Vector2(0.2f, 1);


    [Header("Noise Parameters")]
    public float groundLevel;
    [Range(0, 50)]
    public float heightMultiplier;
    public NOISE_LAYER_PARAMS[] layers;

    [Header("Ground")]
    [Range(0, 10)]
    public float groundHeight;
    public Texture2D GroundTexture;

    [Header("sdf Generation")]
    public int sdfTexResolution;
    public RenderTexture _sdf;

    [Header("Debug")]
    public bool showBounds;
    public bool showPoissonPoints;

    [Header("Save Assets")]
    public string folderName;

    [NonSerialized]
    public RenderTexture _fullSdf;
    private RenderTexture _fullSdfCopy;

    private ComputeBuffer triangleList;
    private ComputeBuffer gridCells;
    private ComputeBuffer layersParams;
    private ComputeBuffer poisson_SortedIndices;
    private ComputeBuffer poisson_CellDelimiters;
    private ComputeBuffer poisson_Points;

    // Poisson
    float poissonEdgeLen;
    int poissonNumCellsX;
    int poissonNumCellsY;

    Tuple<Vector2, float>[] poissonPoints;
    Tuple<int, int>[] poissonCellDelimiters;
    int[] poissonSortedIndices;

    //Mesh
    Mesh[] meshes;
    Mesh[] collisionMeshes;
    Mesh collisionMeshWelded;
    GameObject[] children;

    public void OnValidate()
    {
        //Generate();
    }

    public void SaveAssets()
    {
        string parentFolder = @"Assets";
        string assetsFolder = $@"{parentFolder}\{folderName}";
        string guid;

        if (!AssetDatabase.IsValidFolder(assetsFolder))
            guid = AssetDatabase.CreateFolder(parentFolder, folderName);
        else
            guid = AssetDatabase.AssetPathToGUID(assetsFolder);

        #region Meshes
        if (meshes != null && meshes.Length > 0)
        {
            CombineInstance[] collisionCombineInstance = new CombineInstance[collisionMeshes.Length];

            for (int i = 0; i < meshes.Length; i++)
            {
                collisionCombineInstance[i] = new CombineInstance();
                collisionCombineInstance[i].mesh = new Mesh();
                collisionCombineInstance[i].mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                collisionCombineInstance[i].mesh.vertices = collisionMeshes[i].vertices;
                collisionCombineInstance[i].mesh.triangles = collisionMeshes[i].triangles;
                collisionCombineInstance[i].mesh.normals = collisionMeshes[i].normals;
                collisionCombineInstance[i].transform = Matrix4x4.identity;

                WeldVertices(collisionCombineInstance[i].mesh);


            }

            collisionMeshWelded = new Mesh();
            collisionMeshWelded.CombineMeshes(collisionCombineInstance);
            collisionMeshWelded.RecalculateNormals();
            collisionMeshWelded.RecalculateBounds();

            // mesh for collisions
            if (AssetDatabase.LoadAssetAtPath($@"{parentFolder}\{folderName}\collisionMesh.asset", typeof(Mesh))!=null)
            {
                // probably it's not the way it's meant to be done....but time's up
                AssetDatabase.DeleteAsset($@"{parentFolder}\{folderName}\collisionMesh.asset");
                
            }

            AssetDatabase.CreateAsset(collisionMeshWelded, $@"{parentFolder}\{folderName}\collisionMesh.asset");

            // meshes
            for (int i = 0; i < meshes.Length; i++)
            {
                string path = $@"{parentFolder}\{folderName}\mesh_{i.ToString()}.asset";

                if (AssetDatabase.LoadAssetAtPath(path, typeof(Mesh)) != null)
                {
                    // probably it's not the way it's meant to be done....but time's up
                    AssetDatabase.DeleteAsset(path);

                }

                AssetDatabase.CreateAsset(meshes[i], path);
            }
            
        }
        #endregion

        #region Texture
        if(_fullSdf!=null)
        {
            Texture3D tex = new Texture3D(_fullSdf.width, _fullSdf.height, _fullSdf.volumeDepth, _fullSdf.graphicsFormat, 0);

            Texture2D slice = new Texture2D(_fullSdf.width, _fullSdf.height, _fullSdf.graphicsFormat, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
            
            RenderTexture sliceRender = new RenderTexture(_fullSdf.width, _fullSdf.height, 0, _fullSdf.graphicsFormat, 0);

            List<float[]> rawData = new List<float[]>();

            //*****************************************************************************************************//
            //    Graphics.Copy works fine for copying as long as the GPU-side exists (at least it looks like)     //
            //    Didn't know how to update the CPU-side part of the texture and Texture3D doesn't have ReadPixels //
            //    Reading one slice at a time + call to ReadPixels maybe is not the best approach but it works     //
            //    (The big problem is that I don't know how unity stores textures internally)                      //
            //*****************************************************************************************************//
            for (int i = 0; i < _fullSdf.volumeDepth; i++)
            {     
                Graphics.CopyTexture(_fullSdf, i, sliceRender, 0);

                RenderTexture.active = sliceRender;

                slice.ReadPixels(new Rect(0, 0, sliceRender.width, sliceRender.height), 0, 0);

                rawData.Add(slice.GetPixelData<float>(0).ToArray());
            }

            tex.SetPixelData<float>(rawData.SelectMany(x => x).ToArray(), 0);

            tex.Apply();

            if (AssetDatabase.LoadAssetAtPath($@"{parentFolder}\{folderName}\fullSDF.asset", typeof(Texture3D)) != null)
            {
                // probably it's not the way it's meant to be done....but time's up
                AssetDatabase.DeleteAsset($@"{parentFolder}\{folderName}\fullSDF.asset");

            }

            AssetDatabase.CreateAsset(tex, $@"{parentFolder}\{folderName}\fullSDF.asset");
        }

        #endregion

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public void Generate()
    {
        // Point distribution (poisson disc sampling->filtering based on perlin noise values)
        #region POISSON_DISTRIBUTION

        poissonPoints = GetPointDistribution(out poissonSortedIndices, out poissonCellDelimiters);

        #endregion

        // Generating cylinders from the points
        #region SDF Gen

        _sdf = new RenderTexture(sdfTexResolution, sdfTexResolution, 0, RenderTextureFormat.ARGBFloat);

        _sdf.enableRandomWrite = true;

        _sdf.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;

        _sdf.volumeDepth = sdfTexResolution;

        _sdf.filterMode = FilterMode.Trilinear;

        _sdf.Create();


        poisson_Points = new ComputeBuffer(poissonPoints.Length, sizeof(float) * 3);

        poisson_CellDelimiters = new ComputeBuffer(poissonCellDelimiters.Length, sizeof(int) * 2);

        poisson_SortedIndices = new ComputeBuffer(poissonSortedIndices.Length, sizeof(int));

        // Setting uniforms and buffers
        SDFGen.SetInt("_Splits", split);

        SDFGen.SetInt("_Resolution", sdfTexResolution);

        SDFGen.SetFloat("_Scale", size);

        SDFGen.SetFloat("_PoissonEdgeLen", poissonEdgeLen);

        SDFGen.SetFloat("_HeightMultiplier", heightMultiplier);

        SDFGen.SetInt("_PoissonNumCellsX", poissonNumCellsX);

        SDFGen.SetInt("_PoissonNumCellsY", poissonNumCellsY);

        SDFGen.SetFloat("_HeightMin",  minDistance);

        poisson_Points.SetData(poissonPoints.SelectMany(t => new float[] { t.Item1.x, t.Item1.y, t.Item2 }).ToArray());

        poisson_CellDelimiters.SetData(poissonCellDelimiters.SelectMany(t => new int[] { t.Item1, t.Item2 }).ToArray());

        poisson_SortedIndices.SetData(poissonSortedIndices);

        SDFGen.SetTexture(SDFGen.FindKernel("GenerateSDF"), "_SDF", _sdf  );

        SDFGen.SetBuffer(SDFGen.FindKernel("GenerateSDF"), "_PoissonSortedIndices", poisson_SortedIndices);

        SDFGen.SetBuffer(SDFGen.FindKernel("GenerateSDF"), "_PoissonCellDelimiters", poisson_CellDelimiters);

        SDFGen.SetBuffer(SDFGen.FindKernel("GenerateSDF"), "_PoissonPoints", poisson_Points);

        //disptach
        int numThreads = (int)Mathf.Floor((sdfTexResolution * split) / 8.0f) + 1;

        SDFGen.Dispatch(SDFGen.FindKernel("GenerateSDF"), numThreads, numThreads, numThreads);

        #endregion

        // Generating the actual mesh + full "sdf" for GPU collisions(cylinders SDF + noise layers -> marching cubes algorithm)
        #region MESH_GENERATION

        if (children != null)
        {
            foreach (GameObject go in children)
            {
                if (go != null)
                {
                    GameObject.DestroyImmediate(go);
                }
            }
        }

        children = new GameObject[split * split * split];
        for (int i = 0; i < children.Length; i++)
        {
            children[i] = GameObject.CreatePrimitive(PrimitiveType.Quad);
        }

        // collision SDF 3d texture
        _fullSdf = new RenderTexture(resolution * split, resolution * split, 0, RenderTextureFormat.ARGBFloat);

        _fullSdf.enableRandomWrite = true;

        _fullSdf.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;

        _fullSdf.volumeDepth = resolution * split;

        _fullSdf.Create();

        _fullSdfCopy = new RenderTexture(resolution * split, resolution * split, 0, RenderTextureFormat.ARGBFloat);

        _fullSdfCopy.enableRandomWrite = true;

        _fullSdfCopy.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;

        _fullSdfCopy.volumeDepth = resolution * split;

        _fullSdfCopy.Create();

        meshes = new Mesh[split * split * split];
        collisionMeshes= new Mesh[split * split * split];
        // collision meshes
        for (int x = 0; x < split; x++)
        {
            for (int y = 0; y < split; y++)
            {
                for (int z = 0; z < split; z++)
                {
                    Vector3 offset = new Vector3(x, y, z) * size;

                    collisionMeshes[x + (y * split) + (z * split * split)] = GenerateGeometry(offset, 64);
                
                }
            }
        }

        // meshes
        for (int x = 0; x < split; x++)
        {
            for (int y = 0; y < split; y++)
            {
                for (int z = 0; z < split; z++)
                {
                    Vector3 offset = new Vector3(x, y, z) * size;

                    meshes[x + (y * split) + (z * split * split)] = GenerateGeometry(offset, resolution);

                    children[x + (y * split) + (z * split * split)].GetComponent<MeshFilter>().sharedMesh = meshes[x + (y * split) + (z * split * split)];

                    children[x + (y * split) + (z * split * split)].transform.parent = transform;

                    children[x + (y * split) + (z * split * split)].GetComponent<Renderer>().sharedMaterial = GetComponent<Renderer>().sharedMaterial;
                }
            }
        }

        // Diffuse sdf values;

        // finding the outer shell
        Graphics.CopyTexture(_fullSdf, _fullSdfCopy);

        DiffuseSDF.SetFloat("_GroundLevel", groundLevel);

        DiffuseSDF.SetInt("_Resolution", resolution * split);

        DiffuseSDF.SetTexture(DiffuseSDF.FindKernel("FindInner"), "_Values", _fullSdfCopy);

        DiffuseSDF.SetTexture(DiffuseSDF.FindKernel("FindInner"), "_Result", _fullSdf);

        DiffuseSDF.Dispatch(DiffuseSDF.FindKernel("FindInner"), resolution * split / 8, resolution * split / 8, resolution * split / 8);

        // diffusing the values

        int numIterations = 80;

        Graphics.CopyTexture(_fullSdf, _fullSdfCopy);

        for (int i = 1; i < numIterations; i++)
        {
            RenderTexture
                read = i % 2 == 0 ? _fullSdfCopy : _fullSdf,
                write = i % 2 != 0 ? _fullSdfCopy : _fullSdf;

            DiffuseSDF.SetTexture(DiffuseSDF.FindKernel("Diffuse"), "_ReadTex", read);
            DiffuseSDF.SetTexture(DiffuseSDF.FindKernel("Diffuse"), "_WriteTex", write);

            int
                incrementX = 1,
                incrementY = 0,
                incrementZ = 0;

            DiffuseSDF.SetInt("incrementX", incrementX);
            DiffuseSDF.SetInt("incrementY", incrementY);
            DiffuseSDF.SetInt("incrementZ", incrementZ);

            DiffuseSDF.SetFloat("dist", (1.0f / resolution));

            DiffuseSDF.Dispatch(DiffuseSDF.FindKernel("Diffuse"), resolution * split / 8, resolution * split / 8, resolution * split / 8);

            Graphics.CopyTexture(write, read);
        }
        for (int i = 1; i < numIterations; i++)
        {
            RenderTexture
                read = i % 2 == 0 ? _fullSdfCopy : _fullSdf,
                write = i % 2 != 0 ? _fullSdfCopy : _fullSdf;

            DiffuseSDF.SetTexture(DiffuseSDF.FindKernel("Diffuse"), "_ReadTex", read);
            DiffuseSDF.SetTexture(DiffuseSDF.FindKernel("Diffuse"), "_WriteTex", write);

            int
                incrementX = 0,
                incrementY = 1,
                incrementZ = 0;

            DiffuseSDF.SetInt("incrementX", incrementX);
            DiffuseSDF.SetInt("incrementY", incrementY);
            DiffuseSDF.SetInt("incrementZ", incrementZ);

            DiffuseSDF.SetFloat("dist", (1.0f / resolution));

            DiffuseSDF.Dispatch(DiffuseSDF.FindKernel("Diffuse"), resolution * split / 8, resolution * split / 8, resolution * split / 8);

            Graphics.CopyTexture(write, read);
        }
        for (int i = 1; i < numIterations; i++)
        {
            RenderTexture
                read = i % 2 == 0 ? _fullSdfCopy : _fullSdf,
                write = i % 2 != 0 ? _fullSdfCopy : _fullSdf;

            DiffuseSDF.SetTexture(DiffuseSDF.FindKernel("Diffuse"), "_ReadTex", read);
            DiffuseSDF.SetTexture(DiffuseSDF.FindKernel("Diffuse"), "_WriteTex", write);

            int
                incrementX = 0,
                incrementY = 0,
                incrementZ = 1;

            DiffuseSDF.SetInt("incrementX", incrementX);
            DiffuseSDF.SetInt("incrementY", incrementY);
            DiffuseSDF.SetInt("incrementZ", incrementZ);

            DiffuseSDF.SetFloat("dist", (1.0f / resolution));

            DiffuseSDF.Dispatch(DiffuseSDF.FindKernel("Diffuse"), resolution * split / 8, resolution * split / 8, resolution * split / 8);

            Graphics.CopyTexture(write, read);
        }
        #endregion

        //Releasing resources
        triangleList?.Release();
        gridCells?.Release();
        layersParams?.Release();
        poisson_SortedIndices?.Release();
        poisson_CellDelimiters?.Release();
        poisson_Points?.Release();

    }

    private void Start()
    {

       //Generate();
          
    }

    private void OnDestroy()
    {
        //Releasing resources
        triangleList?.Release();
        gridCells?.Release();
        layersParams?.Release();
        poisson_SortedIndices?.Release();
        poisson_CellDelimiters?.Release();
        poisson_Points?.Release();
    }
    private Tuple<Vector2, float>[] GetPointDistribution(out int[] indicesSortedByCell, out Tuple<int, int>[] delimiters)
    {
        // Fist we create a "random" point distribution
        Tuple<Vector2, float>[] points =
            PoissonDiscSamling_VariableDensity.PoissonDistribute(seed, Vector3.zero, Vector3.one * size *split,
                                                                 minDistance, maxDistance, poisson_noiseScale,
                                                                 out List<int>[,] grid, out poissonNumCellsX, 
                                                                 out poissonNumCellsY, out poissonEdgeLen, distanceDistribution);

        poissonCellDelimiters = new Tuple<int, int>[poissonNumCellsX * poissonNumCellsY];

        List<int> pointsIndicesForCell = new List<int>();

        List<Tuple<Vector2, float>> filtered = new List<Tuple<Vector2, float>>();

        for (int j = 0; j < poissonNumCellsY; j++)
        {
            for (int i = 0; i < poissonNumCellsX; i++)
            {
                int
                    lower, upper;

                // lower delimiter
                lower = filtered.Count;

                foreach (int index in grid[i, j])
                {
                    // Filtering initial point distribution
                    if (points[index].Item1.x > (1 - sitesSelectionParams.Margin) * (size * split) ||
                        points[index].Item1.x < (sitesSelectionParams.Margin) * (size * split) ||
                        points[index].Item1.y > (1 - sitesSelectionParams.Margin) * (size * split) ||
                        points[index].Item1.y < (sitesSelectionParams.Margin) * (size * split))
                    {
                        continue;
                    }

                        if (points[index].Item2>sitesSelectionParams.Threshold*maxDistance)
                    {
                        //add point to filtered
                        filtered.Add(points[index]);

                        //add it's index in the filtered collection to the sorted (by cell index) indices collection
                        pointsIndicesForCell.Add(filtered.Count - 1);

                    }
                   
                }

                // upper delimiter
                upper = filtered.Count;

                // if the cell contains no point set lower and upper to -1 (defining here this convention)
                if (lower == upper)
                    lower = upper = -1;

                poissonCellDelimiters[i + j * poissonNumCellsX] = new Tuple<int, int>(lower, upper);

            }

        }

        delimiters = poissonCellDelimiters.ToArray();

        indicesSortedByCell = pointsIndicesForCell.ToArray();

        return filtered.ToArray();

    }

    private float SampleNoise(float x, float y, float scale)
    {
        return Mathf.PerlinNoise(x * scale, y * scale);
    }

    private float[] _gridCellsBuff;

    private Mesh GenerateGeometry(Vector3 offset, int res)
    {
       
        float[] buffer = new float[res * res * res * 8 * 7];

        triangleList = new ComputeBuffer(res * res * res, 5 * 6 * 3 * sizeof(float));

        gridCells = new ComputeBuffer(res * res * res, 8 * 7 * sizeof(float));

        layersParams = new ComputeBuffer(layers.Length, 12 * sizeof(float));

        float[] array = new float[layers.Length * 12];
        for (int i = 0, k = 0; i < layers.Length; i++)
        {
            array[k++] = layers[i].offset.x;
            array[k++] = layers[i].offset.y;
            array[k++] = layers[i].offset.z;
            array[k++] = layers[i].influence;
            array[k++] = layers[i].scale.x;
            array[k++] = layers[i].scale.y;
            array[k++] = layers[i].scale.z;
            array[k++] = layers[i].heightMax;
            array[k++] = layers[i].warpInfluence;
            array[k++] = layers[i].warpScale.x;
            array[k++] = layers[i].warpScale.y;
            array[k++] = layers[i].warpScale.z;
        }

        layersParams.SetData(array);

        //set uniforms and buffers

        computeNoise.SetInt("_Splits", split);

        computeNoise.SetInt("_Resolution", res);

        computeNoise.SetVector("_CoordsOffset", offset);

        computeNoise.SetFloat("_Scale", size);

        computeNoise.SetFloat("_GroundLevel", groundLevel);

        computeNoise.SetFloat("_GroundHeight", groundHeight);

        computeNoise.SetInt("_NumLayers", layers.Length);

        computeNoise.SetBuffer(computeNoise.FindKernel("SampleNoise"), "_Cells", gridCells);

        computeNoise.SetBuffer(computeNoise.FindKernel("SampleNoise"), "_NoiseParams", layersParams);

        computeNoise.SetTexture(computeNoise.FindKernel("SampleNoise"), "_SDF", _sdf);

        computeNoise.SetTexture(computeNoise.FindKernel("SampleNoise"), "_FullSdf", _fullSdf);

        computeNoise.SetTexture(computeNoise.FindKernel("SampleNoise"), "_GroundTexture", GroundTexture);

        // dispatch
        int numThreads = (int)Mathf.Floor(res / 8.0f) + 1;

        computeNoise.Dispatch(computeNoise.FindKernel("SampleNoise"), numThreads, numThreads, numThreads);

        gridCells.GetData(buffer);

        _gridCellsBuff = (float[])buffer.Clone();

        //POLYGONISE
        buffer = new float[res * res * res * 5 * 6 * 3];
        //set uniforms and buffers
        polygonise.SetFloat("_Scale", size);

        polygonise.SetFloat("_GroundLevel", groundLevel);

        polygonise.SetInt("_Resolution", res);

        polygonise.SetBuffer(polygonise.FindKernel("Polygonise"), "_TriangleList", triangleList);

        polygonise.SetBuffer(polygonise.FindKernel("Polygonise"), "_Cells", gridCells);

        // dispatch
        polygonise.Dispatch(polygonise.FindKernel("Polygonise"), numThreads, numThreads, numThreads);

        triangleList.GetData(buffer);

        List<Vector3> coordsList = new List<Vector3>();
        List<Vector3> normsList = new List<Vector3>();
        for (int i = 0; i < buffer.Length; i += 18)
        {
            if (!(buffer[i] > -(size * 1.5f)))
                continue;

            coordsList.Add(new Vector3(buffer[i], buffer[i + 1], buffer[i + 2]));
            coordsList.Add(new Vector3(buffer[i+6], buffer[i + 7], buffer[i + 8]));
            coordsList.Add(new Vector3(buffer[i+12], buffer[i + 13], buffer[i + 14]));

            Vector3 mean =
                new Vector3(buffer[i + 3], buffer[i + 4], buffer[i + 5]) +
                new Vector3(buffer[i + 9], buffer[i + 10], buffer[i + 11]) +
                new Vector3(buffer[i + 15], buffer[i + 16], buffer[i + 17]);
            mean /= 3.0f;


            normsList.Add(mean);
            normsList.Add(mean);
            normsList.Add(mean);
        }


        int[] triangles = new int[coordsList.Count];

        for (int i = 0; i < triangles.Length; i++)
        {
            triangles[i] = i;
        }
        
        Mesh result = new Mesh();
        result.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        result.vertices = coordsList.ToArray();
        result.triangles = triangles;
        result.colors = normsList.Select(n=>new Color(n.x, n.y, n.z)).ToArray();

        result.RecalculateNormals();

        return result;
    
    }

    //https://stackoverflow.com/questions/59792261/simplify-collision-mesh-of-road-like-system
    public static void WeldVertices(Mesh aMesh, float aMaxDistDelta = 0.01f)
    {
        var aMaxDelta = aMaxDistDelta * aMaxDistDelta;
        var verts = aMesh.vertices;
        List<int> newVerts = new List<int>();
        int[] map = new int[verts.Length];
        // create mapping and filter duplicates.
        for (int i = 0; i < verts.Length; i++)
        {
            var p = verts[i];

            bool duplicate = false;
            for (int i2 = 0; i2 < newVerts.Count; i2++)
            {
                int a = newVerts[i2];
                if ((verts[a] - p).sqrMagnitude <= aMaxDelta)
                {
                    map[i] = i2;
                    duplicate = true;
                    break;
                }
            }
            if (!duplicate)
            {
                map[i] = newVerts.Count;
                newVerts.Add(i);
            }
        }
        // create new vertices
        var verts2 = new Vector3[newVerts.Count];
        for (int i = 0; i < newVerts.Count; i++)
        {
            int a = newVerts[i];
            verts2[i] = verts[a];
        }
        // map the triangle to the new vertices
        var tris = aMesh.triangles;
        for (int i = 0; i < tris.Length; i++)
        {
            tris[i] = map[tris[i]];
        }

        aMesh.Clear();
        aMesh.vertices = verts2;
        aMesh.triangles = tris;
    }

    private static Color[] colors = new Color[] { Color.blue, Color.magenta, Color.green, Color.cyan, Color.red, Color.yellow, Color.white };

    private void OnDrawGizmos()
    {
        #region BOUNDS
        if (showBounds)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(Vector3.zero + Vector3.one * size * split * 0.5f, Vector3.one * size * split);
        }
        #endregion

        #region POISSONPOINTS
        if (poissonPoints!=null && showPoissonPoints)
        {

          
            for (int i = 0; i < poissonCellDelimiters.Length; i++)
            {
                Gizmos.color = colors[i % colors.Length];

                if (poissonCellDelimiters[i].Item1 == -1)
                    continue;

                for (int j = poissonCellDelimiters[i].Item1; j < poissonCellDelimiters[i].Item2; j++)
                {
                    Tuple<Vector2, float> point = poissonPoints[j];


                    Gizmos.color = new Color(0, 0, (point.Item2-minDistance)/maxDistance);
                    Gizmos.DrawSphere(new Vector3(point.Item1.x, 0, point.Item1.y), point.Item2 * 0.5f);
                }

            }
        }
        #endregion
   
    }
}
#endif