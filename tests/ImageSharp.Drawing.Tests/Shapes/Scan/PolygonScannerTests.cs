// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Numerics;
using System.Text;
using SixLabors.ImageSharp.Drawing.Shapes;
using SixLabors.ImageSharp.Drawing.Shapes.Scan;
using SixLabors.ImageSharp.Drawing.Tests.TestUtilities;
using Xunit;
using Xunit.Abstractions;
using IOPath = System.IO.Path;

namespace SixLabors.ImageSharp.Drawing.Tests.Shapes.Scan
{
    public class PolygonScannerTests
    {
        private readonly ITestOutputHelper output;

        private static readonly DebugDraw DebugDraw = new DebugDraw(nameof(PolygonScannerTests));

        public PolygonScannerTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        private void PrintPoints(ReadOnlySpan<PointF> points)
        {
            StringBuilder sb = new StringBuilder();

            foreach (PointF p in points)
            {
                sb.Append($"({p.X},{p.Y}), ");
            }

            this.output.WriteLine(sb.ToString());
        }


        private void PrintPointsX(PointF[] isc)
        {
            string s = string.Join(",", isc.Select(p => $"{p.X}"));
            this.output.WriteLine(s);
        }

        private static void VerifyScanline(ReadOnlySpan<FuzzyFloat> expected, ReadOnlySpan<float> actual,
            string scanlineId)
        {
            if (expected == null)
            {
                return;
            }

            Assert.True(expected.Length == actual.Length,
                $"Scanline had {actual.Length} intersections instead of {expected.Length}: {scanlineId}");

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(expected[i].Equals(actual[i]), $"Mismatch at scanline {scanlineId}: {expected[i]} != {actual[i]}");
            }
        }

        private void TestScan(IPath path, int min, int max, int subsampling, FuzzyFloat[][] expected) =>
            TestScan(path, min, max, subsampling, expected, IntersectionRule.OddEven);

        private void TestScan(
            IPath path,
            int min,
            int max,
            int subsampling,
            FuzzyFloat[][] expected,
            IntersectionRule intersectionRule,
            OrientationHandling orientationHandling = OrientationHandling.FirstRingIsContourFollowedByHoles)
        {
            var scanner = PolygonScanner.Create(path,
                min,
                max,
                subsampling,
                intersectionRule,
                Configuration.Default.MemoryAllocator,
                orientationHandling);

            try
            {
                int counter = 0;
                while (scanner.MoveToNextPixelLine())
                {
                    while (scanner.MoveToNextSubpixelScanLine())
                    {
                        ReadOnlySpan<float> intersections = scanner.ScanCurrentLine();
                        VerifyScanline(expected[counter], intersections, $"Y={scanner.SubPixelY} Cnt={counter}");

                        counter++;
                    }
                }

                Assert.Equal(expected.Length, counter + 1);
            }
            finally
            {
                scanner.Dispose();
            }
        }

        [Fact]
        public void BasicConcave00()
        {
            IPath poly = PolygonFactory.CreatePolygon((2, 2), (5, 3), (5, 6), (8, 6), (8, 9), (5, 11), (2, 7));
            DebugDraw.Polygon(poly, 1f, 50f);

            FuzzyFloat[][] expected =
            {
                new FuzzyFloat[] {2, 2},
                new FuzzyFloat[] {2, 5},
                new FuzzyFloat[] {2, 5},
                new FuzzyFloat[] {2, 5},
                new FuzzyFloat[] {2, 5, 5, 8},
                new FuzzyFloat[] {2, 8},
                new FuzzyFloat[] {2.75f, 8},
                new FuzzyFloat[] {3.5f, 8},
                new FuzzyFloat[] {4.25f, 6.5f},
                new FuzzyFloat[] {5, 5},
            };

            TestScan(poly, 2, 11, 1, expected);
        }

        [Fact]
        public void BasicConcave01()
        {
            IPath poly = PolygonFactory.CreatePolygon((0, 0), (10, 10), (20, 0), (20, 20), (0, 20));
            DebugDraw.Polygon(poly);

            FuzzyFloat[][] expected =
            {
                new FuzzyFloat[] {0f, 0f, 20.000000f, 20.000000f,},
                new FuzzyFloat[] {0f, 1.0000000f, 19.000000f, 20.000000f},
                new FuzzyFloat[] {0f, 2.0000000f, 18.000000f, 20.000000f},
                new FuzzyFloat[] {0f, 3.0000000f, 17.000000f, 20.000000f},
                new FuzzyFloat[] {0f, 4.0000000f, 16.000000f, 20.000000f},
                new FuzzyFloat[] {0f, 5.0000000f, 15.000000f, 20.000000f},
                new FuzzyFloat[] {0f, 6.0000000f, 14.000000f, 20.000000f},
                new FuzzyFloat[] {0f, 7.0000000f, 13.000000f, 20.000000f},
                new FuzzyFloat[] {0f, 8.0000000f, 12.000000f, 20.000000f},
                new FuzzyFloat[] {0f, 9.0000000f, 11.000000f, 20.000000f},
                new FuzzyFloat[] {0f, 10.000000f, 10.000000f, 20.000000f},
                new FuzzyFloat[] {0f, 20.000000f},
                new FuzzyFloat[] {0f, 20.000000f},
                new FuzzyFloat[] {0f, 20.000000f},
                new FuzzyFloat[] {0f, 20.000000f},
                new FuzzyFloat[] {0f, 20.000000f},
                new FuzzyFloat[] {0f, 20.000000f},
                new FuzzyFloat[] {0f, 20.000000f},
                new FuzzyFloat[] {0f, 20.000000f},
                new FuzzyFloat[] {0f, 20.000000f},
                new FuzzyFloat[] {0f, 20.000000f},
            };

            TestScan(poly, 0, 20, 1, expected);
        }

        [Fact]
        public void BasicConcave02()
        {
            IPath poly = PolygonFactory.CreatePolygon((0, 3), (3, 3), (3, 0), (1, 2), (1, 1), (0, 0));
            DebugDraw.Polygon(poly, 1f, 100f);

            FuzzyFloat[][] expected =
            {
                new FuzzyFloat[] {0f, 0f, 3.0000000f, 3.0000000f},
                new FuzzyFloat[] {0f, 0.50000000f, 2.5000000f, 3.0000000f},
                new FuzzyFloat[] {0f, 1.0000000f, 2.0000000f, 3.0000000f},
                new FuzzyFloat[] {0f, 1.0000000f, 1.5000000f, 3.0000000f},
                new FuzzyFloat[] {0f, 1.0000000f, 1.0000000f, 3.0000000f},
                new FuzzyFloat[] {0f, 3.0000000f},
                new FuzzyFloat[] {0f, 3.0000000f},
            };
            TestScan(poly, 0, 3, 2, expected);
        }

        [Fact]
        public void BasicConcave03()
        {
            IPath poly = PolygonFactory.CreatePolygon((0, 0), (2, 0), (3, 1), (3, 0), (6, 0), (6, 2), (5, 2), (5, 1),
                (4, 1), (4, 2), (2, 2), (1, 1), (0, 2));
            DebugDraw.Polygon(poly, 1f, 100f);

            FuzzyFloat[][] expected =
            {
                new FuzzyFloat[] {0f, 2.0000000f, 3.0000000f, 6.0000000f},
                new FuzzyFloat[] {0f, 2.2000000f, 3.0000000f, 6.0000000f},
                new FuzzyFloat[] {0f, 2.4000000f, 3.0000000f, 6.0000000f},
                new FuzzyFloat[] {0f, 2.6000000f, 3.0000000f, 6.0000000f},
                new FuzzyFloat[] {0f, 2.8000000f, 3.0000000f, 6.0000000f},
                new FuzzyFloat[]
                {
                    0f, 1.0000000f, 1.0000000f, 3.0000000f, 3.0000000f, 4.0000000f, 4.0000000f, 5.0000000f, 5.0000000f,
                    6.0000000f
                },
                new FuzzyFloat[] {0f, 0.80000000f, 1.2000000f, 4.0000000f, 5.0000000f, 6.0000000f},
                new FuzzyFloat[] {0f, 0.60000000f, 1.4000000f, 4.0000000f, 5.0000000f, 6.0000000f},
                new FuzzyFloat[] {0f, 0.40000000f, 1.6000000f, 4.0000000f, 5.0000000f, 6.0000000f},
                new FuzzyFloat[] {0f, 0.20000000f, 1.8000000f, 4.0000000f, 5.0000000f, 6.0000000f},
                new FuzzyFloat[] {0f, 0f, 2.0000000f, 4.0000000f, 5.0000000f, 6.0000000f},
            };
            TestScan(poly, 0, 2, 5, expected);
        }

        [Fact]
        public void SelfIntersecting01()
        {
            // TODO: This case is not handled intuitively with the current rules
            IPath poly = PolygonFactory.CreatePolygon((0, 0), (10, 0), (0, 10), (10, 10));
            DebugDraw.Polygon(poly, 10f, 10f);

            FuzzyFloat[][] expected =
            {
                new FuzzyFloat[] {0f, 10.000000f},
                new FuzzyFloat[] {0.50000000f, 9.5000000f},
                new FuzzyFloat[] {1.0000000f, 9.0000000f},
                new FuzzyFloat[] {1.5000000f, 8.5000000f},
                new FuzzyFloat[] {2.0000000f, 8.0000000f},
                new FuzzyFloat[] {2.5000000f, 7.5000000f},
                new FuzzyFloat[] {3.0000000f, 7.0000000f},
                new FuzzyFloat[] {3.5000000f, 6.5000000f},
                new FuzzyFloat[] {4.0000000f, 6.0000000f},
                new FuzzyFloat[] {4.5000000f, 5.5000000f},
                new FuzzyFloat[] {5.0000000f, 5.0000000f},
                new FuzzyFloat[] {4.5000000f, 5.5000000f},
                new FuzzyFloat[] {4.0000000f, 6.0000000f},
                new FuzzyFloat[] {3.5000000f, 6.5000000f},
                new FuzzyFloat[] {3.0000000f, 7.0000000f},
                new FuzzyFloat[] {2.5000000f, 7.5000000f},
                new FuzzyFloat[] {2.0000000f, 8.0000000f},
                new FuzzyFloat[] {1.5000000f, 8.5000000f},
                new FuzzyFloat[] {1.0000000f, 9.0000000f},
                new FuzzyFloat[] {0.50000000f, 9.5000000f},
                new FuzzyFloat[] {0f, 10.000000f},
            };
            TestScan(poly, 0, 10, 2, expected);
        }

        [Fact]
        public void SelfIntersecting02()
        {
            IPath poly = PolygonFactory.CreatePolygon((0, 0), (10, 10), (10, 0), (0, 10));
            DebugDraw.Polygon(poly, 10f, 10f);

            FuzzyFloat[][] expected =
            {
                new FuzzyFloat[] {0f, 0f, 10.000000f, 10.000000f},
                new FuzzyFloat[] {0f, 0.50000000f, 9.5000000f, 10.000000f},
                new FuzzyFloat[] {0f, 1.0000000f, 9.0000000f, 10.000000f},
                new FuzzyFloat[] {0f, 1.5000000f, 8.5000000f, 10.000000f},
                new FuzzyFloat[] {0f, 2.0000000f, 8.0000000f, 10.000000f},
                new FuzzyFloat[] {0f, 2.5000000f, 7.5000000f, 10.000000f},
                new FuzzyFloat[] {0f, 3.0000000f, 7.0000000f, 10.000000f},
                new FuzzyFloat[] {0f, 3.5000000f, 6.5000000f, 10.000000f},
                new FuzzyFloat[] {0f, 4.0000000f, 6.0000000f, 10.000000f},
                new FuzzyFloat[] {0f, 4.5000000f, 5.5000000f, 10.000000f},
                new FuzzyFloat[] {0f, 5.0000000f, 5.0000000f, 10.000000f},
                new FuzzyFloat[] {0f, 4.5000000f, 5.5000000f, 10.000000f},
                new FuzzyFloat[] {0f, 4.0000000f, 6.0000000f, 10.000000f},
                new FuzzyFloat[] {0f, 3.5000000f, 6.5000000f, 10.000000f},
                new FuzzyFloat[] {0f, 3.0000000f, 7.0000000f, 10.000000f},
                new FuzzyFloat[] {0f, 2.5000000f, 7.5000000f, 10.000000f},
                new FuzzyFloat[] {0f, 2.0000000f, 8.0000000f, 10.000000f},
                new FuzzyFloat[] {0f, 1.5000000f, 8.5000000f, 10.000000f},
                new FuzzyFloat[] {0f, 1.0000000f, 9.0000000f, 10.000000f},
                new FuzzyFloat[] {0f, 0.50000000f, 9.5000000f, 10.000000f},
                new FuzzyFloat[] {0f, 0f, 10.000000f, 10.000000f},
            };
            TestScan(poly, 0, 10, 2, expected);
        }

        [Theory]
        [InlineData(IntersectionRule.OddEven)]
        [InlineData(IntersectionRule.Nonzero)]
        public void SelfIntersecting03(IntersectionRule rule)
        {
            IPath poly = PolygonFactory.CreatePolygon((1, 3), (1, 2), (5, 2), (5, 5), (2, 5), (2, 1), (3, 1), (3, 4),
                (4, 4), (4, 3), (1, 3));
            DebugDraw.Polygon(poly, 1f, 100f);

            FuzzyFloat[][] expected;
            if (rule == IntersectionRule.OddEven)
            {
                expected = new[]
                {
                    new FuzzyFloat[] {2.0000000f, 3.0000000f},
                    new FuzzyFloat[] {2.0000000f, 3.0000000f},
                    new FuzzyFloat[] {1.0000000f, 2.0000000f, 3.0000000f, 5.0000000f},
                    new FuzzyFloat[] {1.0000000f, 2.0000000f, 3.0000000f, 5.0000000f},
                    new FuzzyFloat[] {1.0000000f, 2.0000000f, 3.0000000f, 4.0000000f, 4.0000000f, 5.0000000f},
                    new FuzzyFloat[] {2.0000000f, 3.0000000f, 4.0000000f, 5.0000000f},
                    new FuzzyFloat[] {2.0000000f, 3.0000000f, 3.0000000f, 4.0000000f, 4.0000000f, 5.0000000f},
                    new FuzzyFloat[] {2.0000000f, 5.0000000f},
                    new FuzzyFloat[] {2.0000000f, 5.0000000f},
                };
            }
            else
            {
                expected = new[]
                {
                    new FuzzyFloat[] {2.0000000f, 3.0000000f},
                    new FuzzyFloat[] {2.0000000f, 3.0000000f},
                    new FuzzyFloat[] {1.0000000f, 5.0000000f},
                    new FuzzyFloat[] {1.0000000f, 5.0000000f},
                    new FuzzyFloat[] {1.0000000f, 4.0000000f, 4.0000000f, 5.0000000f},
                    new FuzzyFloat[] {2.0000000f, 3.0000000f, 4.0000000f, 5.0000000f},
                    new FuzzyFloat[] {2.0000000f, 3.0000000f, 3.0000000f, 4.0000000f, 4.0000000f, 5.0000000f},
                    new FuzzyFloat[] {2.0000000f, 5.0000000f},
                    new FuzzyFloat[] {2.0000000f, 5.0000000f},
                };
            }

            TestScan(poly, 1, 5, 2, expected, rule);
        }
        
        [Theory]
        [InlineData(IntersectionRule.OddEven)]
        [InlineData(IntersectionRule.Nonzero)]
        public void SelfIntersecting04(IntersectionRule rule)
        {
            IPath poly = PolygonFactory.CreatePolygon((1, 4), (1, 3), (3, 3), (3, 2), (2, 2), (2, 4), (1, 4), (1, 1),
                (4, 1), (4, 4), (3, 4), (3, 5), (2, 5), (2, 4), (1, 4));
            DebugDraw.Polygon(poly, 1f, 100f);
            
            FuzzyFloat[][] expected;
            if (rule == IntersectionRule.OddEven)
            {
                expected = new[]
                {
                    new FuzzyFloat[] {1, 4},
                    new FuzzyFloat[] {1, 4},
                    new FuzzyFloat[] {1, 2, 2, 3, 3, 4},
                    new FuzzyFloat[] {1, 2, 3, 4},
                    new FuzzyFloat[] {1, 1, 2, 3, 3, 4},
                    new FuzzyFloat[] {1, 1, 2, 4},
                    new FuzzyFloat[] {1, 1, 2, 2, 2, 3, 3, 4},
                    new FuzzyFloat[] {2, 3},
                    new FuzzyFloat[] {2, 3},
                };
            }
            else
            {
                expected = new[]
                {
                    new FuzzyFloat[] {1, 4},
                    new FuzzyFloat[] {1, 4},
                    new FuzzyFloat[] {1, 2, 2, 3, 3, 4},
                    new FuzzyFloat[] {1, 2, 3, 4},
                    new FuzzyFloat[] {1, 3, 3, 4},
                    new FuzzyFloat[] {1, 4},
                    new FuzzyFloat[] {1, 2, 2, 3, 3, 4 },
                    new FuzzyFloat[] {2, 3},
                    new FuzzyFloat[] {2, 3},
                };
            }
            
            TestScan(poly, 1, 5, 2, expected, rule);
        }
        
        
        [Fact]
        public void NegativeOrientation01()
        {
            IPath poly = PolygonFactory.CreatePolygon((0, 0), (0, 2), (2, 2), (2, 0));

            FuzzyFloat[][] expected =
            {
                new FuzzyFloat[] { 0, 0, 2, 2},
                new FuzzyFloat[] { 0, 2 },
                new FuzzyFloat[] { 0, 2 },
                new FuzzyFloat[] { 0, 2 },
                new FuzzyFloat[] { 0, 0, 2, 2},
            };
            
            TestScan(poly, 0, 2, 2, expected, IntersectionRule.OddEven, OrientationHandling.KeepOriginal);
        }

        private static (float y, FuzzyFloat[] x) Empty(float y) => (y, new FuzzyFloat[0]);

        private static FuzzyFloat F(float x, float eps) => new FuzzyFloat(x, eps);

        public static readonly TheoryData<string, (float y, FuzzyFloat[] x)[]> NumericCornerCasesData =
            new TheoryData<string, (float y, FuzzyFloat[] x)[]>
            {
                {
                    "A", new[]
                    {
                        Empty(2f), Empty(2.25f),

                        (2.5f, new FuzzyFloat[] {2, 11}),
                        (2.75f, new FuzzyFloat[] {2, 11}),
                        (3f, new FuzzyFloat[] {2, 8, 8, 11}),
                        (3.25f, new FuzzyFloat[] {11, 11}),

                        Empty(3.5f), Empty(3.75f), Empty(4f),
                    }
                },
                {
                    "B", new[]
                    {
                        Empty(2f), Empty(2.25f),

                        (2.5f, new FuzzyFloat[] {12, 21}),
                        (2.75f, new FuzzyFloat[] {12, 21}),
                        (3f, new FuzzyFloat[] {12, 15, 15, 21}),
                        (3.25f, new FuzzyFloat[] {18, 21}),

                        Empty(3.5f), Empty(3.75f), Empty(4f),
                    }
                },
                {
                    "C", new[]
                    {
                        Empty(3f), Empty(3.25f),

                        (3.5f, new FuzzyFloat[] {2, 8}),
                        (3.75f, new FuzzyFloat[] {2, 8}),
                        (4f, new FuzzyFloat[] {2, 8}),
                    }
                },
                {
                    "D", new[]
                    {
                        Empty(3f),

                        (3.25f, new FuzzyFloat[] {12, 12}),
                        (3.5f, new FuzzyFloat[] {12, 18}),
                        (3.75f, new FuzzyFloat[] {12, 15, 15, 18}),
                        (4f, new FuzzyFloat[] {12, 12, 18, 18}),
                    }
                },
                {
                    "E", new[]
                    {
                        Empty(4f), Empty(4.25f),

                        (4.5f, new FuzzyFloat[] {3, 3, 6, 6}),
                        (4.75f, new FuzzyFloat[] {F(2.4166667f, 0.5f), 4, 4, 6}),
                        (5f, new FuzzyFloat[] {2, 6}),
                    }
                },
                {
                    "F", new[]
                    {
                        Empty(4f),

                        (4.25f, new FuzzyFloat[] {13, 13}),
                        (4.5f, new FuzzyFloat[] {F(12.714286f, 0.5f), F(13.444444f, 0.5f), 16, 16}),
                        (4.75f, new FuzzyFloat[] {F(12.357143f, 0.5f), 14, 14, 16}),
                        (5f, new FuzzyFloat[] {12, 16}),
                    }
                },
                {
                    "G", new[]
                    {
                        Empty(1f), Empty(1.25f), Empty(1.5f),

                        (1.75f, new FuzzyFloat[] {6, 6}),
                        (2f, new FuzzyFloat[] {F(4.6315789f, 1f), F(7.3684211f, 1f)}),
                        (2.25f, new FuzzyFloat[] {2, 10}),

                        Empty(2.5f), Empty(1.75f), Empty(3f),
                    }
                },
                {
                    "H", new[]
                    {
                        Empty(1f), Empty(1.25f), Empty(1.5f),

                        (1.75f, new FuzzyFloat[] {16, 16}),
                        (2f, new FuzzyFloat[] {14, 14, 14, 16}), // this emits 2 dummy points, but normally it should not corrupt quality too much
                        (2.25f, new FuzzyFloat[] {16, 16}),

                        Empty(2.5f), Empty(1.75f), Empty(3f),
                    }
                }
            };

        [Theory]
        [MemberData(nameof(NumericCornerCasesData))]
        public void NumericCornerCases(string name, (float y, FuzzyFloat[] x)[] expectedIntersections)
        {
            Polygon poly = NumericCornerCasePolygons.GetByName(name);
            DebugDraw.Polygon(poly, 0.25f, 100f, $"{nameof(NumericCornerCases)}_{name}");

            int min = (int) expectedIntersections.First().y;
            int max = (int) expectedIntersections.Last().y;

            TestScan(poly, min, max, 4, expectedIntersections.Select(i => i.x).ToArray());
        }

        public static TheoryData<float, string, (float y, FuzzyFloat[] x)[]> NumericCornerCases_Offset_Data()
        {
            var result = new TheoryData<float, string, (float y, FuzzyFloat[] x)[]>();

            float[] offsets = {1e3f, 1e4f, 1e5f};

            foreach (float offset in offsets)
            {
                foreach (var data in NumericCornerCasesData)
                {
                    result.Add(offset, (string)data[0], ((float y, FuzzyFloat[] x)[]) data[1]);
                }
            }
            
            return result;
        }

        [Theory]
        [MemberData(nameof(NumericCornerCases_Offset_Data))]
        public void NumericCornerCases_Offset(float offset, string name, (float y, FuzzyFloat[] x)[] expectedIntersections)
        {
            float dx = offset;
            float dy = offset;
            
            IPath poly = NumericCornerCasePolygons.GetByName(name).Transform(Matrix3x2.CreateTranslation(dx, dy));
            expectedIntersections = TranslateIntersections(expectedIntersections, dx, dy);

            int min = (int) expectedIntersections.First().y;
            int max = (int) expectedIntersections.Last().y;

            TestScan(poly, min, max, 4, expectedIntersections.Select(i => i.x).ToArray());
        }

        
        private static (float y, FuzzyFloat[] x)[] TranslateIntersections(
            (float y, FuzzyFloat[] x)[] ex, float dx, float dy)
        {
            return ex.Select(e => (e.y + dy, e.x.Select(xx => xx + dx).ToArray())).ToArray();
        }
    }
}
