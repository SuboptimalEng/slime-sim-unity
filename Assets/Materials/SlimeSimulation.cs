using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Agent
{
    public Vector2 position;
    public Vector2 direction;

    public static int Size
    {
        get { return sizeof(float) * 2 * 2; }
    }
}

public class SlimeSimulation : MonoBehaviour
{
    public ComputeShader computeShader;

    // define render texture dimensions
    [Range(128, 1024)]
    public int width;

    [Range(128, 1024)]
    public int height;

    [Range(8, 1024)]
    public int numOfAgents;

    Agent[] agents;
    ComputeBuffer agentsBuffer;
    RenderTexture positionTexture;
    RenderTexture trailMapTexture;
    RenderTexture diffusedTrailMapTexture;

    void Start()
    {
        // set depth to 0 for 2D textures
        int depth = 0;
        RenderTextureFormat format = RenderTextureFormat.ARGB32;
        RenderTextureReadWrite readWrite = RenderTextureReadWrite.Default;

        // create the position texture and enable UAV access
        positionTexture = new RenderTexture(width, height, depth, format, readWrite);
        positionTexture.enableRandomWrite = true;
        positionTexture.Create();

        trailMapTexture = new RenderTexture(width, height, depth, format, readWrite);
        trailMapTexture.enableRandomWrite = true;
        trailMapTexture.Create();

        diffusedTrailMapTexture = new RenderTexture(width, height, depth, format, readWrite);
        diffusedTrailMapTexture.enableRandomWrite = true;
        diffusedTrailMapTexture.Create();

        // set up a few agents to simulate
        agents = new Agent[numOfAgents];
        for (int i = 0; i < numOfAgents; i++)
        {
            agents[i].position = Vector2.zero;
            agents[i].direction = UnityEngine.Random.insideUnitCircle.normalized;
        }

        // set up agentsBuffer to be the correct size
        agentsBuffer = new ComputeBuffer(numOfAgents, Agent.Size);
    }

    void PrintAgentsPositions()
    {
        string[] arr = new string[agents.Length];
        for (int i = 0; i < agents.Length; i++)
        {
            arr[i] = agents[i].position.ToString();
        }
        Debug.Log(string.Join(", ", arr));
    }

    void Update()
    {
        // DiscardContents() -> tells unity you no longer need this data.
        // it does not guarantee that memory is immediately released.
        positionTexture.DiscardContents();

        // Release() -> tells unity immediately discard this memory
        // useful to reset previous positions value to float4(0, 0, 0, 0)
        // this texture should only store the current position of the agent
        positionTexture.Release();

        // set the "agents buffer" array with the latest position + direction data from "agents"
        agentsBuffer.SetData(agents);

        // todo: maybe used time.fixedDeltaTime? what's the difference anyway?
        computeShader.SetFloat("deltaTime", Time.deltaTime);
        computeShader.SetInt("numOfAgents", numOfAgents);

        int kernelHandle1 = computeShader.FindKernel("CSPositionMap");
        computeShader.SetBuffer(kernelHandle1, "AgentsBuffer", agentsBuffer);
        computeShader.SetTexture(kernelHandle1, "PositionTexture", positionTexture);
        computeShader.Dispatch(kernelHandle1, numOfAgents, 1, 1);

        // todo: figure out why we need to set the positionTexture again even though
        // we don't need to create the variable for #pragma kernel CSTrailMap
        int kernelHandle2 = computeShader.FindKernel("CSTrailMap");
        computeShader.SetTexture(kernelHandle2, "PositionTexture", positionTexture);
        computeShader.SetTexture(kernelHandle2, "TrailMapTexture", trailMapTexture);
        computeShader.Dispatch(
            kernelHandle2,
            trailMapTexture.width / 8,
            trailMapTexture.height / 8,
            1
        );

        int kernelHandle3 = computeShader.FindKernel("CSDiffuse");
        computeShader.SetInt("width", width);
        computeShader.SetInt("height", height);
        computeShader.SetTexture(kernelHandle3, "PositionTexture", positionTexture);
        computeShader.SetTexture(kernelHandle3, "TrailMapTexture", trailMapTexture);
        computeShader.SetTexture(kernelHandle3, "DiffusedTrailMapTexture", diffusedTrailMapTexture);
        computeShader.Dispatch(
            kernelHandle3,
            diffusedTrailMapTexture.width / 8,
            diffusedTrailMapTexture.height / 8,
            1
        );

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        // meshRenderer.material.mainTexture = positionTexture;
        // meshRenderer.material.mainTexture = trailMapTexture;
        meshRenderer.material.mainTexture = diffusedTrailMapTexture;

        // copy diffusedTrailMapTexture into trailMapTexture so that the trailMap
        // can decrement the values in the blurred sections of the trail
        // Graphics.Blit(diffusedTrailMapTexture, trailMapTexture);

        // update the "agents" array with the positions + directions from the compute shader
        agentsBuffer.GetData(agents);
    }

    void OnDestroy()
    {
        agentsBuffer.Release();
    }
}
