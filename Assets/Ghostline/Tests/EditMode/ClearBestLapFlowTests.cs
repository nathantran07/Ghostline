using System;
using Ghostline.Core;
using NUnit.Framework;

namespace Ghostline.Tests.EditMode
{
    public sealed class ClearBestLapFlowTests
    {
        [Test]
        public void HoldProgressEarlyReleaseAndFreshHold()
        {
            var flow = new ClearBestLapFlow();
            Assert.That(flow.Tick(0.25f, true, false, false), Is.False);
            Assert.That(flow.State, Is.EqualTo(ClearBestLapState.Holding));
            Assert.That(flow.HoldProgress, Is.EqualTo(0.25f));
            flow.Tick(0.5f, false, false, false);
            Assert.That(flow.State, Is.EqualTo(ClearBestLapState.Idle));
            Assert.That(flow.HoldProgress, Is.Zero);
            flow.Tick(0.75f, true, false, false);
            Assert.That(flow.State, Is.EqualTo(ClearBestLapState.Holding));
            flow.Tick(0.25f, true, false, false);
            Assert.That(flow.State, Is.EqualTo(ClearBestLapState.AwaitingConfirm));
            Assert.That(flow.HoldProgress, Is.EqualTo(1f));
        }

        [Test]
        public void ConfirmationEmitsExactlyOneClearRequestAndRequiresDeleteRelease()
        {
            var flow = OpenPrompt();
            Assert.That(flow.Tick(0.1f, true, true, false), Is.True);
            Assert.That(flow.State, Is.EqualTo(ClearBestLapState.Idle));
            Assert.That(flow.Tick(2f, true, true, false), Is.False);
            Assert.That(flow.State, Is.EqualTo(ClearBestLapState.Idle));
            flow.Tick(0f, false, false, false);
            flow.Tick(1f, true, false, false);
            Assert.That(flow.State, Is.EqualTo(ClearBestLapState.AwaitingConfirm));
        }

        [Test]
        public void CancelWinsOverSimultaneousConfirmation()
        {
            var flow = OpenPrompt();
            Assert.That(flow.Tick(0.1f, false, true, true), Is.False);
            Assert.That(flow.State, Is.EqualTo(ClearBestLapState.Idle));
        }

        [Test]
        public void DeleteReleaseDoesNotCancelOpenPrompt()
        {
            var flow = OpenPrompt();
            flow.Tick(1f, false, false, false);
            Assert.That(flow.State, Is.EqualTo(ClearBestLapState.AwaitingConfirm));
            Assert.That(flow.Tick(0f, false, true, false), Is.True);
        }

        [Test]
        public void TimeoutClosesWithoutClearingAndIgnoresConfirmationAtDeadline()
        {
            var flow = OpenPrompt();
            Assert.That(flow.Tick(4.75f, true, false, false), Is.False);
            Assert.That(flow.State, Is.EqualTo(ClearBestLapState.AwaitingConfirm));
            Assert.That(flow.Tick(0.25f, true, true, false), Is.False);
            Assert.That(flow.State, Is.EqualTo(ClearBestLapState.Idle));
            flow.Tick(10f, true, false, false);
            Assert.That(flow.State, Is.EqualTo(ClearBestLapState.Idle));
        }

        [Test]
        public void SuppliedDurationsAndOpeningFrameKeysAreRespected()
        {
            var flow = new ClearBestLapFlow(2f, 3f);
            flow.Tick(1f, true, true, true);
            Assert.That(flow.State, Is.EqualTo(ClearBestLapState.Holding));
            Assert.That(flow.Tick(1f, true, true, true), Is.False);
            Assert.That(flow.State, Is.EqualTo(ClearBestLapState.AwaitingConfirm));
            flow.Tick(3f, false, false, false);
            Assert.That(flow.State, Is.EqualTo(ClearBestLapState.Idle));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDurationsAreRejected(float duration)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClearBestLapFlow(duration, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClearBestLapFlow(1f, duration));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidTickTimeDoesNotMutateFlow(float seconds)
        {
            var flow = OpenPrompt();
            Assert.Throws<ArgumentOutOfRangeException>(() => flow.Tick(seconds, false, true, false));
            Assert.That(flow.State, Is.EqualTo(ClearBestLapState.AwaitingConfirm));
        }

        [Test]
        public void HoldProgressStartsAtZeroReachesHalfAndFullAndResetsOnRelease()
        {
            var flow = new ClearBestLapFlow();
            Assert.That(flow.HoldProgress, Is.Zero);
            flow.Tick(0f, true, false, false);
            Assert.That(flow.HoldProgress, Is.Zero);
            flow.Tick(0.5f, true, false, false);
            Assert.That(flow.HoldProgress, Is.EqualTo(0.5f));
            flow.Tick(0f, false, false, false);
            Assert.That(flow.HoldProgress, Is.Zero);
            flow.Tick(1f, true, false, false);
            Assert.That(flow.HoldProgress, Is.EqualTo(1f));
        }

        [Test]
        public void TimeoutFractionFallsFromFullToHalfToZeroWhenPromptCloses()
        {
            var flow = new ClearBestLapFlow(2f, 4f);
            Assert.That(flow.TimeoutFraction, Is.Zero);
            flow.Tick(1f, true, false, false);
            Assert.That(flow.TimeoutFraction, Is.Zero);
            flow.Tick(1f, true, false, false);
            Assert.That(flow.TimeoutFraction, Is.EqualTo(1f));
            flow.Tick(2f, false, false, false);
            Assert.That(flow.TimeoutFraction, Is.EqualTo(0.5f));
            Assert.That(flow.State, Is.EqualTo(ClearBestLapState.AwaitingConfirm));
            Assert.That(flow.Tick(2f, false, false, false), Is.False);
            Assert.That(flow.TimeoutFraction, Is.Zero);
            Assert.That(flow.State, Is.EqualTo(ClearBestLapState.Idle));
        }

        private static ClearBestLapFlow OpenPrompt()
        {
            var flow = new ClearBestLapFlow();
            Assert.That(flow.Tick(1f, true, false, false), Is.False);
            return flow;
        }
    }
}
