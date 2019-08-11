using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public struct UiRenderBounds : IComponentData
{
    public AABB Value; // in pixels, screen-space, 0,0 at the bottom left
}

//public struct UiRenderData : IComponentData
//{
//    
//}

[AlwaysUpdateSystem]
public class Renderer : ComponentSystem
{
    private Camera m_Camera;
    private Mesh mesh;
    private EntityQuery m_RenderQuery;

    protected override void OnCreate()
    {
        mesh = CreateQuad();
        m_RenderQuery = GetEntityQuery(
            ComponentType.ReadOnly<UiRenderBounds>()
        );
    }

    public static Mesh CreateQuad()
    {
        var mesh = new Mesh();
        mesh.vertices = new Vector3[4]
        {
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(1, 1, 0)
        };
        var tris = new int[6]
        {
            // lower left triangle
            0, 2, 1,
            // upper right triangle
            2, 3, 1
        };
        mesh.triangles = tris;

        var normals = new Vector3[4]
        {
            -Vector3.forward,
            -Vector3.forward,
            -Vector3.forward,
            -Vector3.forward
        };
        mesh.normals = normals;

        var uv = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };
        mesh.uv = uv;
        mesh.RecalculateBounds();
        return mesh;
    }

    protected override void OnUpdate()
    {
        if (!m_Camera)
            m_Camera = Camera.main;
//        Debug.Log($"pix {m_Camera.pixelRect}");
        var v2 = m_Camera.ScreenToWorldPoint(Vector3.forward * 10);
        var m = Matrix4x4.Translate(v2) * Matrix4x4.Rotate(m_Camera.transform.localRotation);
        // height -> 2
        // 1 px = 2 / height
        var px = 2.0f / m_Camera.pixelHeight;
        var pixelScale = Matrix4x4.Scale(new Vector3(px, px, px));
        
        
        Entities.With(m_RenderQuery).ForEach((Entity e, ref UiRenderBounds bounds) =>
        {
            var translation = bounds.Value.Center - bounds.Value.Extents;
            translation.y = m_Camera.pixelHeight - translation.y - bounds.Value.Size.y; // put y=0 at the top
            translation = new float3(
                bounds.Value.Center.x - bounds.Value.Extents.x,
                m_Camera.pixelHeight - bounds.Value.Center.y - bounds.Value.Extents.y,
                
                0
            );
            var matrix4X4 = m *
                            Matrix4x4.Translate(translation * px) *
                            Matrix4x4.Scale(bounds.Value.Size) *
                            pixelScale;
            Graphics.DrawMesh(mesh, matrix4X4, Material.GetDefaultMaterial(), 0);
        });
    }
}

[AlwaysUpdateSystem]
[DisableAutoCreation]
public class UiRenderer : JobComponentSystem
{
    private EntityQuery m_ItemsQuery;
//    private EntityQuery m_AddMissingRenderBounds;
//    private EntityQuery m_ApplyLayoutQuery;

    private Camera m_Camera;

//
    protected override void OnCreate()
    {
        m_Camera = Camera.main;
        m_ItemsQuery = GetEntityQuery(
            ComponentType.ReadOnly<UiRenderBounds>()
        );
//        m_AddMissingRenderBounds = GetEntityQuery(
//            ComponentType.ReadOnly<UiRenderBounds>(),
//            ComponentType.Exclude<RenderBounds>()
//        );
//        m_ApplyLayoutQuery = GetEntityQuery(
//            ComponentType.ReadOnly<UiRenderBounds>(),
//            ComponentType.ReadWrite<LocalToWorld>()
//        );
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
//        EntityManager.AddComponent<RenderBounds>(m_AddMissingRenderBounds);
        inputDeps = new UpdateFromUiBoundsJob().Schedule(m_ItemsQuery, inputDeps);
        if (!m_Camera)
            m_Camera = Camera.main;
        var v2 = m_Camera.ScreenToWorldPoint(Vector3.forward * 10);
        var m = Matrix4x4.Translate(v2) * Matrix4x4.Rotate(m_Camera.transform.localRotation);
        inputDeps = new MapToCameraView {m = m}.Schedule(m_ItemsQuery, inputDeps);
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