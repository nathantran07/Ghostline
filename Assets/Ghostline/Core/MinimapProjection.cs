using System;

namespace Ghostline.Core
{
    /// <summary>Aspect-preserving world projection into a padded rect, with origin at its top left.</summary>
    public sealed class MinimapProjection
    {
        private readonly double _centerX;
        private readonly double _centerY;
        private readonly double _scale;
        private readonly float _width;
        private readonly float _height;
        private readonly float _padding;

        public MinimapProjection(TrackPoint minimum, TrackPoint maximum, float width, float height, float padding)
        {
            NumericGuard.Nonnegative(width, nameof(width));
            NumericGuard.Nonnegative(height, nameof(height));
            NumericGuard.Nonnegative(padding, nameof(padding));
            if (width == 0f || height == 0f)
                throw new ArgumentOutOfRangeException(nameof(width), "Rect dimensions must be positive.");
            if (padding * 2d >= Math.Min(width, height))
                throw new ArgumentOutOfRangeException(nameof(padding), "Padding must leave a positive drawable area.");
            if (maximum.X < minimum.X || maximum.Y < minimum.Y)
                throw new ArgumentException("Maximum bounds must not be below minimum bounds.", nameof(maximum));

            // Double intermediates also handle bounds spanning the entire finite float range.
            double worldWidth = (double)maximum.X - minimum.X;
            double worldHeight = (double)maximum.Y - minimum.Y;
            _centerX = ((double)minimum.X + maximum.X) * 0.5d;
            _centerY = ((double)minimum.Y + maximum.Y) * 0.5d;
            double scaleX = worldWidth == 0d ? double.PositiveInfinity : (width - padding * 2d) / worldWidth;
            double scaleY = worldHeight == 0d ? double.PositiveInfinity : (height - padding * 2d) / worldHeight;
            _scale = worldWidth == 0d && worldHeight == 0d ? 0d : Math.Min(scaleX, scaleY);
            _width = width;
            _height = height;
            _padding = padding;
        }

        /// <summary>Projects without limiting positions to the target rect.</summary>
        public TrackPoint Project(TrackPoint position)
        {
            return new TrackPoint((float)(_width * 0.5d + (position.X - _centerX) * _scale),
                (float)(_height * 0.5d - (position.Y - _centerY) * _scale));
        }

        /// <summary>Projects and clamps to the padded rect, including its letterboxed space.</summary>
        public TrackPoint ProjectClamped(TrackPoint position)
        {
            double x = _width * 0.5d + (position.X - _centerX) * _scale;
            double y = _height * 0.5d - (position.Y - _centerY) * _scale;
            return new TrackPoint((float)Math.Max(_padding, Math.Min(_width - _padding, x)),
                (float)Math.Max(_padding, Math.Min(_height - _padding, y)));
        }
    }
}
