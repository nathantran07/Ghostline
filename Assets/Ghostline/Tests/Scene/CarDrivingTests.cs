using System;
using System.Reflection;
using System.Collections;
using Ghostline.Game;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ghostline.Tests.Scene
{
    public sealed class CarDrivingTests
    {
        [Test]
        public void CoreEvaluatorMatchesInspectorCurvesIncludingWeightsAndWrapping()
        {
            var a = new Keyframe(0f, 0.2f, 0f, 1.8f, 0.3f, 0.75f) { weightedMode = WeightedMode.Out };
            var b = new Keyframe(1f, 0.8f, -0.4f, 0f, 0.65f, 0.3f) { weightedMode = WeightedMode.In };
            AnimationCurve[] curves = { AnimationCurve.Linear(0f, 1f, 2f, 0.2f),
                new AnimationCurve(new Keyframe(0f, 1f, 0f, -0.6f), new Keyframe(1f, 0.4f, -0.3f, 0f)),
                new AnimationCurve(a, b), new AnimationCurve(new Keyframe(0f, 1f, 0f, float.PositiveInfinity), new Keyframe(1f, 2f)) };
            foreach (AnimationCurve curve in curves)
                foreach (WrapMode wrap in new[] { WrapMode.ClampForever, WrapMode.Loop, WrapMode.PingPong })
                {
                    curve.preWrapMode = wrap;
                    curve.postWrapMode = wrap;
                    var core = DrivingCurveAdapter.ToCore(curve);
                    for (int i = -50; i <= 250; i++)
                    {
                        float time = i / 100f;
                        Assert.That(core.Evaluate(time), Is.EqualTo(curve.Evaluate(time)).Within(0.0001f),
                            $"Curve time {time}; wrap {wrap}");
                    }
                }
        }

        [UnityTest]
        public IEnumerator ForceAccelerationRampsAndIsIndependentOfBodyMass()
        {
            yield return new EnterPlayMode();
            foreach (float mass in new[] { 1f, 3f })
            WithCar((car, physics) =>
            {
                car.Body.mass = mass;
                Set(car, "_drag", 0f);
                Set(car, "_throttle", 1f);
                Invoke(car, "FixedUpdate");
                Assert.That(car.SmoothedThrottle, Is.EqualTo(Time.fixedDeltaTime / 0.4f).Within(0.00001f));
                physics.Simulate(Time.fixedDeltaTime);
                Assert.That(car.Body.linearVelocity.y, Is.EqualTo(18f * car.SmoothedThrottle * Time.fixedDeltaTime).Within(0.0001f));
            });
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator LowDragCanAccelerateBeyondReferenceTopSpeedAndReleaseCoastsToRest()
        {
            yield return new EnterPlayMode();
            WithCar((car, physics) =>
            {
                Set(car, "_drag", 0.4f);
                Set(car, "_throttle", 1f);
                Set(car, "_smoothedThrottle", 1f);
                car.Body.linearVelocity = Vector2.up * 15f;
                Invoke(car, "FixedUpdate");
                physics.Simulate(Time.fixedDeltaTime);
                Assert.That(car.Body.linearVelocity.y, Is.GreaterThan(15f));
                Set(car, "_throttle", 0f);
                Set(car, "_smoothedThrottle", 0f);
                for (int i = 0; i < 500; i++)
                {
                    Invoke(car, "FixedUpdate");
                    physics.Simulate(Time.fixedDeltaTime);
                    Assert.That(car.Body.linearVelocity.y, Is.GreaterThanOrEqualTo(-0.00001f));
                }
                Assert.That(car.Body.linearVelocity.magnitude, Is.LessThan(0.0001f));
            });
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator BrakeOverridesThrottleAndOnlyReversesNearRestAndResetClearsInput()
        {
            yield return new EnterPlayMode();
            WithCar((car, physics) =>
            {
                Set(car, "_throttle", 1f);
                Set(car, "_smoothedThrottle", 1f);
                Set(car, "_brake", true);
                car.Body.linearVelocity = Vector2.up * 5f;
                Invoke(car, "FixedUpdate");
                physics.Simulate(Time.fixedDeltaTime);
                Assert.That(car.Body.linearVelocity.y, Is.InRange(0f, 4.5f));
                car.Body.linearVelocity = Vector2.up * 0.4f;
                Invoke(car, "FixedUpdate");
                physics.Simulate(Time.fixedDeltaTime);
                Assert.That(car.Body.linearVelocity.y, Is.Zero.Within(0.00001f));
                Invoke(car, "FixedUpdate");
                physics.Simulate(Time.fixedDeltaTime);
                Assert.That(car.Body.linearVelocity.y, Is.LessThan(0f));
                car.ResetPose(Vector2.zero, 0f);
                Assert.That(car.SmoothedThrottle, Is.Zero);
                Assert.That(car.Body.linearVelocity, Is.EqualTo(Vector2.zero));
            });
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator HeadOnWallImpactRetainsConfiguredSpeed()
        {
            yield return new EnterPlayMode();
            WithCar((car, physics) =>
            {
                var track = new GameObject("Wall owner", typeof(TrackGenerator));
                SceneManager.MoveGameObjectToScene(track, car.gameObject.scene);
                var wall = new GameObject("Wall", typeof(EdgeCollider2D));
                wall.transform.SetParent(track.transform, false);
                wall.GetComponent<EdgeCollider2D>().points = new[] { Vector2.left * 10f, Vector2.right * 10f };
                Set(car, "_drag", 0f);
                Set(car, "_engineBraking", 0f);
                Set(car, "_wallSpeedLoss", 0.35f);
                car.Body.position = Vector2.up;
                car.Body.linearVelocity = Vector2.down * 10f;
                Physics2D.SyncTransforms();
                try
                {
                    for (int i = 0; i < 10 && car.Body.linearVelocity.y < 0f; i++)
                    {
                        Invoke(car, "FixedUpdate");
                        physics.Simulate(Time.fixedDeltaTime);
                    }
                    Assert.That(car.Body.linearVelocity.y, Is.EqualTo(6.5f).Within(0.01f));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(track);
                }
            });
            yield return new ExitPlayMode();
        }

        private static void WithCar(Action<CarController, PhysicsScene2D> test)
        {
            var scene = SceneManager.CreateScene("Driving verification " + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            var root = new GameObject("Test car", typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(CarController));
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var car = root.GetComponent<CarController>();
                Invoke(car, "Awake");
                test(car, scene.GetPhysicsScene2D());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (Application.isPlaying)
                    SceneManager.UnloadSceneAsync(scene);
                else
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void Invoke(object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }
    }
}
