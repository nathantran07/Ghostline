using Ghostline.Core;
using NUnit.Framework;

namespace Ghostline.Tests.EditMode
{
    public sealed class QuitFlowTests
    {
        [Test]
        public void EscapeOpensPlayerPrompt()
        {
            var flow = new QuitFlow();
            Assert.That(flow.Tick(true, false, false, true), Is.False);
            Assert.That(flow.State, Is.EqualTo(QuitState.AwaitingConfirm));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void EscapeOrCancelClosesWithoutQuitting(bool escape, bool cancel)
        {
            var flow = OpenPrompt();
            Assert.That(flow.Tick(escape, false, cancel, true), Is.False);
            Assert.That(flow.State, Is.EqualTo(QuitState.Idle));
        }

        [Test]
        public void ConfirmRequestsQuitExactlyOnce()
        {
            var flow = OpenPrompt();
            Assert.That(flow.Tick(false, true, false, true), Is.True);
            Assert.That(flow.State, Is.EqualTo(QuitState.Idle));
            Assert.That(flow.Tick(false, true, false, true), Is.False);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void CancelWinsOverConfirmation(bool escape, bool cancel)
        {
            var flow = OpenPrompt();
            Assert.That(flow.Tick(escape, true, cancel, true), Is.False);
            Assert.That(flow.State, Is.EqualTo(QuitState.Idle));
        }

        [Test]
        public void OpeningFrameCannotAlsoConfirmOrCancel()
        {
            var flow = new QuitFlow();
            Assert.That(flow.Tick(true, true, true, true), Is.False);
            Assert.That(flow.State, Is.EqualTo(QuitState.AwaitingConfirm));
        }

        [Test]
        public void UnrelatedInputAndElapsedFramesDoNotClosePrompt()
        {
            var flow = OpenPrompt();
            for (int frame = 0; frame < 10000; frame++)
                Assert.That(flow.Tick(false, false, false, true), Is.False);
            Assert.That(flow.State, Is.EqualTo(QuitState.AwaitingConfirm));
        }

        [Test]
        public void DisabledFlowNeverOpensOrQuits()
        {
            var flow = new QuitFlow();
            Assert.That(flow.Tick(true, true, true, false), Is.False);
            Assert.That(flow.State, Is.EqualTo(QuitState.Idle));
            flow.Tick(true, false, false, true);
            Assert.That(flow.Tick(false, true, false, false), Is.False);
            Assert.That(flow.State, Is.EqualTo(QuitState.Idle));
        }

        private static QuitFlow OpenPrompt()
        {
            var flow = new QuitFlow();
            flow.Tick(true, false, false, true);
            return flow;
        }
    }
}
