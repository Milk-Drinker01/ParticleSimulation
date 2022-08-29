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
    public ComputeShader particleCS;
    public Mesh meshToDraw;
    public Material materialToDraw;
    Bounds b;
    ComputeBuffer indirectArgsBuffer;
    ComputeBuffer particlesBuffer;
    public Bounds bounds;
    public float areaSize = 10;
    public float g = 10;
    public float drag = 1;
    public Color[] colors;
    public Vector4 row1;
    public Vector4 row2;
    public Vector4 row3;
    public Vector4 row4;
    public int[] numEachType;

    int count = 0;

    int initKernelID;
    int VelocityKernelID;
    int positionKernelID;

    int numParticlesID;
    int areaSizeID;
    int boundsXID;
    int boundsYID;
    int boundsZID;
    int gID;
    int dID;
    int deltaTimeID;
    int attractionMatrixID;
    int particleDataID;

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
        for (int i = 0; i< numEachType.Length; i++)
        {
            count += numEachType[i];
        }
        Application.targetFrameRate = 0;
        initKernelID = particleCS.FindKernel("init");
        VelocityKernelID = particleCS.FindKernel("CSMain");
        positionKernelID = particleCS.FindKernel("applyPosition");

        numParticlesID = Shader.PropertyToID("_numParticles");
        areaSizeID = Shader.PropertyToID("_areaSize");
        boundsXID = Shader.PropertyToID("boundsX");
        boundsYID = Shader.PropertyToID("boundsY");
        boundsZID = Shader.PropertyToID("boundsZ");
        gID = Shader.PropertyToID("_g");
        dID = Shader.PropertyToID("_drag");
        deltaTimeID = Shader.PropertyToID("_deltaTime");
        attractionMatrixID = Shader.PropertyToID("_attractionMatrix");
        particleDataID = Shader.PropertyToID("_particleData");

        b = new Bounds();
        b.center = Vector3.zero;
        b.size = Vector3.one * 99999999;

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
        particleCS.SetMatrix(attractionMatrixID, new Matrix4x4(row1, row2, row3, row4));
        particleCS.SetFloat(gID, g);
        particleCS.SetFloat(dID, drag);
        Vector3 extents = bounds.extents / 2;
        particleCS.SetVector(boundsXID, new Vector2(bounds.center.x - extents.x, bounds.center.x + extents.x));
        particleCS.SetVector(boundsYID, new Vector2(bounds.center.y - extents.y, bounds.center.y + extents.y));
        particleCS.SetVector(boundsZID, new Vector2(bounds.center.z - extents.z, bounds.center.z + extents.z));

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
        Graphics.DrawMeshInstancedIndirect(meshToDraw, 0, materialToDraw, b, indirectArgsBuffer, 0, null, UnityEngine.Rendering.ShadowCastingMode.Off, true);
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
        particlesBuffer = new ComputeBuffer(count, Marshal.SizeOf(typeof(particleData)));

        List<particleData> partData = new List<particleData>();
        for (int i = 0; i < numEachType.Length; i++)
        {
            for (int j = 0; j < numEachType[i]; j++)
            {
                partData.Add(new particleData { color = new Vector3(colors[i].r, colors[i].g, colors[i].b), type = i });
            }
        }
        particlesBuffer.SetData(partData.ToArray());

        particleCS.SetInt(numParticlesID, count);
        particleCS.SetFloat(areaSizeID, areaSize);

        particleCS.SetBuffer(initKernelID, particleDataID, particlesBuffer);
        particleCS.SetBuffer(VelocityKernelID, particleDataID, particlesBuffer);
        particleCS.SetBuffer(positionKernelID, particleDataID, particlesBuffer);
        materialToDraw.SetBuffer(particleDataID, particlesBuffer);

        particleCS.Dispatch(initKernelID, count, 1, 1);
    }

    void cleanup()
    {
        indirectArgsBuffer.Dispose();
        particlesBuffer.Dispose();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ParticleController))]
public class GameBoundsEditor : Editor
{
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