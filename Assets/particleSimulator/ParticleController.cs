using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.IMGUI.Controls;
#endif

public class ParticleController : MonoBehaviour
{
    public bool gUsesSin;
    public float sinSpeed = 1;
    public ComputeShader particleCS;
    public Mesh meshToDraw;
    public Material materialToDraw;
    public Bounds bounds;
    public float g = 10;
    public float drag = 1;
    public float distanceThreshold = 1;
    public bool useTriangularMatrix;
    [HideInInspector] public int OldNumParticleTypes;
    [HideInInspector] public bool useTriangularMatrixOld;
    public List<particleType> particleTypes;
    public float[] attractionMatrix = new float[25];


    ComputeBuffer indirectArgsBuffer;
    ComputeBuffer particleTypesBuffer;
    ComputeBuffer particlesBuffer;
    ComputeBuffer velocitiesBuffer;
    ComputeBuffer attractionsBuffer;

    int count = 0;

    int initKernelID;
    int VelocityKernelID;
    int positionKernelID;

    int numParticlesID;
    int numParticleTypesID;
    int boundsXID;
    int boundsYID;
    int boundsZID;
    int gID;
    int dID;
    int deltaTimeID;
    int attractionMatrixID;
    int particleDataID;
    int particleVelocityID;
    int particleTypesID;
    int distThreshID;
    int triangularID;

    [System.Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct particleType
    {
        public int count;
        public Color particleColor;
        public bool randomColor;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct particleTypeForCS
    {
        public Vector3 particleColor;
        public int randomColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct particleData
    {
        public Vector3 color;
        public Vector3 particlePosition;
        public Vector3 particleVelocity;
        public int type;
    }

    private void OnDisable()
    {
        cleanup();
    }

    private void OnEnable()
    {
        for (int i = 0; i < particleTypes.Count; i++)
        {
            count += particleTypes[i].count;
        }
        Application.targetFrameRate = 0;
        initKernelID = particleCS.FindKernel("init");
        VelocityKernelID = particleCS.FindKernel("CSMain");
        positionKernelID = particleCS.FindKernel("applyPosition");

        numParticlesID = Shader.PropertyToID("_numParticles");
        numParticleTypesID = Shader.PropertyToID("_numParticleTypes");
        boundsXID = Shader.PropertyToID("boundsX");
        boundsYID = Shader.PropertyToID("boundsY");
        boundsZID = Shader.PropertyToID("boundsZ");
        gID = Shader.PropertyToID("_g");
        dID = Shader.PropertyToID("_drag");
        deltaTimeID = Shader.PropertyToID("_deltaTime");
        attractionMatrixID = Shader.PropertyToID("_attractionMatrix");
        particleTypesID = Shader.PropertyToID("_particleTypes");
        particleDataID = Shader.PropertyToID("_particleData");
        particleVelocityID = Shader.PropertyToID("_particleVelocities");
        distThreshID = Shader.PropertyToID("_distThresh");
        triangularID = Shader.PropertyToID("triangular");

        createArgsBuffer();
        CreateMaterialBuffers();
    }

    public bool debug;
    private void FixedUpdate()
    {
        
    }
    private void Update()
    {
        particleCS.SetFloat(deltaTimeID, Time.deltaTime);
        attractionsBuffer.SetData(attractionMatrix);
        float _g = g;
        if (gUsesSin)
            _g *= Mathf.Sin(Time.time * sinSpeed);
        particleCS.SetFloat(gID, _g);
        particleCS.SetFloat(dID, drag);
        particleCS.SetFloat(distThreshID, distanceThreshold);

        Vector3 extents = bounds.extents / 2;
        Vector3 center = bounds.center / 2;
        particleCS.SetVector(boundsXID, new Vector2(center.x - extents.x, center.x + extents.x));
        particleCS.SetVector(boundsYID, new Vector2(center.y - extents.y, center.y + extents.y));
        particleCS.SetVector(boundsZID, new Vector2(center.z - extents.z, center.z + extents.z));

        particleCS.Dispatch(VelocityKernelID, count, 1, 1);
        particleCS.Dispatch(positionKernelID, count, 1, 1);
        if (debug)
        {
            debug = false;
            particleData[] data = new particleData[count];
            particlesBuffer.GetData(data);
            for (int i = 0; i < data.Length; i++)
            {
                Debug.Log(data[i].particlePosition);
            }
        }
        Graphics.DrawMeshInstancedIndirect(meshToDraw, 0, materialToDraw, bounds, indirectArgsBuffer, 0, null, UnityEngine.Rendering.ShadowCastingMode.Off, true);
    }

    void createArgsBuffer()
    {
        uint[] args = new uint[5];
        int subMeshIndex = 0;
        args[0] = (uint)meshToDraw.GetIndexCount(subMeshIndex);
        args[1] = (uint)count;
        args[2] = (uint)meshToDraw.GetIndexStart(subMeshIndex);
        args[3] = (uint)meshToDraw.GetBaseVertex(subMeshIndex);

        indirectArgsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
        indirectArgsBuffer.SetData(args);
    }

    void CreateMaterialBuffers()
    {
        particleCS.SetInt(triangularID, useTriangularMatrix ? 1 : 0);
        particleCS.SetInt(numParticlesID, count);

        attractionsBuffer = new ComputeBuffer(attractionMatrix.Length, sizeof(float));
        attractionsBuffer.SetData(attractionMatrix);
        particleCS.SetBuffer(VelocityKernelID, attractionMatrixID, attractionsBuffer);
        
        particleCS.SetInt(numParticleTypesID, particleTypes.Count);
        
        List<particleData> partData = new List<particleData>();
        List<particleTypeForCS> partTypes = new List<particleTypeForCS>();
        for (int i = 0; i < particleTypes.Count; i++)
        {
            Vector3 _color = new Vector3(particleTypes[i].particleColor.r, particleTypes[i].particleColor.g, particleTypes[i].particleColor.b);
            partTypes.Add(new particleTypeForCS { particleColor = _color, randomColor = particleTypes[i].randomColor ? 1 : 0 });
            for (int j = 0; j < particleTypes[i].count; j++)
            {
                partData.Add(new particleData { type = i });
            }
        }
        particleTypesBuffer = new ComputeBuffer(particleTypes.Count, Marshal.SizeOf(typeof(particleTypeForCS)));
        particleTypesBuffer.SetData(partTypes.ToArray());
        particleCS.SetBuffer(initKernelID, particleTypesID, particleTypesBuffer);

        particlesBuffer = new ComputeBuffer(count, Marshal.SizeOf(typeof(particleData)), ComputeBufferType.Structured);
        particlesBuffer.SetData(partData.ToArray());
        particleCS.SetBuffer(initKernelID, particleDataID, particlesBuffer);
        particleCS.SetBuffer(VelocityKernelID, particleDataID, particlesBuffer);
        particleCS.SetBuffer(positionKernelID, particleDataID, particlesBuffer);
        materialToDraw.SetBuffer(particleDataID, particlesBuffer);

        velocitiesBuffer = new ComputeBuffer(count, Marshal.SizeOf(typeof(Vector3)));
        particleCS.SetBuffer(initKernelID, particleVelocityID, velocitiesBuffer);
        particleCS.SetBuffer(VelocityKernelID, particleVelocityID, velocitiesBuffer);
        particleCS.SetBuffer(positionKernelID, particleVelocityID, velocitiesBuffer);

        Vector3 extents = bounds.extents / 2;
        Vector3 center = bounds.center / 2;
        particleCS.SetVector(boundsXID, new Vector2(center.x - extents.x, center.x + extents.x));
        particleCS.SetVector(boundsYID, new Vector2(center.y - extents.y, center.y + extents.y));
        particleCS.SetVector(boundsZID, new Vector2(center.z - extents.z, center.z + extents.z));
        particleCS.Dispatch(initKernelID, count, 1, 1);
    }

    void cleanup()
    {
        indirectArgsBuffer.Dispose();
        particlesBuffer.Dispose();
        particleTypesBuffer.Dispose();
        velocitiesBuffer.Dispose();
    }
    public void randomizeAttractions()
    {
        for (int i = 0; i < attractionMatrix.Length; i++)
        {
            attractionMatrix[i] = UnityEngine.Random.Range(-1f, 1f);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ParticleController))]
public class GameBoundsEditor : Editor
{
    static class Styles
    {
        public static readonly GUIStyle rightLabel = new GUIStyle("RightLabel");
    }
    const int kMaxLayers = 32;
    const int indent = 30;
    const int checkboxSize = 35;
    int labelSize = 110;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        ParticleController particleScript = (ParticleController)target;

        bool triangular = particleScript.useTriangularMatrix;
        //bool triangular = true;
        int count = particleScript.particleTypes.Count;
        if (count != particleScript.OldNumParticleTypes || triangular != particleScript.useTriangularMatrixOld)
        {
            particleScript.useTriangularMatrixOld = triangular;
            particleScript.OldNumParticleTypes = count;
            if (triangular)
                particleScript.attractionMatrix = new float[((count * count) + count) / 2];
            else
                particleScript.attractionMatrix = new float[count * count];
            Debug.Log("resetting");
        }

        GUI.matrix = Matrix4x4.identity;
        int x = 0;
        var r = GUILayoutUtility.GetRect(indent + checkboxSize * count + labelSize, checkboxSize);
        for (int j = 0; j < count; j++)
        {
            GUI.Label(new Rect(labelSize + indent + r.x + x * checkboxSize, r.y, checkboxSize, checkboxSize), j.ToString());
            x++;
        }

        int index = 0;
        int y = 0;
        int jCount = count;
        for (int i = 0; i < count; i++)
        {
            GUILayout.BeginHorizontal();
            x = 0;
            r = GUILayoutUtility.GetRect(indent + checkboxSize * count + labelSize, checkboxSize);
            var labelRect = new Rect(r.x + indent + (checkboxSize * (1+count)), r.y, labelSize, checkboxSize + 5);
            GUI.Label(labelRect, i.ToString(), Styles.rightLabel);
            
            for (int j = 0; j < jCount; j++)
            {
                var tooltip = new GUIContent("", i + "/" + j);
                float val = particleScript.attractionMatrix[index];
                int extra = i * checkboxSize;
                if (!triangular)
                    extra = 0;
                float value = EditorGUI.FloatField(new Rect(labelSize + indent + r.x + x * checkboxSize + extra, r.y, checkboxSize, checkboxSize), val);

                if (val != value)
                {
                    Undo.RecordObject(particleScript, "Change attraction Matrix");
                    particleScript.attractionMatrix[index] = value;
                }

                x++;
                index++;
            }
            y++;
            if (triangular)
                jCount--;
            GUILayout.EndHorizontal();
        }

        if (GUILayout.Button("Randomize Attractions"))
        {
            particleScript.randomizeAttractions();
        }

    }
    // draw lines between a chosen game object
    // and a selection of added game objects
    private BoxBoundsHandle m_BoundsHandle = new BoxBoundsHandle();
    void OnSceneGUI()
    {
        // get the chosen game object
        BoxBoundsHandle hand;
        ParticleController boundsScript = (ParticleController)target;

        m_BoundsHandle.center = boundsScript.bounds.center;
        m_BoundsHandle.size = boundsScript.bounds.size;

        // draw the handle
        EditorGUI.BeginChangeCheck();
        m_BoundsHandle.DrawHandle();
        if (EditorGUI.EndChangeCheck())
        {
            // record the target object before setting new values so changes can be undone/redone
            Undo.RecordObject(boundsScript, "Change Bounds");

            // copy the handle's updated data back to the target object
            Bounds newBounds = new Bounds();
            newBounds.center = m_BoundsHandle.center;
            newBounds.size = m_BoundsHandle.size;
            boundsScript.bounds = newBounds;
        }
    }
}
#endif