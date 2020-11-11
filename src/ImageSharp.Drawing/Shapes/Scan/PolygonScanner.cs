// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Drawing.Utilities;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Drawing.Shapes.Scan
{
    internal ref struct PolygonScanner
    {
        private readonly int minY;
        private readonly int maxY;
        private readonly int subsampling;
        private readonly int counterMax;
        private readonly IntersectionRule intersectionRule;
        private readonly MemoryAllocator allocator;
        private ScanEdgeCollection edgeCollection;
        private Span<ScanEdge> edges;

        // Common contiguous buffer for sorted0, sorted1, intersections, activeEdges [,edgeUpAtIntersections]
        private IMemoryOwner<int> dataBuffer;

        // | <- edgeCnt -> | <- edgeCnt -> | <- edgeCnt -> | <- maxIntersectionCount -> | <- maxIntersectionCount -> |
        // |---------------|---------------|---------------|----------------------------|----------------------------|
        // | sorted0       | sorted1       | activeEdges   | intersections              | intersectionTypes          |
        // |---------------|---------------|---------------|----------------------------|----------------------------|
        private Span<int> sorted0;
        private Span<int> sorted1;
        private ActiveEdgeList activeEdges;
        private Span<float> intersections;
        private Span<NonZeroIntersectionType> intersectionTypes;

        private int idx0;
        private int idx1;
        private float yPlusOne;

        public readonly float SubpixelFraction;
        public int PixelLineY;
        public float SubPixelY;

        private PolygonScanner(
            ScanEdgeCollection edgeCollection,
            int maxIntersectionCount,
            int minY,
            int maxY,
            int subsampling,
            IntersectionRule intersectionRule,
            MemoryAllocator allocator)
        {
            this.minY = minY;
            this.maxY = maxY;
            this.subsampling = subsampling;
            this.SubpixelFraction = 1f / subsampling;
            this.intersectionRule = intersectionRule;
            this.allocator = allocator;
            this.edgeCollection = edgeCollection;
            this.edges = edgeCollection.Edges;
            this.counterMax = ((maxY - minY) * subsampling) + 1;
            int edgeCount = this.edges.Length;
            int dataBufferSize = (edgeCount * 3) + maxIntersectionCount;

            // In case of IntersectionRule.Nonzero, we need to allocate space for edgeUpAtIntersections:
            if (intersectionRule == IntersectionRule.Nonzero)
            {
                dataBufferSize += maxIntersectionCount;
            }

            this.dataBuffer = allocator.Allocate<int>(dataBufferSize);
            Span<int> dataBufferInt32Span = this.dataBuffer.Memory.Span;
            Span<float> dataBufferFloatSpan = MemoryMarshal.Cast<int, float>(dataBufferInt32Span);

            this.sorted0 = dataBufferInt32Span.Slice(0, edgeCount);
            this.sorted1 = dataBufferInt32Span.Slice(edgeCount, edgeCount);
            this.activeEdges = new ActiveEdgeList(dataBufferInt32Span.Slice(edgeCount * 2, edgeCount));
            this.intersections = dataBufferFloatSpan.Slice(edgeCount * 3, maxIntersectionCount);
            if (intersectionRule == IntersectionRule.Nonzero)
            {
                Span<int> remainder =
                    dataBufferInt32Span.Slice((edgeCount * 3) + maxIntersectionCount, maxIntersectionCount);
                this.intersectionTypes = MemoryMarshal.Cast<int, NonZeroIntersectionType>(remainder);
            }
            else
            {
                this.intersectionTypes = default;
            }

            this.idx0 = 0;
            this.idx1 = 0;
            this.PixelLineY = minY - 1;
            this.SubPixelY = default;
            this.yPlusOne = default;
        }

        public static PolygonScanner Create(
            IPath polygon,
            int minY,
            int maxY,
            int subsampling,
            IntersectionRule intersectionRule,
            MemoryAllocator allocator,
            OrientationHandling orientationHandling = OrientationHandling.FirstRingIsContourFollowedByHoles)
        {
            TessellatedMultipolygon multipolygon = TessellatedMultipolygon.Create(polygon, allocator, orientationHandling);
            ScanEdgeCollection edges = ScanEdgeCollection.Create(multipolygon, allocator, subsampling);
            PolygonScanner scanner = new PolygonScanner(edges, multipolygon.TotalVertexCount * 2, minY, maxY, subsampling, intersectionRule, allocator);
            scanner.Init();
            return scanner;
        }

        public static PolygonScanner Create(
            Region region,
            int minY,
            int maxY,
            int subsampling,
            IntersectionRule intersectionRule,
            Configuration configuration,
            OrientationHandling orientationHandling = OrientationHandling.FirstRingIsContourFollowedByHoles)
            => Create(region.Shape, minY, maxY, subsampling, intersectionRule, configuration.MemoryAllocator, orientationHandling);

        private void Init()
        {
            // Reuse memory buffers of 'intersections' and 'activeEdges' for key-value sorting,
            // since that region is unused at initialization time.
            Span<float> keys0 = this.intersections.Slice(0, this.sorted0.Length);
            Span<float> keys1 = MemoryMarshal.Cast<int, float>(this.activeEdges.Buffer);

            for (int i = 0; i < this.edges.Length; i++)
            {
                ref ScanEdge edge = ref this.edges[i];
                keys0[i] = edge.Y0;
                keys1[i] = edge.Y1;
                this.sorted0[i] = i;
                this.sorted1[i] = i;
            }

            SortUtility.Sort(keys0, this.sorted0);
            SortUtility.Sort(keys1, this.sorted1);

            this.SkipEdgesBeforeMinY();
        }

        private void SkipEdgesBeforeMinY()
        {
            if (this.edges.Length == 0)
            {
                return;
            }

            this.SubPixelY = this.edges[this.sorted0[0]].Y0;

            int i0 = 1;
            int i1 = 0;

            // Do fake scans for the lines belonging to edge start and endpoints before minY
            while (this.SubPixelY < this.minY)
            {
                this.EnterEdges();
                this.LeaveEdges();
                this.activeEdges.RemoveLeavingEdges();

                float y0 = this.edges[this.sorted0[i0]].Y0;
                float y1 = this.edges[this.sorted1[i1]].Y1;

                if (y0 < y1)
                {
                    this.SubPixelY = y0;
                    i0++;
                }
                else
                {
                    this.SubPixelY = y1;
                    i1++;
                }
            }
        }

        public bool MoveToNextPixelLine()
        {
            this.PixelLineY++;
            this.yPlusOne = this.PixelLineY + 1;
            this.SubPixelY = this.PixelLineY - this.SubpixelFraction;
            return this.PixelLineY < this.maxY;
        }

        public bool MoveToNextSubpixelScanLine()
        {
            this.SubPixelY += this.SubpixelFraction;
            this.EnterEdges();
            this.LeaveEdges();
            return this.SubPixelY < this.yPlusOne;
        }

        public ReadOnlySpan<float> ScanCurrentLine()
        {
            return this.intersectionRule == IntersectionRule.OddEven
                ? this.activeEdges.ScanOddEven(this.SubPixelY, this.edges, this.intersections)
                : this.activeEdges.ScanNonZero(this.SubPixelY, this.edges, this.intersections, this.intersectionTypes);
        }

        public void Dispose()
        {
            this.edgeCollection.Dispose();
            this.dataBuffer.Dispose();
        }

        private void EnterEdges()
        {
            while (this.idx0 < this.sorted0.Length)
            {
                int edge0 = this.sorted0[this.idx0];
                if (this.edges[edge0].Y0 > this.SubPixelY)
                {
                    break;
                }

                this.activeEdges.EnterEdge(edge0);
                this.idx0++;
            }
        }

        private void LeaveEdges()
        {
            while (this.idx1 < this.sorted1.Length)
            {
                int edge1 = this.sorted1[this.idx1];
                if (this.edges[edge1].Y1 > this.SubPixelY)
                {
                    break;
                }

                this.activeEdges.LeaveEdge(edge1);
                this.idx1++;
            }
        }
    }
}
