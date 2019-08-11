using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace DefaultNamespace
{
    class UiItemProxy : MonoBehaviour, IConvertGameObjectToEntity
    {
        public Bounds Bounds;
        private Mesh mesh;
        public Material material;
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddComponentData(entity, new NonUniformScale(){ Value = new float3(Bounds.size.x, Bounds.size.y, 0)});
            dstManager.AddComponent(entity, typeof(CompositeScale));
            dstManager.AddComponentData(entity, new UiRenderBounds{ Value = Bounds.ToAABB() });
            mesh = new Mesh();
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
            dstManager.AddSharedComponentData(entity,new RenderMesh
            {
                material = material,
                mesh = mesh,
                receiveShadows = false,
                castShadows = ShadowCastingMode.Off,
            });
        }
    }
}