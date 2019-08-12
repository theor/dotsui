using System;
using System.Collections.Generic;
using NUnit.Framework.Constraints;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bridge
{
    static class EMExt
    {
        public static T GetOrCreate<T>(this ComponentDataFromEntity<T> comp, EntityCommandBuffer em, Entity e) where T : struct, IComponentData
        {
            if (comp.Exists(e))
                return comp[e];
            em.AddComponent<T>(e);
            return default;
        }
    }
    struct UiEntity
    {
        public override string ToString()
        {
            return $"{nameof(entity)}: {entity}";
        }

        public EntityCommandBuffer em;
        public Entity entity;

        public UiEntity(Entity entity, EntityCommandBuffer ecb, ComponentDataFromEntity<Background> getComponentDataFromEntity)
        {
            this.em = ecb;
            this.entity = entity;
            Backgrounds = getComponentDataFromEntity;
        }

        public IEnumerable<string> classList => throw new NotImplementedException();

        public ComponentDataFromEntity<Background> Backgrounds;
        
        public bool Invalid => entity == Entity.Null;
        public int pseudoStates => throw new NotImplementedException();
        public string typeName => $"E{entity.Index}";// throw new NotImplementedException();
        public string name =>  $"E{entity.Index}";//throw new NotImplementedException();
        public float4 backgroundColor
        {
            get =>  Backgrounds[entity].backgroundColor;
            set
            {
                var background = Backgrounds.GetOrCreate(em, entity);
                background.backgroundColor = (Vector4) value;
                em.SetComponent(entity, background);
            }
        }

        public bool ClassListContains(string value)
        {
            throw new NotImplementedException();
        }

        public UiEntity GetParent()
        {
            return default; // TODO
        }
    }
    internal struct MatchResultInfo
    {
        public readonly bool success;
        public readonly PseudoStates triggerPseudoMask; // what pseudo states contributes to matching this selector
        public readonly PseudoStates dependencyPseudoMask; // what pseudo states if set, would have given a different result

        public MatchResultInfo(bool success, PseudoStates triggerPseudoMask, PseudoStates dependencyPseudoMask)
        {
            this.success = success;
            this.triggerPseudoMask = triggerPseudoMask;
            this.dependencyPseudoMask = dependencyPseudoMask;
        }
    }


    internal class StyleMatchingContext
    {
        public List<StyleSheet> styleSheetStack;
        public StyleVariableContext variableContext;
        public UiEntity currentElement;
        public Action<UiEntity, Bridge.MatchResultInfo> processResult;
        public UnityEngine.UIElements.StyleSheets.InheritedStylesData inheritedStyle;

        public StyleMatchingContext(
            Action<UiEntity, Bridge.MatchResultInfo> processResult)
        {
            this.styleSheetStack = new List<StyleSheet>();
            this.variableContext = StyleVariableContext.none;
            this.currentElement = default;
            this.processResult = processResult;
        }
    }

    // Each struct represents on match for a visual element against a complex
    internal struct SelectorMatchRecord
    {
        public StyleSheet sheet;
        public int styleSheetIndexInStack;
        public StyleComplexSelector complexSelector;

        public SelectorMatchRecord(StyleSheet sheet, int styleSheetIndexInStack) : this()
        {
            this.sheet = sheet;
            this.styleSheetIndexInStack = styleSheetIndexInStack;
        }

        public static int Compare(SelectorMatchRecord a, SelectorMatchRecord b)
        {
            if (a.sheet.isUnityStyleSheet != b.sheet.isUnityStyleSheet)
                return a.sheet.isUnityStyleSheet ? -1 : 1;

            int res = a.complexSelector.specificity.CompareTo(b.complexSelector.specificity);

            if (res == 0)
            {
                res = a.styleSheetIndexInStack.CompareTo(b.styleSheetIndexInStack);
            }

            if (res == 0)
            {
                res = a.complexSelector.orderInStyleSheet.CompareTo(b.complexSelector.orderInStyleSheet);
            }

            return res;
        }
    }

    // Pure functions for the central logic of selector application
    static class StyleSelectorHelper
    {
        public static MatchResultInfo MatchesSelector(UiEntity element, StyleSelector selector)
        {
            bool match = true;

            StyleSelectorPart[] parts = selector.parts;
            int count = parts.Length;

            for (int i = 0; i < count && match; i++)
            {
                switch (parts[i].type)
                {
                    case StyleSelectorType.Wildcard:
                        break;
                    case StyleSelectorType.Class:
                        match = element.ClassListContains(parts[i].value);
                        break;
                    case StyleSelectorType.ID:
                        string value = parts[i].value;
                        match = element.name == value;
                        break;
                    case StyleSelectorType.Type:
                        //TODO: This tests fails to capture instances of sub-classes
                        string value1 = parts[i].value;
                        match = element.typeName == value1;
                        break;
                    case StyleSelectorType.Predicate:
                        UQuery.IVisualPredicateWrapper w = parts[i].tempData as UQuery.IVisualPredicateWrapper;
                        match = w != null && w.Predicate(element);
                        break;
                    case StyleSelectorType.PseudoClass:
                        break;
                    default: // ignore, all errors should have been warned before hand
                        match = false;
                        break;
                }
            }

            int triggerPseudoStateMask = 0;
            int dependencyPseudoMask = 0;

            bool saveMatch = match;

            if (saveMatch  && selector.pseudoStateMask != 0)
            {
                match = (selector.pseudoStateMask & (int)element.pseudoStates) == selector.pseudoStateMask;

                if (match)
                {
                    // the element matches this selector because it has those flags
                    dependencyPseudoMask = selector.pseudoStateMask;
                }
                else
                {
                    // if the element had those flags defined, it would match this selector
                    triggerPseudoStateMask = selector.pseudoStateMask;
                }
            }

            if (saveMatch && selector.negatedPseudoStateMask != 0)
            {
                match &= (selector.negatedPseudoStateMask & ~(int)element.pseudoStates) == selector.negatedPseudoStateMask;

                if (match)
                {
                    // the element matches this selector because it does not have those flags
                    triggerPseudoStateMask |= selector.negatedPseudoStateMask;
                }
                else
                {
                    // if the element didn't have those flags, it would match this selector
                    dependencyPseudoMask |= selector.negatedPseudoStateMask;
                }
            }

            return new MatchResultInfo(match, (PseudoStates)triggerPseudoStateMask, (PseudoStates)dependencyPseudoMask);
        }

        public static bool MatchRightToLeft(UiEntity element, StyleComplexSelector complexSelector, Action<UiEntity, MatchResultInfo> processResult)
        {
            // see https://speakerdeck.com/constellation/css-jit-just-in-time-compiled-css-selectors-in-webkit for
            // a detailed explaination of the algorithm

            var current = element;
            int nextIndex = complexSelector.selectors.Length - 1;
            UiEntity saved = default;
            int savedIdx = -1;

            // go backward
            while (nextIndex >= 0)
            {
                if (current.Invalid)
                    break;

                MatchResultInfo matchInfo = MatchesSelector(current, complexSelector.selectors[nextIndex]);
                processResult(current, matchInfo);

                if (!matchInfo.success)
                {
                    // if we have a descendent relationship, keep trying on the parent
                    // ie. "div span", div failed on this element, try on the parent
                    // happens earlier than the backtracking saving below
                    if (nextIndex < complexSelector.selectors.Length - 1 &&
                        complexSelector.selectors[nextIndex + 1].previousRelationship == StyleSelectorRelationship.Descendent)
                    {
                        current = current.GetParent();
                        continue;
                    }

                    // otherwise, if there's a previous relationship, it's a 'child' one. backtrack from the saved point and try again
                    // ie.  for "#x > .a .b", #x failed, backtrack to .a on the saved element
                    if (!saved.Invalid)
                    {
                        current = saved;
                        nextIndex = savedIdx;
                        continue;
                    }

                    break;
                }

                // backtracking save
                // for "a > b c": we're considering the b matcher. c's previous relationship is Descendent
                // save the current element parent to try to match b again
                if (nextIndex < complexSelector.selectors.Length - 1
                    && complexSelector.selectors[nextIndex + 1].previousRelationship == StyleSelectorRelationship.Descendent)
                {
                    saved = current.GetParent();
                    savedIdx = nextIndex;
                }

                // from now, the element is a match
                if (--nextIndex < 0)
                {
                    return true;
                }
                current = current.GetParent();
            }
            return false;
        }

        static void FastLookup(IDictionary<string, StyleComplexSelector> table, List<SelectorMatchRecord> matchedSelectors, StyleMatchingContext context, string input, ref SelectorMatchRecord record)
        {
            StyleComplexSelector currentComplexSelector;
            if (table.TryGetValue(input, out currentComplexSelector))
            {
                while (currentComplexSelector != null)
                {
                    if (MatchRightToLeft(context.currentElement, currentComplexSelector, context.processResult))
                    {
                        record.complexSelector = currentComplexSelector;
                        matchedSelectors.Add(record);
                    }
                    currentComplexSelector = currentComplexSelector.nextInTable;
                }
            }
        }

        public static void FindMatches(StyleMatchingContext context, List<SelectorMatchRecord> matchedSelectors)
        {
            Debug.Assert(matchedSelectors.Count == 0);

//            Debug.Assert(context.currentElement != null, "context.currentElement != null");

            UiEntity element = context.currentElement;

            for (int i = 0; i < context.styleSheetStack.Count; i++)
            {
                StyleSheet styleSheet = context.styleSheetStack[i];
                SelectorMatchRecord record = new SelectorMatchRecord(styleSheet, i);

                FastLookup(styleSheet.orderedTypeSelectors, matchedSelectors, context, element.typeName, ref record);
//                FastLookup(styleSheet.orderedTypeSelectors, matchedSelectors, context, "*", ref record);
//
//                if (!string.IsNullOrEmpty(element.name))
//                {
//                    FastLookup(styleSheet.orderedNameSelectors, matchedSelectors, context, element.name, ref record);
//                }
//
//                foreach (string @class in element.classList)
//                {
//                    FastLookup(styleSheet.orderedClassSelectors, matchedSelectors, context, @class, ref record);
//                }
            }
        }
    }
}