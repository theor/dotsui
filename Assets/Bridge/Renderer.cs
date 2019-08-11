using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.UIR;
using UnityEngine.UIElements.UIR.Implementation;
using UnityEngine.Yoga;

public struct UiRenderBounds : IComponentData
{
    public AABB Value;
}

[AlwaysUpdateSystem]
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(RenderBoundsUpdateSystem))]
[UpdateBefore(typeof(LodRequirementsUpdateSystem))]
public class UiRenderer : JobComponentSystem
{
    private EntityQuery m_AddMissingRenderBounds;
    private EntityQuery m_ItemsQuery;
    private EntityQuery m_ApplyLayoutQuery;

    private Camera m_Camera;

//
    protected override void OnCreate()
    {
        m_Camera = Camera.main;
        m_ItemsQuery = GetEntityQuery(
            ComponentType.ReadOnly<UiRenderBounds>(),
            ComponentType.ReadWrite<RenderBounds>()
        );
        m_AddMissingRenderBounds = GetEntityQuery(
            ComponentType.ReadOnly<UiRenderBounds>(),
            ComponentType.Exclude<RenderBounds>()
        );
        m_ApplyLayoutQuery = GetEntityQuery(
            ComponentType.ReadOnly<UiRenderBounds>(),
            ComponentType.ReadWrite<LocalToWorld>()
        );
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        EntityManager.AddComponent<RenderBounds>(m_AddMissingRenderBounds);
        inputDeps = new UpdateFromUiBoundsJob().Schedule(m_ItemsQuery, inputDeps);
        if (!m_Camera)
            m_Camera = Camera.main;
        var v2 = m_Camera.ScreenToWorldPoint(Vector3.forward * 10);
        var m = Matrix4x4.Translate(v2) * Matrix4x4.Rotate(m_Camera.transform.localRotation);
        inputDeps = new MapToCameraView {m = m}.Schedule(m_ApplyLayoutQuery, inputDeps);
        return inputDeps;
    }

    public struct UpdateFromUiBoundsJob : IJobForEach<RenderBounds, UiRenderBounds>
    {
        public void Execute(ref RenderBounds renderBounds, [ReadOnly] ref UiRenderBounds uiRenderBounds)
        {
//                renderBounds.Value = uiRenderBounds.Value;
            var i = 10;
            renderBounds.Value.Extents = new float3(i, i, i);
        }
    }

    public struct MapToCameraView : IJobForEach<LocalToWorld, UiRenderBounds>
    {
        public Matrix4x4 m;

        public void Execute(ref LocalToWorld ltw, [ReadOnly] ref UiRenderBounds uiRenderBounds)
        {
            ltw.Value = m * Matrix4x4.Translate(uiRenderBounds.Value.Center - uiRenderBounds.Value.Extents) *
                        Matrix4x4.Scale(uiRenderBounds.Value.Size * 0.1f);
        }
    }
}

//public class Renderer : ComponentSystem
//{
//    private UIRenderDevice _device;
//    private UIRAtlasManager _uirAtlasManager;
//    private VectorImageManager _vectorImageMan;
//
//    protected override void OnUpdate()
//    {
//        var viewport = new Rect(0, 0, 400, 400);
//        var proj = Matrix4x4.Ortho(viewport.xMin, viewport.xMax, viewport.yMax, viewport.yMin, -1f, 1f);
//        Exception e = new Exception();
//        var cmd = new RenderChainCommand();
//        _device.DrawChain(cmd, viewport, proj, (Texture) this._uirAtlasManager?.atlas, (Texture) this._vectorImageMan?.atlas, 1, ref e);
//    }
//
//    protected override void OnCreate()
//    {
//        _uirAtlasManager = new UIRAtlasManager(64);
//        _vectorImageMan = new VectorImageManager(_uirAtlasManager);
//        _device = new UIRenderDevice(RenderEvents.ResolveShader(null), 0U, 0U, 1024U, UIRenderDevice.DrawingModes.FlipY,
//            1024);
//    }
//
//    protected override void OnDestroy()
//    {
//        _uirAtlasManager.Dispose();
//        _vectorImageMan.Dispose();
//        _device.Dispose();
//    }
//}