namespace Bonk;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Silk.NET.OpenAL;
using Veldrid;
using Veldrid.SPIRV;

internal class Player : IDisposable
{
    private readonly AL al = AL.GetApi();
    private readonly GraphicsDevice device;
    private readonly CommandList cl;
    private readonly Pipeline pipeline;
    private readonly Shader vertexShader, fragmentShader;
    private readonly ResourceLayout resourceLayout;
    private readonly ResourceSet resourceSet;
    private readonly Texture yPlane, uPlane, vPlane;
    private readonly Texture yPlaneStaging, uPlaneStaging, vPlaneStaging;
    private readonly Bonk.Decoder decoder;
    private readonly System.Diagnostics.Stopwatch timer = new();
    private readonly List<uint> allAudioBuffers = new();
    private readonly uint audioSource;

    private bool disposedValue;
    private double nextFrameTime;
    private bool wasPlaying;

    public Player(GraphicsDevice device, string fileName)
    {
        this.device = device;
        var factory = device.ResourceFactory;

        decoder = new(new FileStream(fileName, FileMode.Open, FileAccess.Read));
        decoder.ToggleAllAudioTracks(true);

        (Texture, Texture) CreateTexture(uint width, uint height) => (
            factory!.CreateTexture(new(width, height, 1, 1, 1, PixelFormat.R8_UNorm, TextureUsage.Sampled, TextureType.Texture2D)),
            factory!.CreateTexture(new(width, height, 1, 1, 1, PixelFormat.R8_UNorm, TextureUsage.Staging, TextureType.Texture2D)));
        (yPlane, yPlaneStaging) = CreateTexture(decoder.FrameWidth, decoder.FrameHeight);
        (uPlane, uPlaneStaging) = CreateTexture(decoder.FrameWidth / 2, decoder.FrameHeight / 2);
        (vPlane, vPlaneStaging) = CreateTexture(decoder.FrameWidth / 2, decoder.FrameHeight / 2);

        var shaders = factory.CreateFromSpirv(
            vertexShaderDescription: new(ShaderStages.Vertex, Encoding.UTF8.GetBytes(VertexShader), "main"),
            fragmentShaderDescription: new(ShaderStages.Fragment, Encoding.UTF8.GetBytes(FragmentShader), "main"));
        (vertexShader, fragmentShader) = (shaders[0], shaders[1]);

        resourceLayout = factory.CreateResourceLayout(new()
        {
            Elements = new ResourceLayoutElementDescription[]
            {
                new("yPlane", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new("uPlane", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new("vPlane", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new("yuvSampler", ResourceKind.Sampler, ShaderStages.Fragment)
            }
        });

        resourceSet = factory.CreateResourceSet(new(resourceLayout, yPlane, uPlane, vPlane, device.LinearSampler));

        pipeline = factory.CreateGraphicsPipeline(new()
        {
            BlendState = BlendStateDescription.SingleOverrideBlend,
            DepthStencilState = DepthStencilStateDescription.Disabled,
            PrimitiveTopology = PrimitiveTopology.TriangleStrip,
            RasterizerState = new()
            {
                CullMode = FaceCullMode.None,
                DepthClipEnabled = false,
                FillMode = PolygonFillMode.Solid,
                FrontFace = FrontFace.Clockwise,
                ScissorTestEnabled = false
            },
            Outputs = new(depthAttachment: null, new OutputAttachmentDescription(PixelFormat.B8_G8_R8_A8_UNorm)),
            ResourceLayouts = new[] { resourceLayout },
            ShaderSet = new(
                vertexLayouts: new VertexLayoutDescription[0],
                shaders),
        });

        cl = factory.CreateCommandList();
        nextFrameTime = 0;

        audioSource = al.GenSource();

        timer.Start();
    }

    private unsafe void UploadPlane(Texture staging, Texture target, ReadOnlySpan<byte> plane)
    {
        var map = device.Map(staging, MapMode.Write);
        fixed (void* planePtr = plane)
            Buffer.MemoryCopy(planePtr, map.Data.ToPointer(), plane.Length, plane.Length);
        device.Unmap(staging);
        cl.CopyTexture(staging, target);
    }

    private unsafe void UploadAudio(ReadOnlySpan<short> samples)
    {
        al.GetSourceProperty(audioSource, GetSourceInteger.BuffersProcessed, out int newBuffersProcessed);
        uint buffer;
        if (newBuffersProcessed > 0)
            al.SourceUnqueueBuffers(audioSource, 1, &buffer);
        else
        {
            buffer = al.GenBuffer();
            allAudioBuffers.Add(buffer);
        }

        fixed(void* sampleData = samples)
            al.BufferData(buffer, BufferFormat.Stereo16, sampleData, 2 * samples.Length, decoder.AudioTracks[0].Frequency);
        al.SourceQueueBuffers(audioSource, new[] { buffer });

        if (!wasPlaying)
        {
            wasPlaying = true;
            al.SourcePlay(audioSource);
        }
    }

    public void Render(Framebuffer output)
    {
        cl.Begin();

        if (timer.Elapsed.TotalSeconds >= nextFrameTime && decoder.MoveNext())
        {
            nextFrameTime += (double)decoder.FpsDivider / decoder.FpsDividend;
            UploadPlane(yPlaneStaging, yPlane, decoder.Current.YPlane);
            UploadPlane(uPlaneStaging, uPlane, decoder.Current.UPlane);
            UploadPlane(vPlaneStaging, vPlane, decoder.Current.VPlane);

            if (!decoder.Current.AudioSamples.IsEmpty)
                UploadAudio(decoder.Current.AudioSamples);
        }

        cl.SetPipeline(pipeline);
        cl.SetFramebuffer(output);
        cl.SetGraphicsResourceSet(0, resourceSet);
        cl.SetFullViewport(0);
        cl.Draw(4);
        cl.End();
        device.SubmitCommands(cl);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue && disposing)
        {
            al.SourceStop(audioSource);
            al.DeleteSource(audioSource);
            foreach (var buffer in allAudioBuffers)
                al.DeleteBuffer(buffer);

            cl.Dispose();
            pipeline.Dispose();
            vertexShader.Dispose();
            fragmentShader.Dispose();
            resourceLayout.Dispose();
            resourceSet.Dispose();
            yPlane.Dispose();
            uPlane.Dispose();
            vPlane.Dispose();
            yPlaneStaging.Dispose();
            uPlaneStaging.Dispose();
            vPlaneStaging.Dispose();
            decoder.Dispose();
            timer.Stop();
        }
        disposedValue = true;
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private const string VertexShader = @"
#version 450
#extension GL_KHR_vulkan_glsl: enable

layout(location = 0) out vec2 fsin_uv;

void main()
{
    gl_Position = vec4(
        (gl_VertexIndex % 2) > 0 ? 1 : -1,
        (gl_VertexIndex / 2) > 0 ? 1 : -1,
        0,
        1);
    fsin_uv = (gl_Position.xy * vec2(1, -1)) / 2 + 0.5;
}
";

    private const string FragmentShader = @"
#version 450
#extension GL_KHR_vulkan_glsl: enable

layout(location = 0) in vec2 fsin_uv;

layout(location = 0) out vec4 fsout_color;

layout(set = 0, binding = 0) uniform texture2D yPlane;
layout(set = 0, binding = 1) uniform texture2D uPlane;
layout(set = 0, binding = 2) uniform texture2D vPlane;
layout(set = 0, binding = 3) uniform sampler yuvSampler;

void main()
{
    float y = texture(sampler2D(yPlane, yuvSampler), fsin_uv).r;
    float pb = texture(sampler2D(uPlane, yuvSampler), fsin_uv).r - 0.5;
    float pr = texture(sampler2D(vPlane, yuvSampler), fsin_uv).r - 0.5;

    float kr = 0.299; // BT.601
    float kg = 0.587;
    float kb = 0.114;

    float r = y + (2 - 2 * kr) * pr;
    float g = y - (kb / kg) * (2 - 2 * kb) * pb - (kr / kg) * (2 - 2 * kr) * pr;
    float b = y + (2 - 2 * kb) * pb;

    fsout_color = vec4(r, g, b, 1);
}
";
}
