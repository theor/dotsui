using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Yoga;

[UpdateBefore(typeof(UiRenderer))]
class LayoutSystem : ComponentSystem
{
    private Dictionary<Entity, YogaNode> nodes = new Dictionary<Entity, YogaNode>();
    private YogaNode _root;
    protected override void OnCreate()
    {
        _root = new YogaNode(YogaConfig.Default)
        {
            Data = "root",
            Display = YogaDisplay.Flex,
            FlexDirection = YogaFlexDirection.Column,
            
        };
    }

    protected override void OnUpdate()
    {
        Entities.ForEach((Entity e, ref UiRenderBounds uiRenderBounds) =>
        {
            if(!nodes.TryGetValue(e, out var yn))
            {
                var yogaNode = new YogaNode(YogaConfig.Default)
                {
                    Data = e,
                    Margin = YogaValue.Point(10),
                    Flex = 1,
                };
                nodes.Add(e, yogaNode);
                _root.AddChild(yogaNode);
            }
        });
        
//        YogaNode n = new YogaNode(YogaConfig.Default)
//        {
//            Data = "root",
//            Display = YogaDisplay.Flex,
//            FlexDirection = YogaFlexDirection.Column,
//        };
//        n.Width = YogaValue.Point(400);
//        n.Height = YogaValue.Point(200);
//        n.AddChild(new YogaNode(YogaConfig.Default)
//        {
//            Data = "child",
//            Margin = YogaValue.Point(10),
//            Flex = 1,
////            Width = YogaValue.Percent(100),
////            Height = YogaValue.Percent(100),
//        });n.AddChild(new YogaNode(YogaConfig.Default)
//        {
//            Data = "child",
//            Margin = YogaValue.Point(10),
//            Flex = 1,
////            Width = YogaValue.Percent(100),
////            Height = YogaValue.Percent(100),
//        });
        var r = Screen.safeArea;
        _root.Width = YogaValue.Point(r.width);
        _root.Height = YogaValue.Point(r.height);
        _root.CalculateLayout(Single.NaN, Single.NaN);
//        Dump(_root, 0);
        Entities.ForEach((Entity e, ref UiRenderBounds uiRenderBounds) =>
        {
            var yn = nodes[e];
            var layout = yn.GetLayoutRect();
            uiRenderBounds.Value.Center = new float3(layout.center.x, layout.center.y, 0);
            uiRenderBounds.Value.Extents = new float3(layout.width / 2, layout.height / 2, 0);
        });

        void Dump(YogaNode yn, int indent)
        {
            Debug.Log($"{new string(' ', 2 * indent)}{yn.Data}: {yn.GetLayoutRect()}");
            foreach (YogaNode child in yn)
            {
                Dump(child, indent + 1);
            }
        }
    }
}


static class Ext
{
    public static Rect GetLayoutRect(this YogaNode n)
    {
        
        Rect newRect = new Rect(n.LayoutX, n.LayoutY, n.LayoutWidth, n.LayoutHeight);
        return newRect;
    }
}