using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ee4v.FaceExpression.Tests
{
    public sealed class FaceExpressionTests
    {
        private const string TestFolder = "Assets/FaceExpressionTests";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.CreateFolder("Assets", "FaceExpressionTests");
            }
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestFolder);
        }

        [Test]
        public void Apply_GeneratesGestureMatrixAndControlsBlinkAndMouthBindings()
        {
            var avatar = CreateAvatar(
                out var mesh,
                "Smile",
                "Angry",
                "Blink",
                "vrc.v_aa");
            try
            {
                var smile = CreateClip(
                    "Smile.anim",
                    ("blendShape.Smile", 80f),
                    ("blendShape.Blink", 70f),
                    ("blendShape.vrc.v_aa", 60f));
                var angry = CreateClip(
                    "Angry.anim",
                    ("blendShape.Angry", 65f),
                    ("blendShape.Blink", 75f),
                    ("blendShape.vrc.v_aa", 55f));
                var controller = AnimatorController.CreateAnimatorControllerAtPath(
                    TestFolder + "/Avatar.controller");
                controller.AddLayer("Existing Layer");
                var fistOpen = new GestureCombination(FaceGesture.Fist, FaceGesture.Open);
                var victoryGun = new GestureCombination(
                    FaceGesture.Victory,
                    FaceGesture.HandGun);
                var avatarBindings = new FaceExpressionAvatarBindings(
                    new[] { Binding("blendShape.Blink") },
                    new[] { Binding("blendShape.vrc.v_aa") });

                GestureMatrixControllerWriter.Apply(
                    controller,
                    avatar,
                    new Dictionary<GestureCombination, FaceExpressionAssignment>
                    {
                        [fistOpen] = new FaceExpressionAssignment(
                            smile,
                            enableBlink: false),
                        [victoryGun] = new FaceExpressionAssignment(
                            angry,
                            enableBlink: true,
                            fixMouth: true)
                    },
                    new[]
                    {
                        new FaceExpressionMenuEntry(
                            "Menu Angry",
                            new FaceExpressionAssignment(
                                angry,
                                enableBlink: true,
                                fixMouth: true))
                    },
                    avatarBindings,
                    TestFolder);

                Assert.That(
                    controller.layers.Select(layer => layer.name),
                    Does.Contain("Existing Layer"));
                Assert.That(
                    controller.layers.Count(layer =>
                        layer.name == GestureMatrixControllerWriter.LayerName),
                    Is.EqualTo(1));
                var assignments = GestureMatrixControllerWriter.Read(controller);
                Assert.That(assignments[fistOpen].Clip, Is.SameAs(smile));
                Assert.That(assignments[fistOpen].EnableBlink, Is.False);
                Assert.That(assignments[fistOpen].FixMouth, Is.False);
                Assert.That(assignments[victoryGun].Clip, Is.SameAs(angry));
                Assert.That(assignments[victoryGun].EnableBlink, Is.True);
                Assert.That(assignments[victoryGun].FixMouth, Is.True);
                var menuEntries = GestureMatrixControllerWriter.ReadMenuEntries(controller);
                Assert.That(menuEntries.Count, Is.EqualTo(1));
                Assert.That(menuEntries[0].Name, Is.EqualTo("Menu Angry"));
                Assert.That(menuEntries[0].Assignment.Clip, Is.SameAs(angry));

                var layer = controller.layers.Single(candidate =>
                    candidate.name == GestureMatrixControllerWriter.LayerName);
                var fist = layer.stateMachine.states
                    .Select(child => child.state)
                    .Single(state => state.name == "01-02 Fist + Open");
                var fistClip = (AnimationClip)fist.motion;
                Assert.That(ReadValue(fistClip, "blendShape.Smile"), Is.EqualTo(80f));
                Assert.That(ReadValue(fistClip, "blendShape.Angry"), Is.EqualTo(5f));
                Assert.That(ReadValue(fistClip, "blendShape.Blink"), Is.EqualTo(70f));
                Assert.That(ReadOptionalValue(fistClip, "blendShape.vrc.v_aa"), Is.Null);

                var victory = layer.stateMachine.states
                    .Select(child => child.state)
                    .Single(state => state.name == "04-06 Victory + HandGun");
                var victoryClip = (AnimationClip)victory.motion;
                Assert.That(ReadOptionalValue(victoryClip, "blendShape.Blink"), Is.Null);
                Assert.That(ReadValue(victoryClip, "blendShape.vrc.v_aa"), Is.EqualTo(55f));
                var transition = layer.stateMachine.anyStateTransitions
                    .Single(candidate => candidate.destinationState == victory);
                Assert.That(
                    transition.conditions.Select(condition =>
                        (condition.parameter, condition.threshold)),
                    Is.EquivalentTo(new[]
                    {
                        ("GestureLeft", 4f),
                        ("GestureRight", 6f),
                        (GestureMatrixControllerWriter.MenuParameter, 0f)
                    }));

                GestureMatrixControllerWriter.Apply(
                    controller,
                    avatar,
                    new Dictionary<GestureCombination, FaceExpressionAssignment>
                    {
                        [fistOpen] = new FaceExpressionAssignment(smile)
                    },
                    new FaceExpressionMenuEntry[0],
                    avatarBindings,
                    TestFolder);
                Assert.That(
                    controller.layers.Count(candidate =>
                        candidate.name == GestureMatrixControllerWriter.LayerName),
                    Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(avatar);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Read_ConvertsHeadersAndIncludesSelectedMeshes()
        {
            var avatar = CreateAvatar(
                out var mesh,
                "----------EYE------------",
                "Blink",
                "====MOUTH====",
                "Smile");
            var accessory = new GameObject("Accessory");
            accessory.transform.SetParent(avatar.transform, false);
            var accessoryRenderer = accessory.AddComponent<SkinnedMeshRenderer>();
            var accessoryMesh = CreateMesh("Sparkle");
            accessoryRenderer.sharedMesh = accessoryMesh;
            try
            {
                FaceExpressionGroupSession.SetAvatar(avatar);
                FaceExpressionGroupSession.AddMesh("Accessory");
                var channels = FaceExpressionClipEditor.Read(
                    avatar,
                    null,
                    new[] { "=", "-" },
                    FaceExpressionGroupSession.RendererPaths);

                Assert.That(channels[0].IsHeader, Is.True);
                Assert.That(channels[0].HeaderText, Is.EqualTo("EYE"));
                Assert.That(channels[1].IsHeader, Is.False);
                Assert.That(channels[2].IsHeader, Is.True);
                Assert.That(channels[2].HeaderText, Is.EqualTo("MOUTH"));
                Assert.That(channels[4].RendererPath, Is.EqualTo("Accessory"));
                Assert.That(channels[4].Name, Is.EqualTo("Sparkle"));
                Assert.That(
                    FaceExpressionGroupSession.SelectedMeshes.Select(item => item.Path),
                    Is.EqualTo(new[] { "Body", "Accessory" }));
            }
            finally
            {
                FaceExpressionGroupSession.SelectGroup(null);
                FaceExpressionGroupSession.SetAvatar(null);
                Object.DestroyImmediate(avatar);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(accessoryMesh);
            }
        }

        [Test]
        public void Groups_FilterHeadersAndTreatAddedMeshesAsGroups()
        {
            var avatar = CreateAvatar(out var bodyMesh, "Smile");
            var hair = new GameObject("Hair");
            hair.transform.SetParent(avatar.transform, false);
            var hairMesh = CreateMesh("Loose");
            hair.AddComponent<SkinnedMeshRenderer>().sharedMesh = hairMesh;
            var channels = new[]
            {
                new BlendShapeChannel("Body", "Smile", 0f, false),
                new BlendShapeChannel("Body", "---EYES---", 0f, false, "EYES"),
                new BlendShapeChannel("Body", "Blink", 0f, false),
                new BlendShapeChannel("Body", "Wide", 0f, false),
                new BlendShapeChannel("Body", "---MOUTH---", 0f, false, "MOUTH"),
                new BlendShapeChannel("Body", "Aa", 0f, false),
                new BlendShapeChannel("Hair", "Loose", 0f, false)
            };

            try
            {
                FaceExpressionGroupSession.SetAvatar(avatar);
                FaceExpressionGroupSession.AddMesh("Hair");
                FaceExpressionGroupSession.UpdateChannels(channels);

                Assert.That(FaceExpressionGroupSession.TotalCount, Is.EqualTo(5));
                Assert.That(
                    FaceExpressionGroupSession.Groups.Select(group =>
                        (group.Name, group.Count)),
                    Is.EqualTo(new[]
                    {
                        ("Hair", 1),
                        ("EYES", 2),
                        ("MOUTH", 1)
                    }));

                var hairGroup = FaceExpressionGroupSession.Groups.Single(group =>
                    group.RendererPath == "Hair");
                FaceExpressionGroupSession.SelectGroup(hairGroup.Key);
                Assert.That(
                    FaceExpressionGroupSession.Filter(channels)
                        .Select(channel => channel.Name),
                    Is.EqualTo(new[] { "Loose" }));

                var mouthGroup = FaceExpressionGroupSession.Groups.Single(group =>
                    group.RendererPath == null && group.Name == "MOUTH");
                FaceExpressionGroupSession.SelectGroup(mouthGroup.Key);
                Assert.That(
                    FaceExpressionGroupSession.Filter(channels)
                        .Select(channel => channel.Name),
                    Is.EqualTo(new[] { "---MOUTH---", "Aa" }));
            }
            finally
            {
                FaceExpressionGroupSession.SelectGroup(null);
                FaceExpressionGroupSession.SetAvatar(null);
                Object.DestroyImmediate(avatar);
                Object.DestroyImmediate(bodyMesh);
                Object.DestroyImmediate(hairMesh);
            }
        }

        [Test]
        public void BlendShapeClipboard_PastesMatchingNamesWithoutUsingIndices()
        {
            var avatar = CreateAvatar(out var mesh, "Smile", "Angry");
            var renderer = avatar.GetComponentInChildren<SkinnedMeshRenderer>();
            var previousClipboard = EditorGUIUtility.systemCopyBuffer;
            var channels = new[]
            {
                new BlendShapeChannel("Body", "Angry", 65f, true),
                new BlendShapeChannel("Body", "Missing", 80f, true)
            };

            try
            {
                Assert.That(BlendShapeClipboard.Copy(channels), Is.True);
                Assert.That(BlendShapeClipboard.CanPaste(renderer), Is.True);
                Assert.That(BlendShapeClipboard.Paste(renderer), Is.EqualTo(1));
                Assert.That(renderer.GetBlendShapeWeight(0), Is.Zero);
                Assert.That(renderer.GetBlendShapeWeight(1), Is.EqualTo(65f));
            }
            finally
            {
                EditorGUIUtility.systemCopyBuffer = previousClipboard;
                Object.DestroyImmediate(avatar);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void BlendShapeRows_GroupOnlyNormalAndSides()
        {
            var channels = new[]
            {
                new BlendShapeChannel(
                    "Body",
                    "---EYE---",
                    0f,
                    false,
                    "EYE"),
                CreateMappedChannel("eye_blink_1", 10f),
                CreateMappedChannel("eye_blink_1_L", 20f),
                CreateMappedChannel("eye_blink_1_R", 30f),
                CreateMappedChannel("eye_blink_2", 40f),
                CreateMappedChannel("eye_blink_2_L", 50f),
                CreateMappedChannel("eye_blink_2_R", 60f),
                new BlendShapeChannel(
                    "Body",
                    "---MOUTH---",
                    0f,
                    false,
                    "MOUTH"),
                CreateMappedChannel("mouth_blink_1", 70f),
                CreateMappedChannel("mouth_blink_1_L", 80f),
                CreateMappedChannel("mouth_blink_1_R", 90f)
            };

            var rows = BlendShapeRowItem.Create(
                channels,
                CreateMappedRule(channels),
                hideHeaders: true);

            Assert.That(rows.Count, Is.EqualTo(3));
            Assert.That(rows.Select(row => row.DisplayName),
                Is.EqualTo(new[] { "blink_1", "blink_2", "blink_1" }));
            Assert.That(rows[0].ActiveChannel.Name, Is.EqualTo("eye_blink_1"));
            rows[0].ToggleSide("L");
            Assert.That(rows[0].ActiveChannel.Name,
                Is.EqualTo("eye_blink_1_L"));
            Assert.That(rows[1].ActiveChannel.Name, Is.EqualTo("eye_blink_2"));
            Assert.That(rows[2].ActiveChannel.Name, Is.EqualTo("mouth_blink_1"));
        }

        [Test]
        public void BlendShapeRows_KeepClipChannelsSeparate()
        {
            var channels = new[]
            {
                CreateMappedChannel("eye_brink_1", 10f),
                CreateMappedChannel("eye_brink_2", 20f),
                CreateMappedChannel("eye_brink_2_L", 30f),
                CreateMappedChannel("eye_brink_2_R", 40f)
            };

            var rows = BlendShapeRowItem.Create(
                channels,
                CreateMappedRule(channels),
                hideHeaders: true,
                groupSides: false);

            Assert.That(rows.Select(row => row.DisplayName),
                Is.EqualTo(channels.Select(channel => channel.Name)));
            Assert.That(rows.Select(row => row.ActiveChannel.Name),
                Is.EqualTo(channels.Select(channel => channel.Name)));
            Assert.That(rows.Select(row => row.SelectedSide),
                Is.EqualTo(new[] { string.Empty, string.Empty, "L", "R" }));

            var row = new BlendShapeRow();
            row.SetItem(rows[2], optionsReadOnly: true);
            var sides = row.Query<Toggle>(
                    className: "ee4v-face-expression-row__side")
                .ToList();
            Assert.That(row.Query<PopupField<string>>().ToList(), Is.Empty);
            Assert.That(sides.Count, Is.EqualTo(2));
            Assert.That(sides[0].style.visibility.value,
                Is.EqualTo(Visibility.Hidden));
            Assert.That(sides[1].style.visibility.value,
                Is.EqualTo(Visibility.Visible));
            Assert.That(sides[1].value, Is.True);
            Assert.That(sides[1].enabledSelf, Is.False);
        }

        [TestCase("mouth_smile_1_R", "smile_1", "R")]
        [TestCase("eye_nagomi2_left", "nagomi2", "L")]
        [TestCase("eyelid_under_up2_R", "under_up2", "R")]
        [TestCase("other_sweat _1", "sweat _1", "")]
        [TestCase(
            "mouth_grin 2 (no tooth)_L",
            "grin 2 (no tooth)",
            "L")]
        [TestCase(
            "option_tear_under_L 1",
            "tear_under 1",
            "L")]
        public void BlendShapePresetClassifier_SeedsSupportedAvatarConventions(
            string shapeName,
            string expectedRole,
            string expectedSide)
        {
            var mapping = BlendShapeNameClassifier.Classify(
                123L,
                "Body",
                shapeName);

            Assert.That((mapping.role ?? string.Empty), Is.EqualTo(expectedRole));
            Assert.That((mapping.side ?? string.Empty), Is.EqualTo(expectedSide));
        }

        [Test]
        public void BlendShapePresets_RoundTripManualFbxMapping()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "ee4v-blendshape-preset-" +
                System.Guid.NewGuid().ToString("N"));
            var mapping = BlendShapeNameClassifier.Classify(
                123L,
                "Body",
                "eye_blink_2_R");
            mapping.role = "manual blink";
            var preset = new BlendShapeFbxPreset
            {
                assetGuid = "fbx-guid",
                assetPath = "Assets/Avatar.fbx",
                name = "Avatar",
                mappings = new List<BlendShapeNameMapping> { mapping }
            };

            try
            {
                var store = new BlendShapePresetFileStore(directory);
                store.Save(preset);
                var restored = store.Load();
                var rule = new BlendShapeNamingRule(restored);
                var channel = CreateMappedChannel("eye_blink_2_R", 0f);

                Assert.That(
                    File.Exists(Path.Combine(directory, "Avatar.json")),
                    Is.True);
                Assert.That(restored.presets.Count, Is.EqualTo(1));
                Assert.That(rule.TryParse(channel, out var parsed), Is.True);
                Assert.That(parsed.Role, Is.EqualTo("manual blink"));
                Assert.That(parsed.Side, Is.EqualTo("R"));
                Assert.That(
                    rule.TryParse(
                        new BlendShapeChannel(
                            "Body",
                            "eye_blink_2_R",
                            0f,
                            false,
                            sourceAssetGuid: "different-fbx",
                            sourceMeshLocalId: 123L),
                        out _),
                    Is.False);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static BlendShapeChannel CreateMappedChannel(
            string name,
            float value)
        {
            return new BlendShapeChannel(
                "Body",
                name,
                value,
                true,
                sourceAssetGuid: "fbx-guid",
                sourceMeshLocalId: 123L);
        }

        private static BlendShapeNamingRule CreateMappedRule(
            IEnumerable<BlendShapeChannel> channels)
        {
            var preset = new BlendShapeFbxPreset
            {
                assetGuid = "fbx-guid",
                name = "Avatar",
                mappings = channels.Select(channel =>
                    BlendShapeNameClassifier.Classify(
                        123L,
                        "Body",
                        channel.Name)).ToList()
            };
            var state = new BlendShapeNamePresetState();
            state.presets.Add(preset);
            return new BlendShapeNamingRule(state);
        }

        private static GameObject CreateAvatar(
            out Mesh mesh,
            params string[] shapeNames)
        {
            var avatar = new GameObject("Avatar");
            var body = new GameObject("Body");
            body.transform.SetParent(avatar.transform, false);
            var renderer = body.AddComponent<SkinnedMeshRenderer>();
            mesh = CreateMesh(shapeNames.Length == 0
                ? new[] { "Smile", "Angry" }
                : shapeNames);
            if (shapeNames.Length == 0)
            {
                shapeNames = new[] { "Smile", "Angry" };
            }

            renderer.sharedMesh = mesh;
            if (shapeNames.Length > 1 && shapeNames[1] == "Angry")
            {
                renderer.SetBlendShapeWeight(1, 5f);
            }

            return avatar;
        }

        private static Mesh CreateMesh(params string[] shapeNames)
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up
                },
                triangles = new[] { 0, 1, 2 }
            };
            var delta = new[] { Vector3.zero, Vector3.zero, Vector3.zero };
            foreach (var shapeName in shapeNames)
            {
                mesh.AddBlendShapeFrame(shapeName, 100f, delta, delta, delta);
            }

            return mesh;
        }

        private static AnimationClip CreateClip(
            string fileName,
            params (string property, float value)[] values)
        {
            var clip = new AnimationClip();
            foreach (var value in values)
            {
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        "Body",
                        typeof(SkinnedMeshRenderer),
                        value.property),
                    AnimationCurve.Constant(0f, 1f / 60f, value.value));
            }

            AssetDatabase.CreateAsset(clip, TestFolder + "/" + fileName);
            return clip;
        }

        private static float ReadValue(AnimationClip clip, string property)
        {
            var curve = AnimationUtility.GetEditorCurve(clip, Binding(property));
            Assert.That(curve, Is.Not.Null);
            return curve.Evaluate(0f);
        }

        private static float? ReadOptionalValue(AnimationClip clip, string property)
        {
            var curve = AnimationUtility.GetEditorCurve(clip, Binding(property));
            return curve == null ? (float?)null : curve.Evaluate(0f);
        }

        private static EditorCurveBinding Binding(string property)
        {
            return EditorCurveBinding.FloatCurve(
                "Body",
                typeof(SkinnedMeshRenderer),
                property);
        }
    }
}
