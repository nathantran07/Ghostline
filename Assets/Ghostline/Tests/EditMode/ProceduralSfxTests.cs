using System;
using NUnit.Framework;
using Ghostline.Core;

namespace Ghostline.Tests.EditMode
{
    public sealed class ProceduralSfxTests
    {
        [TestCase(ProceduralSfxKind.WallHit)]
        [TestCase(ProceduralSfxKind.Countdown)]
        [TestCase(ProceduralSfxKind.Start)]
        [TestCase(ProceduralSfxKind.LapComplete)]
        [TestCase(ProceduralSfxKind.NewBestLap)]
        public void EffectsAreDeterministicBoundedAndEnveloped(ProceduralSfxKind kind)
        {
            float[] buffer = ProceduralSfx.Generate(kind, 48000);
            Assert.That(buffer, Is.EqualTo(ProceduralSfx.Generate(kind, 48000)));
            Assert.That(buffer[0], Is.Zero);
            Assert.That(buffer[buffer.Length - 1], Is.Zero);
            Assert.That(buffer, Is.All.InRange(-0.85f, 0.85f));
            Assert.That(Array.Exists(buffer, value => Math.Abs(value) > 0.01f), Is.True);
        }

        [Test]
        public void StartAndBestChimesAreDistinctAndImpactGainIsBounded()
        {
            Assert.That(ProceduralSfx.Generate(ProceduralSfxKind.Start, 8000),
                Is.Not.EqualTo(ProceduralSfx.Generate(ProceduralSfxKind.Countdown, 8000)));
            Assert.That(ProceduralSfx.Generate(ProceduralSfxKind.NewBestLap, 8000),
                Is.Not.EqualTo(ProceduralSfx.Generate(ProceduralSfxKind.LapComplete, 8000)));
            Assert.That(AudioMath.ImpactVolume(0f, 18f), Is.Zero);
            Assert.That(AudioMath.ImpactVolume(9f, 18f), Is.EqualTo(0.5f));
            Assert.That(AudioMath.ImpactVolume(float.MaxValue, float.Epsilon), Is.EqualTo(1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => AudioMath.ImpactVolume(-1f, 18f));
            Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralSfx.Generate((ProceduralSfxKind)99, 48000));
        }
    }
}
