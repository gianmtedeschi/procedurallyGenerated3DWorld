using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Distributions.PoissonDiscSampling_VariableDensity
{
    public static class PoissonDiscSamling_VariableDensity
    {
        public static Tuple<Vector2, float>[] PoissonDistribute(uint seed, Vector2 origin, Vector2 size,
                    float rMin, float rMax, float densityNoiseScale, 
                    out List<int>[,] gridCells, out int numCells_x, out int numCells_y, out float cellEdge, AnimationCurve ac)
        {

            Random rnd = new Random(seed);

            float offset = rnd.NextFloat(0, size.x*size.x*size.x);

            // variables initialization
            float edgeLen = rMax /*/ Mathf.Sqrt(2)*/;

            // number of spawned points for each point (arbitrary)
            int k = 20;

            int
                numCellsX = Mathf.CeilToInt(size.x / edgeLen),
                numCellsY = Mathf.CeilToInt(size.y / edgeLen);

            List<int>[,] grid = new List<int>[numCellsX, numCellsY];

            List<int> tempPoints = new List<int>();

            List<Tuple<Vector2, float>> points = new List<Tuple<Vector2, float>>();

            for (int i = 0; i < numCellsX; i++)
            {
                for (int j = 0; j < numCellsY; j++)
                {
                    grid[i, j] = new List<int>();
                }
            }

            // random points inside grid
            Vector2 initPoint = new Vector2(origin.x + (rnd.NextFloat()%size.x),
                                           origin.y + (rnd.NextFloat() % size.y));

            points.Add(new Tuple<Vector2, float>(initPoint, SampleNoise(offset, initPoint.x, initPoint.y, rMin, rMax, densityNoiseScale, ac)));

            tempPoints.Add(0);

            grid[Mathf.FloorToInt(initPoint.x / edgeLen),
                Mathf.FloorToInt(initPoint.y / edgeLen)].Add(0);


            // actual algorithm, similar to the one described here: https://nicholasdwork.com/papers/fastVDPD.pdf
            while (tempPoints.Count > 0)
            {
                int n = tempPoints.Count;

                int currentIndex = (int)rnd.NextFloat(0, n);

                Vector2 currentPoint = points[tempPoints[currentIndex]].Item1;

                float currentRadius = points[tempPoints[currentIndex]].Item2;

                bool atLeastOne = false;

                // foreach random point on circle
                for (int i = 0; i < k; i++)
                {
                    float random = rnd.NextFloat(0, Mathf.PI *2);

                    Vector2 randomDir = new Vector2((float)Math.Cos(random), (float)Math.Sin(random));

                    Vector2 candidatePosition = currentPoint + randomDir *rnd.NextFloat(currentRadius, currentRadius*2);

                    float candidateRadius = SampleNoise(offset, candidatePosition.x, candidatePosition.y, rMin, rMax, densityNoiseScale, ac);

                    if (IsValid(candidatePosition, candidateRadius, origin, size, grid, edgeLen,numCellsX, numCellsY, points))
                    {
                        // add to points collection and add the index to both the grid and the tempPoints collection
                        points.Add(new Tuple<Vector2, float>(candidatePosition, candidateRadius));

                        tempPoints.Add(points.Count-1);

                        grid[Mathf.FloorToInt(candidatePosition.x / edgeLen),
                            Mathf.FloorToInt(candidatePosition.y / edgeLen)].Add(points.Count-1);

                        atLeastOne = true;
                    }

                }

                // if none of the candidates passed the test remove the currentPoint from the tempPoints collection
                if (!atLeastOne)
                
                    tempPoints.RemoveAt(currentIndex);
                
            }

            gridCells = grid;
            numCells_x = numCellsX;
            numCells_y = numCellsY;
            cellEdge = edgeLen;

            return points.ToArray();
        }

        private static float SampleNoise(float offset, float u, float v, float rMin, float rMax, float scale, AnimationCurve ac)
        {
            return Mathf.Lerp(rMin, rMax, ac.Evaluate(Mathf.PerlinNoise((u+offset) * scale , (v+offset) * scale )));
        }

        private static bool IsValid(Vector2 candidatePosition, float candidateRadius, Vector2 origin, Vector2 size, List<int>[,] grid, float edgeLen,int numCells_x, int numCells_y, List<Tuple<Vector2, float>> points)
        {
            // check if the candidate is inside the boundaries
            if (candidatePosition.x < origin.x || candidatePosition.x > origin.x + size.x ||
                candidatePosition.y < origin.y || candidatePosition.y > origin.y + size.y)

                return false;

            // check for the worst case scenario=>maxRadius
            int
                currentIndex_x = Mathf.FloorToInt(candidatePosition.x / edgeLen),
                currentIndex_y = Mathf.FloorToInt(candidatePosition.y / edgeLen);

            for (int i = currentIndex_x - 1; i < currentIndex_x + 2; i++)
            {
                for (int j = currentIndex_y - 1; j < currentIndex_y + 2; j++)
                {
                    if (i < 0 || i >= numCells_x ||
                        j < 0 || j >= numCells_y)
                        continue;

                    foreach (int index in grid[i, j])
                    {
                        float dist = Vector2.Distance(candidatePosition, points[index].Item1); // distance squared?? removed??

                        if (!(dist >= candidateRadius && dist >= points[index].Item2))
                            return false;
                    }
                }
            }




            return true;
        }
    }
}
