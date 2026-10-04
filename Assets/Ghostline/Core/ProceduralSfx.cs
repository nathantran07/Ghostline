using System;

namespace Ghostline.Core
{
    public enum ProceduralSfxKind
    {
        WallHit,
        Countdown,
        Start,
        LapComplete,
        NewBestLap
    }

    public static class ProceduralSfx
    {
        public static float[] Generate(ProceduralSfxKind kind, int sampleRate)
        {
            AudioMath.SampleRate(sampleRate);
            if (!Enum.IsDefined(typeof(ProceduralSfxKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            double duration = kind == ProceduralSfxKind.WallHit ? 0.18d
                : kind == ProceduralSfxKind.Countdown ? 0.13d
                : kind == ProceduralSfxKind.Start ? 0.28d
                : kind == ProceduralSfxKind.LapComplete ? 0.6d : 0.9d;
            var buffer = new float[(int)(duration * sampleRate)];
            uint random = 0xabcdef12;
            double filtered = 0d;
            double noiseFilter = 1d - Math.Exp(-2d * Math.PI * 350d / sampleRate);
            for (int i = 0; i < buffer.Length; i++)
            {
                double time = i / (double)sampleRate;
                double envelope = Math.Min(1d, time / 0.007d)
                    * Math.Min(1d, (buffer.Length - 1 - i) / (sampleRate * 0.04d));
                double signal;
                if (kind == ProceduralSfxKind.WallHit)
                {
                    random ^= random << 13;
                    random ^= random >> 17;
                    random ^= random << 5;
                    filtered += (random / (double)uint.MaxValue * 2d - 1d - filtered) * noiseFilter;
                    signal = (0.7d * Math.Sin(2d * Math.PI * (85d * time - 100d * time * time))
                        + filtered * 0.3d) * Math.Exp(-time * 22d);
                }
                else if (kind == ProceduralSfxKind.Countdown || kind == ProceduralSfxKind.Start)
                    signal = 0.5d * Math.Sin(2d * Math.PI * (kind == ProceduralSfxKind.Start ? 1320d : 880d) * time);
                else
                {
                    // Completion: rising major triad. Best: longer, higher arpeggio and octave.
                    int note = Math.Min(kind == ProceduralSfxKind.NewBestLap ? 3 : 2,
                        (int)(time / (kind == ProceduralSfxKind.NewBestLap ? 0.18d : 0.16d)));
                    double frequency = note == 0 ? 523.25d : note == 1 ? 659.25d : note == 2 ? 783.99d : 1046.5d;
                    if (kind == ProceduralSfxKind.NewBestLap)
                        frequency *= 1.25d;
                    double noteTime = time % (kind == ProceduralSfxKind.NewBestLap ? 0.18d : 0.16d);
                    double attack = Math.Min(1d, noteTime / 0.007d);
                    double release = Math.Min(1d, ((kind == ProceduralSfxKind.NewBestLap ? 0.18d : 0.16d) - noteTime) / 0.02d);
                    signal = 0.45d * Math.Sin(2d * Math.PI * frequency * noteTime) * attack * release;
                }
                buffer[i] = AudioMath.SoftClip((float)(signal * envelope));
            }
            return buffer;
        }
    }
}
