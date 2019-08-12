using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public struct UiRenderBounds : IComponentData
{
    public AABB Value; // in pixels, screen-space, 0,0 at the bottom left
}

struct Background : IComponentData
{
    public float4 backgroundColor;
}

[AlwaysUpdateSystem]
public class UiRenderer : ComponentSystem
{
    [MenuItem("DOTSUI/Dump GOID")]
    static void DumpGOID()
    {
        var goid = GlobalObjectId.GetGlobalObjectIdSlow(Selection.activeObject);
        Debug.Log(goid);
    }

    private const string UIMaterialGOID = "GlobalObjectId_V1-3-7ce14f6751d9ac841a9145542438a2d9-2100000-0";
    private Camera m_Camera;
    private Mesh mesh;
    private EntityQuery m_RenderQuery;
    private int _colorPropertyId;
    private Material material;
    private TextGenerator _gen;

    protected override void OnCreate()
    {
        mesh = CreateQuad();
        m_RenderQuery = GetEntityQuery(
            ComponentType.ReadOnly<UiRenderBounds>()
        );
        GlobalObjectId.TryParse(UIMaterialGOID, out var goid);
        material = (Material)GlobalObjectId.GlobalObjectIdentifierToObjectSlow(goid);
        _colorPropertyId = Shader.PropertyToID("_Color");
        _gen = new TextGenerator();
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

        var backgrounds = GetComponentDataFromEntity<Background>();
        
        var block = new MaterialPropertyBlock();
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
            
            block.SetColor(_colorPropertyId, backgrounds.Exists(e) ? (Color)(Vector4)backgrounds[e].backgroundColor : Color.white);
            Graphics.DrawMesh(mesh, matrix4X4, material, 0, null, 0, block);
        });
        
//        DrawText(block, pixelScale);
    }

    private void DrawText(MaterialPropertyBlock block, Matrix4x4 pixelScale)
    {
        var tm = new Mesh();
        tm.vertices = _gen.verts.Select(v => v.position).ToArray();
        tm.colors32 = _gen.verts.Select(v => v.color).ToArray();
        tm.uv = _gen.verts.Select(v => v.uv0).ToArray();

        int characterCount = _gen.vertexCount / 4;
        int[] tempIndices = new int[characterCount * 6];
        for (int i = 0; i < characterCount; ++i)
        {
            int vertIndexStart = i * 4;
            int trianglesIndexStart = i * 6;
            tempIndices[trianglesIndexStart++] = vertIndexStart;
            tempIndices[trianglesIndexStart++] = vertIndexStart + 1;
            tempIndices[trianglesIndexStart++] = vertIndexStart + 2;
            tempIndices[trianglesIndexStart++] = vertIndexStart;
            tempIndices[trianglesIndexStart++] = vertIndexStart + 2;
            tempIndices[trianglesIndexStart] = vertIndexStart + 3;
        }

        tm.triangles = tempIndices;
        tm.RecalculateBounds();

        block.SetColor(_colorPropertyId, Color.white);

        _gen.Invalidate();
        if (!_gen.PopulateWithErrors("Test Label", GetGenerationSettings(Vector2.one * 500), null))
            Debug.LogError("Error during text gen");
//        Debug.Log("populate:" + populateWithErrors + _gen.vertexCount);
//        Debug.Log(String.Join(",", _gen.verts.Select(c => c.position.ToString())));
        Font.GetDefault()
            .RequestCharactersInTexture(
                " !\"#$%&\'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");
        Graphics.DrawMesh(tm, Matrix4x4.identity * pixelScale, Font.GetDefault().material, 0, null, 0, block);
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

    public TextGenerationSettings GetGenerationSettings(Vector2 extents)
    {
        var settings = new TextGenerationSettings();
        var data = FontData.defaultFontData;
        data.font = Font.GetDefault();

        settings.generationExtents = extents;
//        if (data.font != null && data.font.dynamic)
//        {
//            settings.fontSize = data.fontSize;
//            settings.resizeTextMinSize = data.minSize;
//            settings.resizeTextMaxSize = data.maxSize;
//        }

        // Other settings
        settings.textAnchor = data.alignment;
        settings.alignByGeometry = data.alignByGeometry;
        settings.scaleFactor = 1;
        settings.color = Color.white;
        settings.font = data.font;
        settings.pivot = Vector2.zero;
        settings.richText = data.richText;
        settings.lineSpacing = data.lineSpacing;
        settings.fontStyle = data.fontStyle;
        settings.resizeTextForBestFit = data.bestFit;
        settings.updateBounds = false;
        settings.horizontalOverflow = data.horizontalOverflow;
        settings.verticalOverflow = data.verticalOverflow;

        return settings;
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