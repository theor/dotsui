using System;
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
        public Vector2 Position;
        public Vector2 Size = Vector2.one;
        private Entity _entity;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            _entity = entity;
            dstManager.AddComponentData(entity, new UiRenderBounds{ Value = MakeData() });
        }

        private AABB MakeData()
        {
            return new AABB{Center = (Vector3)(Position+Size/2), Extents = (Vector3)Size/2};
        }

        private void Update()
        {
            var bounds = World.Active.EntityManager.GetComponentData<UiRenderBounds>(_entity);
            var newValue = MakeData();
            if (bounds.Value.Center.Equals(newValue.Center) && bounds.Value.Extents.Equals(newValue.Extents))
                return;
            bounds.Value = newValue;
            World.Active.EntityManager.SetComponentData(_entity, bounds);
        }
    }
}