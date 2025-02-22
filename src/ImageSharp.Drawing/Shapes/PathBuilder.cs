// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SixLabors.ImageSharp.Drawing
{
    /// <summary>
    /// Allow you to derivatively build shapes and paths.
    /// </summary>
    public class PathBuilder
    {
        private readonly List<Figure> figures = new List<Figure>();
        private readonly Matrix3x2 defaultTransform;
        private Figure currentFigure = null;
        private Matrix3x2 currentTransform;
        private Matrix3x2 setTransform;

        /// <summary>
        /// Initializes a new instance of the <see cref="PathBuilder" /> class.
        /// </summary>
        public PathBuilder()
            : this(Matrix3x2.Identity)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PathBuilder"/> class.
        /// </summary>
        /// <param name="defaultTransform">The default transform.</param>
        public PathBuilder(Matrix3x2 defaultTransform)
        {
            this.defaultTransform = defaultTransform;
            this.Clear();
            this.ResetTransform();
        }

        /// <summary>
        /// Sets the translation to be applied to all items to follow being applied to the <see cref="PathBuilder"/>.
        /// </summary>
        /// <param name="translation">The translation.</param>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder SetTransform(Matrix3x2 translation)
        {
            this.setTransform = translation;
            this.currentTransform = this.setTransform * this.defaultTransform;
            return this;
        }

        /// <summary>
        /// Sets the origin all subsequent point should be relative to.
        /// </summary>
        /// <param name="origin">The origin.</param>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder SetOrigin(PointF origin)
        {
            // the new origin should be transofrmed based on the default transform
            this.setTransform.Translation = origin;
            this.currentTransform = this.setTransform * this.defaultTransform;

            return this;
        }

        /// <summary>
        /// Resets the translation to the default.
        /// </summary>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder ResetTransform()
        {
            this.setTransform = Matrix3x2.Identity;
            this.currentTransform = this.setTransform * this.defaultTransform;

            return this;
        }

        /// <summary>
        /// Resets the origin to the default.
        /// </summary>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder ResetOrigin()
        {
            this.setTransform.Translation = Vector2.Zero;
            this.currentTransform = this.setTransform * this.defaultTransform;

            return this;
        }

        /// <summary>
        /// Adds the line connecting the current point to the new point.
        /// </summary>
        /// <param name="start">The start.</param>
        /// <param name="end">The end.</param>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder AddLine(PointF start, PointF end)
        {
            end = PointF.Transform(end, this.currentTransform);
            start = PointF.Transform(start, this.currentTransform);
            this.currentFigure.AddSegment(new LinearLineSegment(start, end));

            return this;
        }

        /// <summary>
        /// Adds the line connecting the current point to the new point.
        /// </summary>
        /// <param name="x1">The x1.</param>
        /// <param name="y1">The y1.</param>
        /// <param name="x2">The x2.</param>
        /// <param name="y2">The y2.</param>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder AddLine(float x1, float y1, float x2, float y2)
        {
            this.AddLine(new PointF(x1, y1), new PointF(x2, y2));

            return this;
        }

        /// <summary>
        /// Adds a series of line segments connecting the current point to the new points.
        /// </summary>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder AddLines(IEnumerable<PointF> points)
        {
            if (points is null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            this.AddLines(points.ToArray());

            return this;
        }

        /// <summary>
        /// Adds a series of line segments connecting the current point to the new points.
        /// </summary>
        /// <param name="points">The points.</param>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder AddLines(params PointF[] points)
        {
            this.AddSegment(new LinearLineSegment(points));

            return this;
        }

        /// <summary>
        /// Adds the segment.
        /// </summary>
        /// <param name="segment">The segment.</param>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder AddSegment(ILineSegment segment)
        {
            this.currentFigure.AddSegment(segment.Transform(this.currentTransform));

            return this;
        }

        /// <summary>
        /// Adds a quadratic bezier curve to the current figure joining the last point to the endPoint.
        /// </summary>
        /// <param name="startPoint">The start point.</param>
        /// <param name="controlPoint">The control point1.</param>
        /// <param name="endPoint">The end point.</param>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder AddBezier(PointF startPoint, PointF controlPoint, PointF endPoint)
        {
            Vector2 startPointVector = startPoint;
            Vector2 controlPointVector = controlPoint;
            Vector2 endPointVector = endPoint;

            Vector2 c1 = (((controlPointVector - startPointVector) * 2) / 3) + startPointVector;
            Vector2 c2 = (((controlPointVector - endPointVector) * 2) / 3) + endPointVector;

            this.AddBezier(startPointVector, c1, c2, endPoint);

            return this;
        }

        /// <summary>
        /// Adds a cubic bezier curve to the current figure joining the last point to the endPoint.
        /// </summary>
        /// <param name="startPoint">The start point.</param>
        /// <param name="controlPoint1">The control point1.</param>
        /// <param name="controlPoint2">The control point2.</param>
        /// <param name="endPoint">The end point.</param>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder AddBezier(PointF startPoint, PointF controlPoint1, PointF controlPoint2, PointF endPoint)
        {
            this.currentFigure.AddSegment(new CubicBezierLineSegment(
                PointF.Transform(startPoint, this.currentTransform),
                PointF.Transform(controlPoint1, this.currentTransform),
                PointF.Transform(controlPoint2, this.currentTransform),
                PointF.Transform(endPoint, this.currentTransform)));

            return this;
        }

        /// <summary>
        /// Adds an elliptical arc to the current  figure
        /// </summary>
        /// <param name="rect"> A <see cref="RectangleF"/> that represents the rectangular bounds of the ellipse from which the arc is taken.</param>
        /// <param name="rotation">The rotation of (<paramref name="rect"/>, measured in degrees clockwise.</param>
        /// <param name="startAngle">The Start angle of the ellipsis, measured in degrees anticlockwise from the Y-axis.</param>
        /// <param name="sweepAngle"> The angle between (<paramref name="startAngle"/> and the end of the arc. </param>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder AddEllipticalArc(RectangleF rect, float rotation, float startAngle, float sweepAngle) => this.AddEllipticalArc((rect.Right + rect.Left) / 2, (rect.Bottom + rect.Top) / 2, rect.Width / 2, rect.Height / 2, rotation, startAngle, sweepAngle);

        /// <summary>
        /// Adds an elliptical arc to the current  figure
        /// </summary>
        /// <param name="rect"> A <see cref="Rectangle"/> that represents the rectangular bounds of the ellipse from which the arc is taken.</param>
        /// <param name="rotation">The rotation of (<paramref name="rect"/>, measured in degrees clockwise.</param>
        /// <param name="startAngle">The Start angle of the ellipsis, measured in degrees anticlockwise from the Y-axis.</param>
        /// <param name="sweepAngle"> The angle between (<paramref name="startAngle"/> and the end of the arc. </param>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder AddEllipticalArc(Rectangle rect, int rotation, int startAngle, int sweepAngle) => this.AddEllipticalArc((float)(rect.Right + rect.Left) / 2, (float)(rect.Bottom + rect.Top) / 2, (float)rect.Width / 2, (float)rect.Height / 2, rotation, startAngle, sweepAngle);

        /// <summary>
        /// Adds an elliptical arc to the current  figure
        /// </summary>
        /// <param name="center"> The center <see cref="PointF"/> of the ellips from which the arc is taken.</param>
        /// <param name="radiusX">X radius of the ellipsis.</param>
        /// <param name="radiusY">Y radius of the ellipsis.</param>
        /// <param name="rotation">The rotation of (<paramref name="radiusX"/> to the X-axis and (<paramref name="radiusY"/> to the Y-axis, measured in degrees clockwise.</param>
        /// <param name="startAngle">The Start angle of the ellipsis, measured in degrees anticlockwise from the Y-axis.</param>
        /// <param name="sweepAngle"> The angle between (<paramref name="startAngle"/> and the end of the arc. </param>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder AddEllipticalArc(PointF center, float radiusX, float radiusY, float rotation, float startAngle, float sweepAngle) => this.AddEllipticalArc(center.X, center.Y, radiusX, radiusY, rotation, startAngle, sweepAngle);

        /// <summary>
        /// Adds an elliptical arc to the current  figure
        /// </summary>
        /// <param name="center"> The center <see cref="Point"/> of the ellips from which the arc is taken.</param>
        /// <param name="radiusX">X radius of the ellipsis.</param>
        /// <param name="radiusY">Y radius of the ellipsis.</param>
        /// <param name="rotation">The rotation of (<paramref name="radiusX"/> to the X-axis and (<paramref name="radiusY"/> to the Y-axis, measured in degrees clockwise.</param>
        /// <param name="startAngle">The Start angle of the ellipsis, measured in degrees anticlockwise from the Y-axis.</param>
        /// <param name="sweepAngle"> The angle between (<paramref name="startAngle"/> and the end of the arc. </param>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder AddEllipticalArc(Point center, int radiusX, int radiusY, int rotation, int startAngle, int sweepAngle) => this.AddEllipticalArc(center.X, center.Y, radiusX, radiusY, rotation, startAngle, sweepAngle);

        /// <summary>
        /// Adds an elliptical arc to the current  figure
        /// </summary>
        /// <param name="x"> The x-coordinate of the center point of the ellips from which the arc is taken.</param>
        /// <param name="y"> The y-coordinate of the center point of the ellips from which the arc is taken.</param>
        /// <param name="radiusX">X radius of the ellipsis.</param>
        /// <param name="radiusY">Y radius of the ellipsis.</param>
        /// <param name="rotation">The rotation of (<paramref name="radiusX"/> to the X-axis and (<paramref name="radiusY"/> to the Y-axis, measured in degrees clockwise.</param>
        /// <param name="startAngle">The Start angle of the ellipsis, measured in degrees anticlockwise from the Y-axis.</param>
        /// <param name="sweepAngle"> The angle between (<paramref name="startAngle"/> and the end of the arc. </param>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder AddEllipticalArc(int x, int y, int radiusX, int radiusY, int rotation, int startAngle, int sweepAngle)
        {
            this.currentFigure.AddSegment(new EllipticalArcLineSegment(x, y, radiusX, radiusY, rotation, startAngle, sweepAngle, this.currentTransform));

            return this;
        }

        /// <summary>
        /// Adds an elliptical arc to the current  figure
        /// </summary>
        /// <param name="x"> The x-coordinate of the center point of the ellips from which the arc is taken.</param>
        /// <param name="y"> The y-coordinate of the center point of the ellips from which the arc is taken.</param>
        /// <param name="radiusX">X radius of the ellipsis.</param>
        /// <param name="radiusY">Y radius of the ellipsis.</param>
        /// <param name="rotation">The rotation of (<paramref name="radiusX"/> to the X-axis and (<paramref name="radiusY"/> to the Y-axis, measured in degrees clockwise.</param>
        /// <param name="startAngle">The Start angle of the ellipsis, measured in degrees anticlockwise from the Y-axis.</param>
        /// <param name="sweepAngle"> The angle between (<paramref name="startAngle"/> and the end of the arc. </param>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder AddEllipticalArc(float x, float y, float radiusX, float radiusY, float rotation, float startAngle, float sweepAngle)
        {
            this.currentFigure.AddSegment(new EllipticalArcLineSegment(x, y, radiusX, radiusY, rotation, startAngle, sweepAngle, this.currentTransform));

            return this;
        }

        /// <summary>
        /// Starts a new figure but leaves the previous one open.
        /// </summary>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder StartFigure()
        {
            if (!this.currentFigure.IsEmpty)
            {
                this.currentFigure = new Figure();
                this.figures.Add(this.currentFigure);
            }
            else
            {
                this.currentFigure.IsClosed = false;
            }

            return this;
        }

        /// <summary>
        /// Closes the current figure.
        /// </summary>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder CloseFigure()
        {
            this.currentFigure.IsClosed = true;
            this.StartFigure();

            return this;
        }

        /// <summary>
        /// Closes the current figure.
        /// </summary>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder CloseAllFigures()
        {
            foreach (Figure f in this.figures)
            {
                f.IsClosed = true;
            }

            this.CloseFigure();

            return this;
        }

        /// <summary>
        /// Builds a complex polygon fromn the current working set of working operations.
        /// </summary>
        /// <returns>The current set of operations as a complex polygon</returns>
        public IPath Build()
        {
            IPath[] paths = this.figures.Where(x => !x.IsEmpty).Select(x => x.Build()).ToArray();
            if (paths.Length == 1)
            {
                return paths[0];
            }

            return new ComplexPolygon(paths);
        }

        /// <summary>
        /// Resets this instance, clearing any drawn paths and reseting any transforms.
        /// </summary>
        /// <returns>The <see cref="PathBuilder"/></returns>
        public PathBuilder Reset()
        {
            this.Clear();
            this.ResetTransform();

            return this;
        }

        /// <summary>
        /// Clears all drawn paths, Leaving any applied transforms.
        /// </summary>
        public void Clear()
        {
            this.currentFigure = new Figure();
            this.figures.Clear();
            this.figures.Add(this.currentFigure);
        }

        private class Figure
        {
            private readonly List<ILineSegment> segments = new List<ILineSegment>();

            public bool IsClosed { get; set; } = false;

            public bool IsEmpty => this.segments.Count == 0;

            public void AddSegment(ILineSegment segment) => this.segments.Add(segment);

            public IPath Build()
                => this.IsClosed
                ? new Polygon(this.segments.ToArray())
                : new Path(this.segments.ToArray());
        }
    }
}
