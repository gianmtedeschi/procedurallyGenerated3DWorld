//#define DEBUG_BOIDS
using MergeSort;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class BoidsEmitter : MonoBehaviour
{
    const int GROUP_SIZE = 256;

    [Header("General")]
    public Vector3 emitterSize;
    public float domainSize;
    [Range(0, 18)]
    public int powOfTwo;
    public float radius;

    [Header("Boids Behavior")]
    [Range(0.0f, 0.2f)]
    public float cohesion = 0.5f;
    [Range(0.0f, 0.2f)]
    public float alignment = 0.5f;
    [Range(0.0f, 2.0f)]
    public float avoidance = 0.5f;
    [Range(0.0f, 10.0f)]
    public float obstacleAvoidance = 0.5f;
    [Range(0.0f, 5.0f)]
    public float speed = 0.1f;

    [Header("Rendering")]
    public Mesh mesh;
    public Material material;
    public float size;

    [Header("Compute Shaders")]
    public ComputeShader bitonicSort;

    [Header("Gizmos")]
    public bool showGrid;
    public bool showDirection;

    // private variables
    private Transform _transform;
    private Vector3 _position;
    private int _resolution;
    private int _numBoids;

    // data
    private Vector3[] _boidsPositions;
    private Vector3[] _boidsDirections;
    private int[] _hashes;
    private int[] _keys;
    private Vector2Int[] _keys_minMax;

    // bitonic sort compute
    BitonicMergeSort _sort;

    // boids compute shader
    public ComputeShader _boidsCompute;

    // buffers
    private ComputeBuffer _hashes_buffer;
    private ComputeBuffer _keys_buffer;
    private ComputeBuffer _keys_minMax_buffer;
    private ComputeBuffer _boidsPositions_buffer;
    private ComputeBuffer _boidsDirections_buffer;
    private ComputeBuffer _boidsNewDirections_buffer;
    //drawing purpose only
    private ComputeBuffer _boidsPrevDirections_buffer;

    // collision sdf
    [SerializeField]
    public Transform playerTransform;
    public float playerRadius=1.0f;
    public Texture3D _sdf;

    // constants
    private const string PROP_COUNT = "count";
    private const string PROP_RESOLUTION = "resolution";
    private const string PROP_RADIUS = "radius";
    private const string PROP_DELTATIME = "deltaTime";
    private const string PROP_SPEED = "speed";
    private const string PROP_ALIGNMENT = "alignment";
    private const string PROP_COHESION = "cohesion";
    private const string PROP_AVOIDANCE = "avoidance";
    private const string PROP_OBSTACLEAVOIDANCE = "obstacleAvoidance";
    private const string PROP_SDFRESOLUTION = "sdfResolution";
    private const string PROP_DOMAINSIZE = "domainSize";
    private const string PROP_SCALE = "_Scale";
    private const string PROP_PLAYERPOSITION = "playerPosition";
    private const string PROP_PLAYERRADIUS = "playerRadius";

    private const string BUFF_HASHES = "_Hashes";
    private const string BUFF_POSITIONS = "_Positions";
    private const string BUFF_KEYS = "_Keys";
    private const string BUFF_KEYS_MINMAX = "_KeysMinMax";
    private const string BUFF_DIRECTIONS = "_Directions";
    private const string BUFF_NEWDIRECTIONS = "_NewDirections";
    private const string BUFF_PREVDIRECTIONS = "_PrevDirections";

    private const string TEX_SDF = "_Sdf";

    private const string KERNEL_HASH = "Hash";
    private const string KERNEL_MINMAX = "FindMinMax";
    private const string KERNEL_CLEAN = "CleanMinMax";
    private const string KERNEL_DIRECTIONS = "ComputeDirections";
    private const string KERNEL_STEP = "Step";

    private void Awake()
    {
        _transform = GetComponent<Transform>();
        _position = transform.position - (0.5f * emitterSize);
        _resolution = (int)(domainSize / radius) + 1;
    }

    // Start is called before the first frame update
    void Start()
    {
        // number must be pow of two for bitonic sort (maybe use padding?)
        _numBoids = (int)Mathf.Pow(2, powOfTwo);

        // init compute
        _sort = new BitonicMergeSort(bitonicSort);

        // init buffers
        _hashes_buffer = new ComputeBuffer(_numBoids, sizeof(int));
        _keys_buffer = new ComputeBuffer(_numBoids, sizeof(int));
        _keys_minMax_buffer = new ComputeBuffer(_resolution * _resolution * _resolution, sizeof(int) * 2);
        _boidsPositions_buffer = new ComputeBuffer(_numBoids, sizeof(float)*3);
        _boidsDirections_buffer = new ComputeBuffer(_numBoids, sizeof(float) * 3);
        _boidsNewDirections_buffer = new ComputeBuffer(_numBoids, sizeof(float) * 3);
        _boidsPrevDirections_buffer = new ComputeBuffer(_numBoids, sizeof(float) * 3);

        _hashes = new int[_numBoids];
        _keys = new int[_numBoids];
        _keys_minMax = new Vector2Int[_resolution * _resolution * _resolution];
        _boidsPositions = new Vector3[_numBoids];
        _boidsDirections = new Vector3[_numBoids];
        
        // random position inside the box and random direction
        for (int i = 0; i < _numBoids; i++)
        {
            _boidsPositions[i] = new Vector3
                (_position.x + Random.Range(0, emitterSize.x),
                _position.y + Random.Range(0, emitterSize.y),
                _position.z + Random.Range(0, emitterSize.z));

            Vector3 direction = new Vector3
                (Random.Range(0, 1.0f),
                 Random.Range(0, 1.0f),
                 Random.Range(0, 1.0f));
            direction.Normalize();

            _boidsDirections[i] = direction;
        }

        _boidsPositions_buffer.SetData(_boidsPositions);
        _boidsDirections_buffer.SetData(_boidsDirections);
    }

    private void HashBoids(int count, float radius, int resolution, ComputeBuffer positions, ComputeBuffer hashes)
    {
        _boidsCompute.SetInt(PROP_COUNT, count);

        _boidsCompute.SetFloat(PROP_RADIUS, radius);

        _boidsCompute.SetInt(PROP_RESOLUTION, resolution);

        _boidsCompute.SetBuffer(_boidsCompute.FindKernel(KERNEL_HASH), BUFF_HASHES, hashes);

        _boidsCompute.SetBuffer(_boidsCompute.FindKernel(KERNEL_HASH), BUFF_POSITIONS, positions);

        ShaderUtil.CalcWorkSize(count, out int x, out int y, out int z);

        _boidsCompute.Dispatch(_boidsCompute.FindKernel(KERNEL_HASH), x, y, z);

    }

    private void SortHashes(ComputeBuffer keys, ComputeBuffer hashes)
    {
        _sort.Init(keys);
        _sort.SortInt(keys, hashes);

        int[] keysArray = new int[keys.count];
        int[] hashesArray = new int[hashes.count];

        keys.GetData(keysArray);
        hashes.GetData(hashesArray);

        int[] sorted = new int[keys.count];

        for (int i = 0; i < sorted.Length; i++)
        {
            sorted[i] = hashesArray[keysArray[i]];
        }


    }

    private void FindDelimiters(int count, int resolution, ComputeBuffer minMax ,ComputeBuffer keys, ComputeBuffer hashes)
    {
        _boidsCompute.SetInt(PROP_COUNT, count);

        _boidsCompute.SetInt(PROP_RESOLUTION, resolution);

        // clean minmax values before processing
        _boidsCompute.SetBuffer(_boidsCompute.FindKernel(KERNEL_CLEAN), BUFF_KEYS_MINMAX, minMax);

        _boidsCompute.Dispatch(_boidsCompute.FindKernel(KERNEL_CLEAN),
            (int)(resolution / 8.0f) + 1,
            (int)(resolution / 8.0f) + 1,
            (int)(resolution / 8.0f) + 1);


        // find min max delimiters for keys
        _boidsCompute.SetBuffer(_boidsCompute.FindKernel(KERNEL_MINMAX), BUFF_HASHES, hashes);

        _boidsCompute.SetBuffer(_boidsCompute.FindKernel(KERNEL_MINMAX), BUFF_KEYS, keys);

        _boidsCompute.SetBuffer(_boidsCompute.FindKernel(KERNEL_MINMAX), BUFF_KEYS_MINMAX, minMax);

        ShaderUtil.CalcWorkSize(count, out int x, out int y, out int z);

        _boidsCompute.Dispatch(_boidsCompute.FindKernel(KERNEL_MINMAX), x, y, z);
    }

    private void ComputeDirections(int count)
    {
        _boidsCompute.SetInt(PROP_SDFRESOLUTION, _sdf.width);

        _boidsCompute.SetFloat(PROP_COHESION, cohesion);

        _boidsCompute.SetFloat(PROP_ALIGNMENT, alignment);

        _boidsCompute.SetFloat(PROP_AVOIDANCE, avoidance);

        _boidsCompute.SetFloat(PROP_DOMAINSIZE, domainSize);

        _boidsCompute.SetFloat(PROP_OBSTACLEAVOIDANCE, obstacleAvoidance);

        _boidsCompute.SetBuffer(_boidsCompute.FindKernel(KERNEL_DIRECTIONS), BUFF_HASHES, _hashes_buffer);

        _boidsCompute.SetBuffer(_boidsCompute.FindKernel(KERNEL_DIRECTIONS), BUFF_KEYS, _keys_buffer);

        _boidsCompute.SetBuffer(_boidsCompute.FindKernel(KERNEL_DIRECTIONS), BUFF_KEYS_MINMAX, _keys_minMax_buffer);

        _boidsCompute.SetBuffer(_boidsCompute.FindKernel(KERNEL_DIRECTIONS), BUFF_POSITIONS, _boidsPositions_buffer);

        _boidsCompute.SetBuffer(_boidsCompute.FindKernel(KERNEL_DIRECTIONS), BUFF_DIRECTIONS, _boidsDirections_buffer);

        _boidsCompute.SetBuffer(_boidsCompute.FindKernel(KERNEL_DIRECTIONS), BUFF_NEWDIRECTIONS, _boidsNewDirections_buffer);

        _boidsCompute.SetTexture(_boidsCompute.FindKernel(KERNEL_DIRECTIONS), TEX_SDF, _sdf);

        ShaderUtil.CalcWorkSize(count, out int x, out int y, out int z);

        _boidsCompute.Dispatch(_boidsCompute.FindKernel(KERNEL_DIRECTIONS), x, y, z);

    }

    private void Step(int count, float deltaTime)
    {
        _boidsCompute.SetFloat(PROP_SPEED, speed);

        _boidsCompute.SetFloat(PROP_DELTATIME, deltaTime);

        _boidsCompute.SetInt(PROP_SDFRESOLUTION, _sdf.width);

        _boidsCompute.SetFloat(PROP_PLAYERRADIUS, playerRadius);

        _boidsCompute.SetVector(PROP_PLAYERPOSITION, playerTransform.position);

        _boidsCompute.SetBuffer(_boidsCompute.FindKernel(KERNEL_STEP), BUFF_POSITIONS, _boidsPositions_buffer);

        _boidsCompute.SetBuffer(_boidsCompute.FindKernel(KERNEL_STEP), BUFF_DIRECTIONS, _boidsDirections_buffer);

        _boidsCompute.SetBuffer(_boidsCompute.FindKernel(KERNEL_STEP), BUFF_NEWDIRECTIONS, _boidsNewDirections_buffer);

        _boidsCompute.SetBuffer(_boidsCompute.FindKernel(KERNEL_STEP), BUFF_PREVDIRECTIONS, _boidsPrevDirections_buffer);

        _boidsCompute.SetTexture(_boidsCompute.FindKernel(KERNEL_STEP), TEX_SDF, _sdf);

        ShaderUtil.CalcWorkSize(count, out int x, out int y, out int z);

        _boidsCompute.Dispatch(_boidsCompute.FindKernel(KERNEL_STEP), x, y, z);

    }

    // Update is called once per frame
    private void FixedUpdate()
    {
       

        // Hash
        HashBoids(_numBoids, radius, _resolution, _boidsPositions_buffer, _hashes_buffer);

        // Sort
        SortHashes(_keys_buffer, _hashes_buffer);

        // Find cell delimiters
        FindDelimiters(_numBoids, _resolution, _keys_minMax_buffer, _keys_buffer, _hashes_buffer);

        // Compute direction
        ComputeDirections(_numBoids);

        // Advance one step in the simulation
        Step(_numBoids, Time.fixedDeltaTime);

       


#if DEBUG_BOIDS
        _boidsPositions_buffer.GetData(_boidsPositions);
        _boidsNewDirections_buffer.GetData(_boidsDirections);
        _keys_buffer.GetData(_keys);
        _hashes_buffer.GetData(_hashes);
        _keys_minMax_buffer.GetData(_keys_minMax);
#endif

    }

    private void Update()
    {
        // procedural drawing

        material.SetBuffer(BUFF_POSITIONS, _boidsPositions_buffer);
        material.SetBuffer(BUFF_DIRECTIONS, _boidsDirections_buffer);
        material.SetBuffer(BUFF_PREVDIRECTIONS, _boidsPrevDirections_buffer);
        material.SetFloat(PROP_SCALE, size);
        var bounds = new Bounds(Vector3.one * domainSize / 2.0f, Vector3.one * domainSize);
        Graphics.DrawMeshInstancedProcedural(
            mesh, 0, material, bounds, _boidsPositions_buffer.count
        );
    }

    private Color[] colors = new Color[] { Color.blue, Color.red, Color.cyan, Color.green, Color.grey, Color.magenta, Color.yellow };

    private void OnDestroy()
    {
        _hashes_buffer.Dispose();
        _keys_buffer.Dispose();
        _keys_minMax_buffer.Dispose();
        _boidsPositions_buffer.Dispose();
        _boidsDirections_buffer.Dispose();
        _boidsNewDirections_buffer.Dispose();
    }

    private void OnDrawGizmos()
    {
        #region Bounds
        Gizmos.color = Color.green;

        Gizmos.DrawWireCube(transform.position, emitterSize);
        #endregion

        #region DomainBounds
        Gizmos.color = Color.white;

        Gizmos.DrawWireCube(Vector3.one*0.5f*domainSize, Vector3.one*domainSize);
        #endregion

#if DEBUG_BOIDS
#region Boids
        Gizmos.color = Color.cyan;
        if(_keys_minMax!=null)
        {
            for (int i = 0; i < _keys_minMax.Length; i++)
            {
                if (_keys_minMax[i].x == -1)
                    continue;

                if (showGrid)
                {
                    Gizmos.color = new Color(colors[i % colors.Length].r, colors[i % colors.Length].g, colors[i % colors.Length].b, 0.3f);
                }
                Vector3 center = new Vector3
                    (i % _resolution,
                    ((int)(i / (float)_resolution) % (_resolution)),
                    (int)(i / (float)(_resolution * _resolution))) * radius + Vector3.one * 0.5f * radius;

                if (showGrid)
                {
                    Gizmos.DrawCube(center, radius * Vector3.one);
                    Gizmos.color = colors[i % colors.Length];
                }

                for (int j = _keys_minMax[i].x; j <= _keys_minMax[i].y; j++)
                {  
                    Vector3 pos = _boidsPositions[_keys[j]];
                    Gizmos.DrawSphere(pos, radius / 5.0f);

                    if(showDirection)
                        Gizmos.DrawLine(pos, pos + _boidsDirections[_keys[j]]);
                }

            }
        }


#endregion
#endif
    }
}
