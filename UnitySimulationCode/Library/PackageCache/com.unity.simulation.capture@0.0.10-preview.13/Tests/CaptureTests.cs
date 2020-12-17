using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Simulation;

using UnityEngine.TestTools;
using NUnit.Framework;
using UnityEditor;
using Object = UnityEngine.Object;
#if ENABLE_CLOUDTESTS
using Unity.Simulation.Tools;
#endif

public class CaptureTests
{
    readonly Color32 kTestColor = Color.blue;



    [SetUp]
    public void Reset()
    {
        AsyncRequest.maxJobSystemParallelism = 3;
        var dir = Application.persistentDataPath;
        if (Directory.Exists(dir))
        {
            var dirInfo = new DirectoryInfo(dir);
            foreach (var directory in dirInfo.GetDirectories())
            {
                directory.Delete(true);
            }

            foreach (var file in dirInfo.GetFiles())
            {
                file.Delete();
            }
        }
    }
    
    List<Object> m_ObjectsToDestroy = new List<Object>();
    [TearDown]
    public void TearDown()
    {
        foreach (var o in m_ObjectsToDestroy)
            Object.DestroyImmediate(o);

        m_ObjectsToDestroy.Clear();
    }

    public void AddTestObjectForCleanup(Object @object) => m_ObjectsToDestroy.Add(@object);

    public void DestroyTestObject(Object @object)
    {
        Object.DestroyImmediate(@object);
        m_ObjectsToDestroy.Remove(@object);
    }
    
    [UnityTest]
    public IEnumerator CaptureTest_ComputeBuffer()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Debug.Log("Compute Shader not supported to passing the test!");
            yield break;
        }
        const int kNumberOfFloats = 8000;

        Debug.Assert(SystemInfo.supportsComputeShaders, "Compute shaders are not supported.");

        var computeShader = Resources.Load("CaptureTestComputeBuffer") as ComputeShader;
        Debug.Assert(computeShader != null);

        var kernel = computeShader.FindKernel("CSMain");

        var input  = new ComputeBuffer(kNumberOfFloats, sizeof(float), ComputeBufferType.Default);
        computeShader.SetBuffer(kernel, "inputBuffer", input);

        var output = new ComputeBuffer(kNumberOfFloats, sizeof(float), ComputeBufferType.Default);
        computeShader.SetBuffer(kernel, "outputBuffer", output);

        var rvalue = UnityEngine.Random.Range(0, 100000);

        var floats = new float[kNumberOfFloats];
        for (var i = 0; i < floats.Length; ++i)
            floats[i] = rvalue;

        input.SetData(floats);

        computeShader.Dispatch(kernel, kNumberOfFloats/8, 1, 1);

        var request = CaptureGPUBuffer.Capture<float>(output);

        while (!request.completed)
            yield return null;

        var results = request.data as float[];

        int count = 0;
        for (var i = 0; i < results.Length; ++i)
            if (results[i] - 1 + rvalue <= Mathf.Epsilon)
                ++count;

        Debug.Assert(count == 0, "Output values are not the expected value.");   

        request.Dispose();
        input.Dispose();
        output.Dispose();
    }

#if ENABLE_CLOUDTESTS
    [CloudTest]
#endif
    [UnityTest]
    public IEnumerator CaptureTest_RenderTexture()
    {
        const int kDimension = 4;
        const int kLength = kDimension * kDimension;

        var color = new Color32((byte)UnityEngine.Random.Range(0, 255), (byte)UnityEngine.Random.Range(0, 255), (byte)UnityEngine.Random.Range(0, 255), 255);

        var data = new Color32[kLength];
        for (var i = 0; i < data.Length; ++i)
            data[i] = color;

        var texture = new Texture2D(kDimension, kDimension, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
        texture.SetPixels32(data);
        texture.Apply();

        var rt = new RenderTexture(kDimension, kDimension, 0, GraphicsFormat.R8G8B8A8_UNorm);

        Graphics.Blit(texture, rt);

        var request = CaptureRenderTexture.Capture(rt);

        while (!request.completed)
            yield return null;

        var results = ArrayUtilities.Cast<Color32>(request.data as Array);

        int count = 0;
        for (var i = 0; i < kLength; ++i)
            if (!results[i].Equals(color))
                ++count;
        
        Debug.Assert(count == 0, string.Format("Output values are not the expected value. Results[0]: {0}, Color {1} " , results[0].ToString(), color.ToString()));   

        request.Dispose();
    }


    
    [UnityTest]
    public IEnumerator CaptureTest_CaptureColorAsColor32_AndDepthAs16bitShort()
    {
        return CaptureTest_CaptureColorAndDepthParametric
        (
            16, 
            GraphicsFormat.R8G8B8A8_UNorm,
            (AsyncRequest<CaptureCamera.CaptureState> request) =>
            {
                Debug.Assert(request.error == false, "Async request failed");
                var colorBuffer = ArrayUtilities.Cast<byte>(request.data.colorBuffer as Array);
                var depthBuffer = ArrayUtilities.Cast<short>(request.data.depthBuffer as Array);

                Debug.Assert(EnsureColorsCloseTo(kTestColor, colorBuffer, 0) == 0, "colorBuffer differs from expected output");
            }
        );
    }

    [UnityTest]
    public IEnumerator CaptureTest_CaptureColorAsColor32_AndDepthAs32bitFloat()
    {
        return CaptureTest_CaptureColorAndDepthParametric
        (
            24, 
            GraphicsFormat.R8G8B8A8_UNorm,
            (AsyncRequest<CaptureCamera.CaptureState> request) =>
            {
                Debug.Assert(request.error == false, "Async request failed");
                var colorBuffer = ArrayUtilities.Cast<byte>(request.data.colorBuffer as Array);
                var depthBuffer = ArrayUtilities.Cast<float>(request.data.depthBuffer as Array);

                Debug.Assert(EnsureColorsCloseTo(kTestColor, colorBuffer, 0) == 0, "colorBuffer differs from expected output");
            }
        );
    }

    [UnityTest]
    public IEnumerator CaptureTest_CaptureColorAndDepth16_FastAndSlow_CheckConsistency()
    {
        byte[] colorBufferFast = null;
        short[] depthBufferFast = null;

        byte[] colorBufferSlow = null;
        short[] depthBufferSlow = null;

        var useAsyncReadbackIfSupported = CaptureOptions.useAsyncReadbackIfSupported;
        CaptureOptions.useAsyncReadbackIfSupported = false;
        yield return CaptureTest_CaptureColorAndDepthParametric
        (
            16, 
            GraphicsFormat.R8G8B8A8_UNorm,
            (AsyncRequest<CaptureCamera.CaptureState> request) =>
            {
                Debug.Assert(request.error == false, "Async request failed");
                colorBufferFast = ArrayUtilities.Cast<byte>(request.data.colorBuffer as Array);
                depthBufferFast = ArrayUtilities.Cast<short>(request.data.depthBuffer as Array);
            }
        );

        CaptureOptions.useAsyncReadbackIfSupported = false;
        yield return CaptureTest_CaptureColorAndDepthParametric
        (
            16, 
            GraphicsFormat.R8G8B8A8_UNorm,
            (AsyncRequest<CaptureCamera.CaptureState> request) =>
            {
                Debug.Assert(request.error == false, "Async request failed");
                colorBufferSlow = ArrayUtilities.Cast<byte>(request.data.colorBuffer as Array);
                depthBufferSlow = ArrayUtilities.Cast<short>(request.data.depthBuffer as Array);
            }
        );

        CaptureOptions.useAsyncReadbackIfSupported = useAsyncReadbackIfSupported;

        int count = 0;
        for (var i = 0; i < colorBufferFast.Length; ++i)
        {
            if (colorBufferFast[i] != colorBufferSlow[i])
                ++count;
        }

        Debug.Assert(count == 0, "color buffers differ by " + count);

        count = 0;
        for (var i = 0; i < ArrayUtilities.Count<short>(depthBufferFast); ++i)
        {
            if (Math.Abs(depthBufferFast[i] - depthBufferSlow[i]) > 0)
                ++count;
        }

        Debug.Assert(count == 0, "depth buffers differ by " + count);
    }

#if ENABLE_CLOUDTESTS
    [CloudTest]
#endif
    [UnityTest]
    public IEnumerator CaptureTest_CaptureColorAndDepth32_FastAndSlow_CheckConsistency()
    {
        byte[] colorBufferFast = null;
        float[] depthBufferFast = null;

        byte[] colorBufferSlow = null;
        float[] depthBufferSlow = null;

        var useAsyncReadbackIfSupported = CaptureOptions.useAsyncReadbackIfSupported;
        CaptureOptions.useAsyncReadbackIfSupported = true;
        yield return CaptureTest_CaptureColorAndDepthParametric
        (
            32, 
            GraphicsFormat.R8G8B8A8_UNorm,
            (AsyncRequest<CaptureCamera.CaptureState> request) =>
            {
                Debug.Assert(request.error == false, "Async request failed");
                colorBufferFast = ArrayUtilities.Cast<byte>(request.data.colorBuffer as Array);
                depthBufferFast = ArrayUtilities.Cast<float>(request.data.depthBuffer as Array);
            }
        );

        CaptureOptions.useAsyncReadbackIfSupported = false;
        yield return CaptureTest_CaptureColorAndDepthParametric
        (
            32, 
            GraphicsFormat.R8G8B8A8_UNorm,
            (AsyncRequest<CaptureCamera.CaptureState> request) =>
            {
                Debug.Assert(request.error == false, "Async request failed");
                colorBufferSlow = ArrayUtilities.Cast<byte>(request.data.colorBuffer as Array);
                depthBufferSlow = ArrayUtilities.Cast<float>(request.data.depthBuffer as Array);
            }
        );

        CaptureOptions.useAsyncReadbackIfSupported = useAsyncReadbackIfSupported;

        int count = 0;
        for (var i = 0; i < colorBufferFast.Length; ++i)
        {
            if (colorBufferFast[i] != colorBufferSlow[i])
                ++count;
        }

        Debug.Assert(count == 0, "color buffers differ by " + count);

        count = 0;
        for (var i = 0; i < ArrayUtilities.Count<float>(depthBufferFast); ++i)
        {
            if (Math.Abs(depthBufferFast[i] - depthBufferSlow[i]) > 1e-6f)
                ++count;
        }

        Debug.Assert(count == 0, "depth buffers differ by " + count);
    }

    Camera SetupCameraWithRenderTexture(int width, int height, GraphicsFormat renderTextureFormat, float near = 0.1f, float far = 1000)
    {
        var go = new GameObject("DataCaptureTestsCamera");
        
        var camera = go.AddComponent<Camera>();
        camera.enabled = false;
        camera.transform.position = Vector3.zero;
        camera.transform.rotation = Quaternion.identity;
        camera.nearClipPlane = near;
        camera.farClipPlane = far;
        camera.orthographic = true;
        camera.orthographicSize = 1;
        camera.fieldOfView = 45;
        camera.targetTexture = new RenderTexture(width, height, 0, renderTextureFormat);

        RenderTexture.active = null;
        
        AddTestObjectForCleanup(go);

        return camera;
    }

    Camera SetupCameraTestWithMaterial(int depthBpp, GraphicsFormat renderTextureFormat, Vector3 gopos, float near = 0.1f, float far = 1000)
    {
        var camera = SetupCameraWithRenderTexture(32, 32, renderTextureFormat, near, far);

        if (depthBpp > 0)
            camera.depthTextureMode = DepthTextureMode.Depth;
        
        var plane = CreatePlaneInFrontOfCamera(kTestColor);
        plane.transform.position = gopos;

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = gopos;

        var renderer = cube.GetComponent<Renderer>();
        Debug.Assert(renderer != null);

        var shader = Shader.Find("Hidden/DataCaptureTestsUnlitShader");
        Debug.Assert(shader != null);

        renderer.material.shader = shader;
        renderer.material.color = kTestColor;

        return camera;
    }

    public IEnumerator CaptureTest_CaptureColorAndDepthParametric(int depthBpp, GraphicsFormat renderTextureFormat, Action<AsyncRequest<CaptureCamera.CaptureState>> validator)
    {
        Debug.Assert(GraphicsUtilities.SupportsRenderTextureFormat(renderTextureFormat), "GraphicsFormat not supported");

        var camera = SetupCameraTestWithMaterial(depthBpp, renderTextureFormat, new Vector3(0, 0, 1.0f));

        var request = CaptureCamera.Capture(camera, colorFunctor: AsyncRequest<CaptureCamera.CaptureState>.DontCare, depthFunctor: AsyncRequest<CaptureCamera.CaptureState>.DontCare, depthFormat: GraphicsUtilities.DepthFormatForDepth(depthBpp));

        camera.Render();

        while (!request.completed)
            yield return null;

        Debug.Assert(request.error == false, "Capture request had an error");

        validator.Invoke(request);
    }

#if ENABLE_CLOUDTESTS
    [CloudTest]
#endif
    [UnityTest]
    public IEnumerator CaptureTest_CaptureColorUsingNonAsyncMethod()
    {
        const int kDimension = 64;
        const int kLength = kDimension * kDimension;

        var color = kTestColor;
        
        var data = new Color32[kLength];
        for (var i = 0; i < data.Length; ++i)
            data[i] = color;

        var texture = new Texture2D(kDimension, kDimension, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None);
        texture.SetPixels32(data);
        texture.Apply();

        var rt = new RenderTexture(kDimension, kDimension, 0, GraphicsFormat.R8G8B8A8_UNorm);
        rt.useMipMap = false;
        rt.autoGenerateMips = false;

        Graphics.Blit(texture, rt);

        var request = Manager.Instance.CreateRequest<AsyncRequest<object>>();

        Func<AsyncRequest<object>, AsyncRequest<object>.Result> functor = (AsyncRequest<object> r) =>
        {
            var colorBuffer = GraphicsUtilities.GetPixelsSlow(rt as RenderTexture);
            Debug.Assert(EnsureColorsCloseTo(kTestColor, colorBuffer, 0) == 0, "colorBuffer differs from expected output");
            return AsyncRequest<object>.Result.Completed;
        };

        request.Enqueue(functor);
        request.Execute(AsyncRequest.ExecutionContext.EndOfFrame);

        while (!request.completed)
            yield return null;

        Debug.Assert(request.error == false, "Request had an error");
    }

#if ENABLE_CLOUDTESTS
    [CloudTest]
#endif
    [UnityTest]
    public IEnumerator CaptureTest_CaptureDepth32ToFile()
    {
        var camera = SetupCameraTestWithMaterial(32, GraphicsFormat.R8G8B8A8_UNorm, new Vector3(0, 0, 500.5f), 0.1f, 1000);
        var depthPath = Path.Combine(Application.persistentDataPath, "depth32.tga");
        var request = CaptureCamera.CaptureDepthToFile(camera, GraphicsFormat.R32_SFloat, depthPath);

        camera.Render();

        while (!request.completed)
            yield return null;

        Debug.Assert(request.error == false, "Capture request had an error");
    }

#if ENABLE_CLOUDTESTS
    [CloudTest]
#endif
    [UnityTest]
    public IEnumerator CaptureTest_CaptureDepth16ToFile()
    {
        var camera = SetupCameraTestWithMaterial(16, GraphicsFormat.R8G8B8A8_UNorm, new Vector3(0, 0, 500.5f), 0.1f, 1000);
        var depthPath = Path.Combine(Application.persistentDataPath, "depth16.tga");
        var request = CaptureCamera.CaptureDepthToFile(camera, GraphicsFormat.R16_UNorm, depthPath);

        camera.Render();

        while (!request.completed)
            yield return null;

        Debug.Assert(request.error == false, "Capture request had an error");
    }
    
    int EnsureColorsCloseTo(Color32 exemplar, byte[] inputs, int deviation)
    {
        int numItems = ArrayUtilities.Count(inputs);

        int count = 0;
        for (int i = 0; i < numItems; i += 4)
        {
            Color32 c;
            c.r = inputs[i+0];
            c.g = inputs[i+1];
            c.b = inputs[i+2];
            c.a = inputs[i+3];
            int rd = Math.Abs((int)exemplar.r - (int)c.r);
            int gd = Math.Abs((int)exemplar.g - (int)c.g);
            int bd = Math.Abs((int)exemplar.b - (int)c.b);
            int ad = Math.Abs((int)exemplar.a - (int)c.a);
            if (rd > deviation || gd > deviation || bd > deviation || ad > deviation)
                ++count;
        }
        return count;
    }

    int EnsureDepthCloseTo(short exemplar, short[] inputs, short deviation)
    {
        int numItems = ArrayUtilities.Count(inputs);

        int count = 0;
        for (int i = 0; i < numItems; ++i)
        {
            var s = inputs[i];
            var d = Math.Abs(exemplar - s);
            if (d > deviation)
                ++count;
        }
        return count;
    }

    int EnsureDepthCloseTo(float exemplar, float[] inputs, float deviation)
    {
        int numItems = ArrayUtilities.Count(inputs);

        int count = 0;
        for (int i = 0; i < numItems; ++i)
        {
            var f = inputs[i];
            var d = Mathf.Abs(exemplar - f);
            if (d > deviation)
                ++count;
        }
        return count;
    }

    bool CompareColors(Color a, Color b)
    {
        return Math.Abs(a.r - b.r) < 1e-6f && Math.Abs(a.g - b.g) < 1e-6f && Math.Abs(a.b - b.b) < 1e-6f;
    }

#if UNITY_2019_3_OR_NEWER
    IEnumerator CaptureColorAndEnsureUpright(bool fastPath)
    {
        var camera = SetupCameraWithRenderTexture(2, 2, GraphicsFormat.R8G8B8A8_UNorm);

        var imagePath = Path.Combine(Application.persistentDataPath, "upright.png");

        var useAsyncReadbackIfSupported = CaptureOptions.useAsyncReadbackIfSupported;
        CaptureOptions.useAsyncReadbackIfSupported = fastPath;

        var request = CaptureCamera.CaptureColorToFile(camera, GraphicsFormat.R8G8B8A8_UNorm, imagePath, CaptureImageEncoder.ImageFormat.Png);
        
        var plane1 = CreatePlaneInFrontOfCamera(Color.black, .1f);
        //position on the bottom half of the screen, in front of plane2
        plane1.transform.localPosition = new Vector3(0, -.5f, .5f);
        plane1.transform.localScale = new Vector3(1, -1f, .1f);
        var plane2 = CreatePlaneInFrontOfCamera(Color.red);
        plane2.transform.localPosition = new Vector3(0, 0, 1f);
        plane2.transform.localScale = new Vector3(1, -1f, 1f);

        camera.clearFlags = CameraClearFlags.Nothing;
        camera.Render();

        while (!request.completed)
            yield return null;

        CaptureOptions.useAsyncReadbackIfSupported = useAsyncReadbackIfSupported;

        Assert.True(request.error == false);
        
        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        texture.LoadImage(File.ReadAllBytes(imagePath));

        Assert.True(CompareColors(texture.GetPixel(0, 0), Color.black));
        Assert.True(CompareColors(texture.GetPixel(1, 0), Color.black));
        Assert.True(CompareColors(texture.GetPixel(0, 1), Color.red));
        Assert.True(CompareColors(texture.GetPixel(1, 1), Color.red));
    }

#if ENABLE_CLOUDTESTS
    [CloudTest]
#endif
    [UnityTest]
    public IEnumerator CaptureTest_1stCaptureColorAndEnsureUpright_FastPath()
    {
        yield return CaptureColorAndEnsureUpright(true);
    }

#if ENABLE_CLOUDTESTS
    [CloudTest]
#endif
    [UnityTest]
    public IEnumerator CaptureTest_1stCaptureColorAndEnsureUpright_SlowPath()
    {
        yield return CaptureColorAndEnsureUpright(false);
    }
#endif // UNITY_2019_3_OR_NEWER
    
    public GameObject CreatePlaneInFrontOfCamera(Color color, float scale = 1)
    {
        GameObject planeObject;
        planeObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
        planeObject.transform.SetPositionAndRotation(new Vector3(0, 0, 10), Quaternion.Euler(90, 0, 0));
        planeObject.transform.localScale = new Vector3(scale, -1, scale);

        var material = new Material(Shader.Find("Hidden/DataCaptureTestsUnlitShader"));
        material.color = color;

        planeObject.GetComponent<MeshRenderer>().sharedMaterial = material;
        AddTestObjectForCleanup(planeObject);
        return planeObject;
    }
}
