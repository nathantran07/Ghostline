using System;
using Ghostline.Core;
using NUnit.Framework;

namespace Ghostline.Tests.EditMode
{
    public sealed class GhostRecorderTests
    {
        [Test]
        public void RecordsAtFixedIntervalsAcrossUnevenUpdatesAndIncludesFinishPose()
        {
            var recorder = new GhostRecorder();
            recorder.Begin(0f, 0f, 0f);
            recorder.Record(0.03f, 3f, 0f, 0f);
            recorder.Record(0.12f, 12f, 0f, 0f);
            GhostRecording recording = recorder.Complete(0.13f, 13f, 0f, 0f);
            Assert.That(recording.Samples.Count, Is.EqualTo(4));
            Assert.That(recording.Samples[1].Time, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(recording.Samples[1].X, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(recording.Samples[2].Time, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(recording.Samples[3].Time, Is.EqualTo(0.13f));
            Assert.That(recording.Samples[3].X, Is.EqualTo(13f));
        }

        [Test]
        public void ExactIntervalFinishIsNotDuplicatedAndBeginClearsOldSamples()
        {
            var recorder = new GhostRecorder(0.1f);
            recorder.Begin(0f, 0f, 0f);
            Assert.That(recorder.Complete(0.1f, 1f, 0f, 0f).Samples.Count, Is.EqualTo(2));
            recorder.Begin(10f, 0f, 0f);
            Assert.That(recorder.Complete(0.02f, 11f, 0f, 0f).Samples.Count, Is.EqualTo(2));
        }

        [Test]
        public void RequiresBeginAndMonotonicTimes()
        {
            var recorder = new GhostRecorder();
            Assert.Throws<InvalidOperationException>(() => recorder.Record(1f, 0f, 0f, 0f));
            recorder.Begin(0f, 0f, 0f);
            recorder.Record(0.1f, 1f, 0f, 0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => recorder.Record(0.05f, 0f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GhostRecorder(0f));
        }
    }
}
