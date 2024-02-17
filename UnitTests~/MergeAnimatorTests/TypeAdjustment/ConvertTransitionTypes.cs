using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using modular_avatar_tests;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

public class ConvertTransitionTypes : TestBase
{
    [Test]
    public void IntConversions()
    {
        var prefab = CreatePrefab("ConvertTransitionTypes.prefab");
        
        AvatarProcessor.ProcessAvatar(prefab);

        var layer = findFxLayer(prefab, "int transitions");
        AssertTransitions(layer, "int", "gt1", 0, ("int", AnimatorConditionMode.Greater, 1));
        AssertTransitions(layer, "int", "lt1", 0, ("int", AnimatorConditionMode.Less, 1));
        AssertTransitions(layer, "int", "eq1", 0,
            ("int", AnimatorConditionMode.Greater, 0.9f),
            ("int", AnimatorConditionMode.Less, 1.1f)
        );
        AssertTransitions(layer, "int", "ne1", 0, ("int", AnimatorConditionMode.Greater, 1.1f));
        AssertTransitions(layer, "int", "ne1", 1, ("int", AnimatorConditionMode.Less, 0.9f));
        AssertTransitions(layer, "int", "ne_multi", 0,
            ("int", AnimatorConditionMode.Greater, 1.1f),
            ("int2", AnimatorConditionMode.Greater, 2.1f)
        );
        AssertTransitions(layer, "int", "ne_multi", 1,
            ("int", AnimatorConditionMode.Greater, 1.1f),
            ("int2", AnimatorConditionMode.Less, 1.9f)
        );
        AssertTransitions(layer, "int", "ne_multi", 2,
            ("int", AnimatorConditionMode.Less, 0.9f),
            ("int2", AnimatorConditionMode.Greater, 2.1f)
        );
        AssertTransitions(layer, "int", "ne_multi", 3,
            ("int", AnimatorConditionMode.Less, 0.9f),
            ("int2", AnimatorConditionMode.Less, 1.9f)
        );
    }

    [Test]
    public void BoolConversions()
    {
        var prefab = CreatePrefab("ConvertTransitionTypes.prefab");
        
        AvatarProcessor.ProcessAvatar(prefab);

        var layer = findFxLayer(prefab, "bool transitions");
        AssertTransitions(layer, "bool", "true", 0, ("bool", AnimatorConditionMode.Greater, 0.5f));
        AssertTransitions(layer, "bool", "false", 0, ("bool", AnimatorConditionMode.Less, 0.5f));
    }

    [Test]
    public void FloatUnchanged()
    {
        var prefab = CreatePrefab("ConvertTransitionTypes.prefab");
        
        AvatarProcessor.ProcessAvatar(prefab);

        var layer = findFxLayer(prefab, "float transitions");
        AssertTransitions(layer, "float", "gt", 0, ("float", AnimatorConditionMode.Greater, 123));
        AssertTransitions(layer, "float", "lt", 0, ("float", AnimatorConditionMode.Less, 123));
    }

    [Test]
    public void AnyState()
    {
        var prefab = CreatePrefab("ConvertTransitionTypes.prefab");
        
        AvatarProcessor.ProcessAvatar(prefab);

        var layer = findFxLayer(prefab, "anystate");
        var anyStateTransitions = layer.stateMachine.anyStateTransitions;

        AssertSingleTransition(anyStateTransitions[0], ("int", AnimatorConditionMode.Greater, 0.1f));
        AssertSingleTransition(anyStateTransitions[1], ("int", AnimatorConditionMode.Less, -0.1f));
    }
    
    
    [Test]
    public void Entry()
    {
        var prefab = CreatePrefab("ConvertTransitionTypes.prefab");
        
        AvatarProcessor.ProcessAvatar(prefab);

        var layer = findFxLayer(prefab, "entry");
        var transitions = layer.stateMachine.entryTransitions;

        AssertSingleTransition(transitions[0], ("int", AnimatorConditionMode.Greater, 0.1f));
        AssertSingleTransition(transitions[1], ("int", AnimatorConditionMode.Less, -0.1f));
    }

    [Test]
    public void PreservesTransitionConfig()
    {
        var prefab = CreatePrefab("ConvertTransitionTypes.prefab");
        
        AvatarProcessor.ProcessAvatar(prefab);

        var layer = findFxLayer(prefab, "preserve_config");

        var state = FindStateInLayer(layer, "foo");
        Assert.AreEqual(123, layer.stateMachine.anyStateTransitions[0].duration);
        Assert.AreEqual(123, state.transitions[0].exitTime);
    }

    [Test]
    public void ConversionWhenInconsistent()
    {
        var prefab = CreatePrefab("ConvertTransitionTypes.prefab");
        
        AvatarProcessor.ProcessAvatar(prefab);

        var fx = (AnimatorController) FindFxController(prefab).animatorController;

        var p_types = fx.parameters.Select(
            p => new KeyValuePair<string, AnimatorControllerParameterType>(p.name, p.type)
        ).ToImmutableDictionary();
        
        Assert.AreEqual(AnimatorControllerParameterType.Int, p_types["int3"]);
        Assert.AreEqual(AnimatorControllerParameterType.Trigger, p_types["trigger"]);
        Assert.AreEqual(AnimatorControllerParameterType.Float, p_types["bool"]);
        Assert.AreEqual(AnimatorControllerParameterType.Float, p_types["int"]);
        Assert.AreEqual(AnimatorControllerParameterType.Float, p_types["float"]);
        Assert.AreEqual(AnimatorControllerParameterType.Float, p_types["int2"]);
    }

    [Test]
    public void SubStateMachineHandling()
    {
        var prefab = CreatePrefab("ConvertTransitionTypes.prefab");
        
        AvatarProcessor.ProcessAvatar(prefab);

        var layer = findFxLayer(prefab, "sub_state_machine");
        
        AssertSingleTransition(layer.stateMachine.entryTransitions[0], ("bool", AnimatorConditionMode.Greater, 0.5f));

        var ssm1 = layer.stateMachine.stateMachines[0].stateMachine;
        AssertSingleTransition(ssm1.entryTransitions[0], ("bool", AnimatorConditionMode.Greater, 0.5f));
        
        var ssm2 = ssm1.stateMachines[0].stateMachine;
        AssertSingleTransition(ssm2.entryTransitions[0], ("bool", AnimatorConditionMode.Greater, 0.5f));
    }
    
    [Test]
    public void NoConversionWhenConsistent()
    {
        var prefab = CreatePrefab("ConvertTransitionTypes.prefab");

        var merge1 = prefab.transform.Find("1").GetComponent<ModularAvatarMergeAnimator>();
        var merge2 = prefab.transform.Find("2").GetComponent<ModularAvatarMergeAnimator>();

        merge2.animator = merge1.animator;
        
        AvatarProcessor.ProcessAvatar(prefab);

        var fx = (AnimatorController) FindFxController(prefab).animatorController;

        var p_types = fx.parameters.Select(
            p => new KeyValuePair<string, AnimatorControllerParameterType>(p.name, p.type)
        ).ToImmutableDictionary();
        
        Assert.AreEqual(AnimatorControllerParameterType.Int, p_types["int3"]);
        Assert.AreEqual(AnimatorControllerParameterType.Trigger, p_types["trigger"]);
        Assert.AreEqual(AnimatorControllerParameterType.Bool, p_types["bool"]);
        Assert.AreEqual(AnimatorControllerParameterType.Int, p_types["int"]);
        Assert.AreEqual(AnimatorControllerParameterType.Float, p_types["float"]);
        Assert.AreEqual(AnimatorControllerParameterType.Int, p_types["int2"]);
        
        var layer = findFxLayer(prefab, "int transitions");
        AssertTransitions(layer, "int", "eq1", 0, ("int", AnimatorConditionMode.Equals, 1f));
    }

    [Test]
    public void CrossLayerTypeConsistency()
    {
        var prefab = CreatePrefab("CrossLayerTypeConsistency.prefab");
        
        AvatarProcessor.ProcessAvatar(prefab);

        var fx = (AnimatorController) FindFxController(prefab).animatorController;
        
        var fx_types = fx.parameters.Select(
            p => new KeyValuePair<string, AnimatorControllerParameterType>(p.name, p.type)
        ).ToImmutableDictionary();
        
        Assert.AreEqual(AnimatorControllerParameterType.Float, fx_types["bool"]);
        
        var fx_layer = fx.layers.First(l => l.name == "l");
        AssertSingleTransition(fx_layer.stateMachine.anyStateTransitions[0], ("bool", AnimatorConditionMode.Greater, 0.5f));
        
        var action = (AnimatorController) FindController(prefab, VRCAvatarDescriptor.AnimLayerType.Action).animatorController;
        
        var action_types = action.parameters.Select(
            p => new KeyValuePair<string, AnimatorControllerParameterType>(p.name, p.type)
        ).ToImmutableDictionary();
        Assert.AreEqual(AnimatorControllerParameterType.Float, action_types["bool"]);

        var action_layer = action.layers.First(l => l.name == "l");
        AssertSingleTransition(action_layer.stateMachine.anyStateTransitions[0], ("bool", AnimatorConditionMode.Greater, 0));
    }

    [Test]
    public void ParametersDoesntAffectTypeResolution()
    {
        var prefab = CreatePrefab("ParametersDoesntAffectTypeResolution.prefab");
        
        AvatarProcessor.ProcessAvatar(prefab);
        
        var fx = (AnimatorController) FindFxController(prefab).animatorController;
        
        Assert.AreEqual(fx.parameters[0].type, AnimatorControllerParameterType.Bool);
    }
    
    void AssertTransitions(AnimatorControllerLayer layer, string src, string dest, int index,
        params (string, AnimatorConditionMode, float)[] conditions)
    {
        var srcState = FindStateInLayer(layer, src);

        var transitions = srcState.transitions.Where(t2 => t2.destinationState.name == dest)
            .ToArray();
        var t = transitions[index];
        
        AssertSingleTransition(t, conditions);
    }

    private static void AssertSingleTransition<T>(T t,
        params (string, AnimatorConditionMode, float)[] conditions
    ) where T: AnimatorTransitionBase
    {
        Assert.AreEqual(t.conditions.Length, conditions.Length);

        for (int i = 0; i < conditions.Length; i++)
        {
            var t_cond = t.conditions[i];
            var (e_param, e_mode, e_thresh) = conditions[i];
            
            Assert.AreEqual(e_param, t_cond.parameter);
            Assert.AreEqual(e_mode, t_cond.mode);
            Assert.Less(Mathf.Abs(t_cond.threshold - e_thresh), 0.001f);
        }
    }
}

