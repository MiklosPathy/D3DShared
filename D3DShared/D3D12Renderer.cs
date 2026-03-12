using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace D3DShared;

/// <summary>
/// Shared D3D12 rendering infrastructure
/// </summary>
public class D3D12Renderer : IDisposable
{
    public const int FrameCount = 2;

    // Window dimensions
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool ResizePending { get; set; }

    // D3D12 objects
    public ID3D12Device? Device { get; private set; }
    public ID3D12CommandQueue? CommandQueue { get; private set; }
    public IDXGISwapChain3? SwapChain { get; private set; }
    public ID3D12DescriptorHeap? RtvHeap { get; private set; }
    public ID3D12DescriptorHeap? DsvHeap { get; private set; }
    public ID3D12DescriptorHeap? CbvHeap { get; private set; }
    public ID3D12Resource?[] RenderTargets { get; } = new ID3D12Resource[FrameCount];
    public ID3D12Resource? DepthStencil { get; private set; }
    public ID3D12CommandAllocator? CommandAllocator { get; private set; }
    public ID3D12GraphicsCommandList? CommandList { get; private set; }
    public ID3D12RootSignature? RootSignature { get; private set; }
    public ID3D12PipelineState? PipelineState { get; private set; }

    // Synchronization
    public ID3D12Fence? Fence { get; private set; }
    public ulong FenceValue { get; set; } = 1;
    public AutoResetEvent? FenceEvent { get; private set; }
    public int FrameIndex { get; set; }
    public uint RtvDescriptorSize { get; private set; }

    // View/Projection matrices
    public Matrix4x4 View { get; set; }
    public Matrix4x4 Projection { get; set; }

    // Available adapters
    public List<(int Index, string Name)> AvailableAdapters { get; } = new();
    public int SelectedAdapterIndex { get; private set; }

    // Handle to the window
    public IntPtr Hwnd { get; private set; }

    // Standard shader code
    public static readonly string StandardShaderCode = @"
cbuffer ConstantBuffer : register(b0)
{
    float4x4 WorldViewProj;
    float4x4 World;
    float4 Light1Direction;
    float4 Light1Color;
    float4 Light2Direction;
    float4 Light2Color;
    float4 Light3Direction;
    float4 Light3Color;
    float4 ObjectColor;
};

struct VSInput
{
    float3 Position : POSITION;
    float3 Normal : NORMAL;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float3 Normal : NORMAL;
    float3 WorldNormal : TEXCOORD0;
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    output.Position = mul(float4(input.Position, 1.0f), WorldViewProj);
    output.Normal = input.Normal;
    output.WorldNormal = mul(float4(input.Normal, 0.0f), World).xyz;
    return output;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    float3 normal = normalize(input.WorldNormal);

    // Calculate contribution from each light
    float3 lightDir1 = normalize(-Light1Direction.xyz);
    float3 lightDir2 = normalize(-Light2Direction.xyz);
    float3 lightDir3 = normalize(-Light3Direction.xyz);

    float diffuse1 = max(dot(normal, lightDir1), 0.0f);
    float diffuse2 = max(dot(normal, lightDir2), 0.0f);
    float diffuse3 = max(dot(normal, lightDir3), 0.0f);

    float3 lighting = Light1Color.rgb * diffuse1 +
                      Light2Color.rgb * diffuse2 +
                      Light3Color.rgb * diffuse3;

    float ambient = 0.1f;
    float3 finalColor = ObjectColor.rgb * (lighting + ambient);

    return float4(saturate(finalColor), 1.0f);
}
";

    public D3D12Renderer(int width = 1280, int height = 720)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Enumerate available GPU adapters
    /// </summary>
    public void EnumerateAdapters()
    {
        if (AvailableAdapters.Count > 0) return;

        using var factory = DXGI.CreateDXGIFactory2<IDXGIFactory4>(false);
        for (uint i = 0; ; i++)
        {
            if (!factory.EnumAdapters1(i, out IDXGIAdapter1? adapter).Success || adapter == null)
                break;

            var desc = adapter.Description1;
            if ((desc.Flags & AdapterFlags.Software) == 0)
            {
                AvailableAdapters.Add(((int)i, desc.Description));
            }
            adapter.Dispose();
        }
    }

    /// <summary>
    /// Initialize D3D12 with the specified adapter
    /// </summary>
    public void Initialize(IntPtr hwnd, int adapterIndex = 0)
    {
        Hwnd = hwnd;

#if DEBUG
        if (D3D12.D3D12GetDebugInterface(out ID3D12Debug? debugInterface).Success && debugInterface != null)
        {
            debugInterface.EnableDebugLayer();
            debugInterface.Dispose();
        }
#endif

        using var factory = DXGI.CreateDXGIFactory2<IDXGIFactory4>(false);

        // Enumerate adapters if not done yet
        if (AvailableAdapters.Count == 0)
        {
            EnumerateAdapters();
        }

        SelectedAdapterIndex = adapterIndex;

        // Create device with selected adapter
        IDXGIAdapter1? selectedAdapter = null;
        if (AvailableAdapters.Count > 0 && adapterIndex < AvailableAdapters.Count)
        {
            uint realIndex = (uint)AvailableAdapters[adapterIndex].Index;
            factory.EnumAdapters1(realIndex, out selectedAdapter);
        }

        D3D12.D3D12CreateDevice(selectedAdapter, FeatureLevel.Level_11_0, out ID3D12Device? device).CheckError();
        Device = device;
        selectedAdapter?.Dispose();

        var queueDesc = new CommandQueueDescription(CommandListType.Direct);
        CommandQueue = Device!.CreateCommandQueue(queueDesc);

        var swapChainDesc = new SwapChainDescription1
        {
            Width = (uint)Width,
            Height = (uint)Height,
            Format = Format.R8G8B8A8_UNorm,
            Stereo = false,
            SampleDescription = new SampleDescription(1, 0),
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = FrameCount,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Unspecified
        };

        using var swapChain1 = factory.CreateSwapChainForHwnd(CommandQueue, hwnd, swapChainDesc);
        SwapChain = swapChain1.QueryInterface<IDXGISwapChain3>();
        FrameIndex = (int)SwapChain.CurrentBackBufferIndex;

        CreateDescriptorHeaps();
        CreateRenderTargets();
        CreateDepthStencil();

        CommandAllocator = Device.CreateCommandAllocator(CommandListType.Direct);

        CreateRootSignature();
        CreatePipelineState();

        CommandList = Device.CreateCommandList<ID3D12GraphicsCommandList>(CommandListType.Direct, CommandAllocator, PipelineState);
        CommandList.Close();

        // Set up view and projection
        View = Matrix4x4.CreateLookAt(new Vector3(0, 0, -5), Vector3.Zero, Vector3.UnitY);
        Projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, (float)Width / Height, 0.1f, 100f);

        Fence = Device.CreateFence(0);
        FenceValue = 1;
        FenceEvent = new AutoResetEvent(false);

        WaitForPreviousFrame();
    }

    private void CreateDescriptorHeaps()
    {
        var rtvHeapDesc = new DescriptorHeapDescription(DescriptorHeapType.RenderTargetView, FrameCount, DescriptorHeapFlags.None);
        RtvHeap = Device!.CreateDescriptorHeap(rtvHeapDesc);
        RtvDescriptorSize = Device.GetDescriptorHandleIncrementSize(DescriptorHeapType.RenderTargetView);

        var dsvHeapDesc = new DescriptorHeapDescription(DescriptorHeapType.DepthStencilView, 1, DescriptorHeapFlags.None);
        DsvHeap = Device.CreateDescriptorHeap(dsvHeapDesc);

        var cbvHeapDesc = new DescriptorHeapDescription(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView, 1, DescriptorHeapFlags.ShaderVisible);
        CbvHeap = Device.CreateDescriptorHeap(cbvHeapDesc);
    }

    private void CreateRenderTargets()
    {
        var rtvHandle = RtvHeap!.GetCPUDescriptorHandleForHeapStart();
        for (int i = 0; i < FrameCount; i++)
        {
            RenderTargets[i] = SwapChain!.GetBuffer<ID3D12Resource>((uint)i);
            Device!.CreateRenderTargetView(RenderTargets[i], null, rtvHandle);
            rtvHandle = rtvHandle.Offset(1, RtvDescriptorSize);
        }
    }

    private void CreateDepthStencil()
    {
        var depthDesc = new ResourceDescription
        {
            Dimension = ResourceDimension.Texture2D,
            Alignment = 0,
            Width = (ulong)Width,
            Height = (uint)Height,
            DepthOrArraySize = 1,
            MipLevels = 1,
            Format = Format.D32_Float,
            SampleDescription = new SampleDescription(1, 0),
            Layout = TextureLayout.Unknown,
            Flags = ResourceFlags.AllowDepthStencil
        };

        var clearValue = new ClearValue(Format.D32_Float, new DepthStencilValue(1.0f, 0));
        DepthStencil = Device!.CreateCommittedResource(
            new HeapProperties(HeapType.Default),
            HeapFlags.None,
            depthDesc,
            ResourceStates.DepthWrite,
            clearValue);

        var dsvDesc = new DepthStencilViewDescription
        {
            Format = Format.D32_Float,
            ViewDimension = DepthStencilViewDimension.Texture2D
        };
        Device.CreateDepthStencilView(DepthStencil, dsvDesc, DsvHeap!.GetCPUDescriptorHandleForHeapStart());
    }

    private void CreateRootSignature()
    {
        var rootParameters = new RootParameter1[]
        {
            new RootParameter1(RootParameterType.ConstantBufferView, new RootDescriptor1(0, 0), ShaderVisibility.All),
            new RootParameter1(RootParameterType.ConstantBufferView, new RootDescriptor1(1, 0), ShaderVisibility.All)
        };

        var rootSignatureDesc = new RootSignatureDescription1(
            RootSignatureFlags.AllowInputAssemblerInputLayout,
            rootParameters);

        RootSignature = Device!.CreateRootSignature(new VersionedRootSignatureDescription(rootSignatureDesc));
    }

    private void CreatePipelineState()
    {
        var vertexShader = Compiler.Compile(StandardShaderCode, "VSMain", "main", "vs_5_0");
        var pixelShader = Compiler.Compile(StandardShaderCode, "PSMain", "main", "ps_5_0");

        var inputElements = new InputElementDescription[]
        {
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
            new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0, InputClassification.PerVertexData, 0)
        };

        var psoDesc = new GraphicsPipelineStateDescription
        {
            RootSignature = RootSignature,
            VertexShader = vertexShader,
            PixelShader = pixelShader,
            InputLayout = new InputLayoutDescription(inputElements),
            SampleMask = uint.MaxValue,
            PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
            RasterizerState = RasterizerDescription.CullNone,
            BlendState = BlendDescription.Opaque,
            DepthStencilState = DepthStencilDescription.Default,
            DepthStencilFormat = Format.D32_Float,
            SampleDescription = new SampleDescription(1, 0)
        };
        psoDesc.RenderTargetFormats[0] = Format.R8G8B8A8_UNorm;

        PipelineState = Device!.CreateGraphicsPipelineState(psoDesc);
    }

    /// <summary>
    /// Create a constant buffer for rendering
    /// </summary>
    public ID3D12Resource CreateConstantBuffer()
    {
        var cbSize = (uint)((Unsafe.SizeOf<ConstantBufferData>() + 255) & ~255);
        return Device!.CreateCommittedResource(
            new HeapProperties(HeapType.Upload),
            HeapFlags.None,
            ResourceDescription.Buffer(cbSize),
            ResourceStates.GenericRead);
    }

    /// <summary>
    /// Create a vertex buffer from vertex data
    /// </summary>
    public unsafe (ID3D12Resource Buffer, VertexBufferView View) CreateVertexBuffer(Vertex[] vertices)
    {
        var bufferSize = (uint)(vertices.Length * Unsafe.SizeOf<Vertex>());
        var buffer = Device!.CreateCommittedResource(
            new HeapProperties(HeapType.Upload),
            HeapFlags.None,
            ResourceDescription.Buffer(bufferSize),
            ResourceStates.GenericRead);

        void* pData = null;
        buffer.Map(0, &pData);
        fixed (Vertex* src = vertices)
        {
            Unsafe.CopyBlock(pData, src, bufferSize);
        }
        buffer.Unmap(0);

        var view = new VertexBufferView(buffer.GPUVirtualAddress, bufferSize, (uint)Unsafe.SizeOf<Vertex>());
        return (buffer, view);
    }

    /// <summary>
    /// Create an index buffer from index data
    /// </summary>
    public unsafe (ID3D12Resource Buffer, IndexBufferView View) CreateIndexBuffer(uint[] indices)
    {
        var bufferSize = (uint)(indices.Length * sizeof(uint));
        var buffer = Device!.CreateCommittedResource(
            new HeapProperties(HeapType.Upload),
            HeapFlags.None,
            ResourceDescription.Buffer(bufferSize),
            ResourceStates.GenericRead);

        void* pData = null;
        buffer.Map(0, &pData);
        fixed (uint* src = indices)
        {
            Unsafe.CopyBlock(pData, src, bufferSize);
        }
        buffer.Unmap(0);

        var view = new IndexBufferView(buffer.GPUVirtualAddress, bufferSize, Format.R32_UInt);
        return (buffer, view);
    }

    /// <summary>
    /// Default front lighting - lights come from front-top (same direction as typical camera)
    /// These directions are passed to shader which negates them, so positive Z = light from front
    /// </summary>
    public static class DefaultLighting
    {
        /// <summary>Front-top-right key light</summary>
        public static readonly Vector3 Light1Direction = new(0.3f, -0.5f, 1.0f);

        /// <summary>Front-top-left fill light</summary>
        public static readonly Vector3 Light2Direction = new(-0.3f, -0.3f, 1.0f);

        /// <summary>Key light color (bright white)</summary>
        public static readonly Vector4 Light1Color = new(1.0f, 1.0f, 1.0f, 1.0f);

        /// <summary>Fill light color (slightly dimmer)</summary>
        public static readonly Vector4 Light2Color = new(0.7f, 0.7f, 0.7f, 1.0f);
    }

    /// <summary>
    /// Update constant buffer with transformation and default front lighting
    /// </summary>
    public void UpdateConstantBuffer(ID3D12Resource buffer, Matrix4x4 world, Vector4 objectColor)
    {
        UpdateConstantBuffer(buffer, world, objectColor,
            DefaultLighting.Light1Direction, DefaultLighting.Light2Direction);
    }

    /// <summary>
    /// Update constant buffer with transformation and custom lighting directions
    /// </summary>
    public void UpdateConstantBuffer(ID3D12Resource buffer, Matrix4x4 world, Vector4 objectColor,
                                     Vector3 light1Dir, Vector3 light2Dir)
    {
        UpdateConstantBuffer(buffer, world, objectColor,
            light1Dir, DefaultLighting.Light1Color,
            light2Dir, DefaultLighting.Light2Color);
    }

    /// <summary>
    /// Update constant buffer with full lighting control
    /// </summary>
    public void UpdateConstantBuffer(ID3D12Resource buffer, Matrix4x4 world, Vector4 objectColor,
                                     Vector3 light1Dir, Vector4 light1Color,
                                     Vector3 light2Dir, Vector4 light2Color)
    {
        var worldViewProj = world * View * Projection;
        var cbData = new ConstantBufferData
        {
            WorldViewProj = Matrix4x4.Transpose(worldViewProj),
            World = Matrix4x4.Transpose(world),
            Light1Direction = new Vector4(Vector3.Normalize(light1Dir), 0),
            Light1Color = light1Color,
            Light2Direction = new Vector4(Vector3.Normalize(light2Dir), 0),
            Light2Color = light2Color,
            Light3Direction = new Vector4(0, 0, 0, 0),
            Light3Color = new Vector4(0.0f, 0.0f, 0.0f, 0.0f),
            ObjectColor = objectColor
        };
        buffer.SetData(in cbData);
    }

    /// <summary>
    /// Resize swap chain buffers
    /// </summary>
    public void ResizeBuffers(int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        Width = width;
        Height = height;

        WaitForPreviousFrame();

        // Release render targets
        for (int i = 0; i < FrameCount; i++)
        {
            RenderTargets[i]?.Dispose();
            RenderTargets[i] = null;
        }

        // Release depth buffer
        DepthStencil?.Dispose();
        DepthStencil = null;

        // Resize swap chain
        SwapChain!.ResizeBuffers(FrameCount, (uint)Width, (uint)Height, Format.R8G8B8A8_UNorm, SwapChainFlags.None);
        FrameIndex = (int)SwapChain.CurrentBackBufferIndex;

        // Recreate render targets
        CreateRenderTargets();
        CreateDepthStencil();

        // Update projection matrix for new aspect ratio
        Projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4, (float)Width / Height, 0.1f, 100f);

        ResizePending = false;
    }

    /// <summary>
    /// Begin a render frame
    /// </summary>
    public void BeginFrame(Color4 clearColor)
    {
        CommandAllocator!.Reset();
        CommandList!.Reset(CommandAllocator, PipelineState);

        CommandList.SetGraphicsRootSignature(RootSignature);
        CommandList.RSSetViewport(new Viewport(0, 0, Width, Height, 0, 1));
        CommandList.RSSetScissorRect(new RectI(0, 0, Width, Height));

        CommandList.ResourceBarrier(new ResourceBarrier(
            new ResourceTransitionBarrier(RenderTargets[FrameIndex]!, ResourceStates.Present, ResourceStates.RenderTarget)));

        var rtvHandle = RtvHeap!.GetCPUDescriptorHandleForHeapStart().Offset(FrameIndex, RtvDescriptorSize);
        var dsvHandle = DsvHeap!.GetCPUDescriptorHandleForHeapStart();

        CommandList.OMSetRenderTargets(rtvHandle, dsvHandle);
        CommandList.ClearRenderTargetView(rtvHandle, clearColor);
        CommandList.ClearDepthStencilView(dsvHandle, ClearFlags.Depth, 1.0f, 0);

        CommandList.IASetPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList);
    }

    /// <summary>
    /// End a render frame and present
    /// </summary>
    public void EndFrame()
    {
        CommandList!.ResourceBarrier(new ResourceBarrier(
            new ResourceTransitionBarrier(RenderTargets[FrameIndex]!, ResourceStates.RenderTarget, ResourceStates.Present)));

        CommandList.Close();

        CommandQueue!.ExecuteCommandList(CommandList);
        SwapChain!.Present(1, PresentFlags.None);

        WaitForPreviousFrame();
    }

    /// <summary>
    /// Wait for previous frame to complete
    /// </summary>
    public void WaitForPreviousFrame()
    {
        ulong fenceToWaitFor = FenceValue;
        CommandQueue!.Signal(Fence, fenceToWaitFor);
        FenceValue++;

        if (Fence!.CompletedValue < fenceToWaitFor)
        {
            Fence.SetEventOnCompletion(fenceToWaitFor, FenceEvent!);
            FenceEvent!.WaitOne();
        }

        FrameIndex = (int)SwapChain!.CurrentBackBufferIndex;
    }

    /// <summary>
    /// Switch to a different GPU adapter
    /// </summary>
    public void SwitchAdapter(int newAdapterIndex, Action? onBeforeReinit = null, Action? onAfterReinit = null)
    {
        if (newAdapterIndex == SelectedAdapterIndex || newAdapterIndex < 0 || newAdapterIndex >= AvailableAdapters.Count)
            return;

        WaitForPreviousFrame();

        onBeforeReinit?.Invoke();

        // Dispose core resources
        DisposeCore();

        // Reinitialize with new adapter
        InitializeCore(newAdapterIndex);

        onAfterReinit?.Invoke();
    }

    private void InitializeCore(int adapterIndex)
    {
#if DEBUG
        if (D3D12.D3D12GetDebugInterface(out ID3D12Debug? debugInterface).Success && debugInterface != null)
        {
            debugInterface.EnableDebugLayer();
            debugInterface.Dispose();
        }
#endif

        using var factory = DXGI.CreateDXGIFactory2<IDXGIFactory4>(false);

        SelectedAdapterIndex = adapterIndex;

        IDXGIAdapter1? selectedAdapter = null;
        if (AvailableAdapters.Count > 0 && adapterIndex < AvailableAdapters.Count)
        {
            uint realIndex = (uint)AvailableAdapters[adapterIndex].Index;
            factory.EnumAdapters1(realIndex, out selectedAdapter);
        }

        D3D12.D3D12CreateDevice(selectedAdapter, FeatureLevel.Level_11_0, out ID3D12Device? device).CheckError();
        Device = device;
        selectedAdapter?.Dispose();

        var queueDesc = new CommandQueueDescription(CommandListType.Direct);
        CommandQueue = Device!.CreateCommandQueue(queueDesc);

        var swapChainDesc = new SwapChainDescription1
        {
            Width = (uint)Width,
            Height = (uint)Height,
            Format = Format.R8G8B8A8_UNorm,
            Stereo = false,
            SampleDescription = new SampleDescription(1, 0),
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = FrameCount,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Unspecified
        };

        using var swapChain1 = factory.CreateSwapChainForHwnd(CommandQueue, Hwnd, swapChainDesc);
        SwapChain = swapChain1.QueryInterface<IDXGISwapChain3>();
        FrameIndex = (int)SwapChain.CurrentBackBufferIndex;

        CreateDescriptorHeaps();
        CreateRenderTargets();
        CreateDepthStencil();

        CommandAllocator = Device.CreateCommandAllocator(CommandListType.Direct);
        CreateRootSignature();
        CreatePipelineState();

        CommandList = Device.CreateCommandList<ID3D12GraphicsCommandList>(CommandListType.Direct, CommandAllocator, PipelineState);
        CommandList.Close();

        Fence = Device.CreateFence(0);
        FenceValue = 1;
        FenceEvent = new AutoResetEvent(false);

        WaitForPreviousFrame();
    }

    private void DisposeCore()
    {
        Fence?.Dispose();
        PipelineState?.Dispose();
        RootSignature?.Dispose();
        CommandList?.Dispose();
        CommandAllocator?.Dispose();
        DepthStencil?.Dispose();
        CbvHeap?.Dispose();
        DsvHeap?.Dispose();
        RtvHeap?.Dispose();
        foreach (var rt in RenderTargets) rt?.Dispose();
        for (int i = 0; i < FrameCount; i++) RenderTargets[i] = null;
        SwapChain?.Dispose();
        CommandQueue?.Dispose();
        Device?.Dispose();
        FenceEvent?.Dispose();
    }

    public void Dispose()
    {
        WaitForPreviousFrame();
        DisposeCore();
    }
}
