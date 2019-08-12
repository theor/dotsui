using System;
using System.Collections.Generic;
using System.Linq;
using Bridge;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;
using UnityEngine.UIElements.StyleSheets;
using SelectorMatchRecord = Bridge.SelectorMatchRecord;
using StyleMatchingContext = Bridge.StyleMatchingContext;

[AlwaysUpdateSystem]
public class StyleSystem : ComponentSystem
{
    private StyleSheet s;

    protected override void OnCreate()
    {
        base.OnCreate();
    }

    protected override void OnUpdate()
    {
        List<Bridge.SelectorMatchRecord> l = new List<Bridge.SelectorMatchRecord>();
        Bridge.StyleMatchingContext ctx = new Bridge.StyleMatchingContext((element, info) =>
        {
            Debug.Log($"Matches: {element}");
        });
        Assert.IsTrue(GlobalObjectId.TryParse("GlobalObjectId_V1-1-e6e2a2a07016443409378c352d64f325-7433441132597879392-0", out var ssgoid));
        ctx.styleSheetStack = new List<StyleSheet>{(StyleSheet)GlobalObjectId.GlobalObjectIdentifierToObjectSlow(ssgoid)};

        var backgrounds = GetComponentDataFromEntity<Background>();
        
        Entities.ForEach((Entity e, ref UiRenderBounds bounds) =>
        {
            l.Clear();
            ctx.currentElement = new UiEntity(e, PostUpdateCommands, backgrounds);
            Bridge.StyleSelectorHelper.FindMatches(ctx, l);
            if (l.Count > 0)
            {
                string Rules(StyleRule info) => String.Join(",", info.properties.Select(p => $"{p.name}: {p.values}"));
                Debug.Log(String.Join("\r\n", l.Select(x => $"{x.complexSelector} {Rules(x.complexSelector.rule)}")));
                foreach (var record in l)
                {
                    Apply(ctx, record);
                }
            }
        });
    }

    private void Apply(StyleMatchingContext ctx, SelectorMatchRecord record)
    {
        foreach (StyleProperty property in record.complexSelector.rule.properties)
        {
            switch (StyleSheetCache.GetPropertyIDFromName(property.name))
            {
                case StylePropertyID.BackgroundColor:
                    ctx.currentElement.backgroundColor = (Vector4)record.sheet.ReadColor(property.values[0]);
                    break;
            }
        }
    }
}