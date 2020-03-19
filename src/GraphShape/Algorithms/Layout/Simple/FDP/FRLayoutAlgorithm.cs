using System;
using System.Collections.Generic;
using QuikGraph;
using System.Windows;
using JetBrains.Annotations;

namespace GraphShape.Algorithms.Layout.Simple.FDP
{
    /// <summary>
    /// Fruchterman-Reingold layout algorithm.
    /// </summary>
    /// <typeparam name="TVertex">Vertex type.</typeparam>
    /// <typeparam name="TEdge">Edge type.</typeparam>
    /// <typeparam name="TGraph">Graph type</typeparam>
    public class FRLayoutAlgorithm<TVertex, TEdge, TGraph>
        : ParameterizedLayoutAlgorithmBase<TVertex, TEdge, TGraph, FRLayoutParametersBase>
        where TEdge : IEdge<TVertex>
        where TGraph : IVertexAndEdgeListGraph<TVertex, TEdge>
    {
        /// <summary>
        /// Actual temperature of the 'mass'.
        /// </summary>
        private double _temperature;

        private double _maxWidth = double.PositiveInfinity;
        private double _maxHeight = double.PositiveInfinity;

        /// <summary>
        /// Initializes a new instance of the <see cref="FRLayoutAlgorithm{TVertex,TEdge,TGraph}"/> class.
        /// </summary>
        /// <param name="visitedGraph">Graph to layout.</param>
        public FRLayoutAlgorithm([NotNull] TGraph visitedGraph)
            : base(visitedGraph)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FRLayoutAlgorithm{TVertex,TEdge,TGraph}"/> class.
        /// </summary>
        /// <param name="visitedGraph">Graph to layout.</param>
        /// <param name="verticesPositions">Vertices positions.</param>
        /// <param name="oldParameters">Optional algorithm parameters.</param>
        public FRLayoutAlgorithm(
            [NotNull] TGraph visitedGraph,
            [CanBeNull] IDictionary<TVertex, Point> verticesPositions,
            [CanBeNull] FRLayoutParametersBase oldParameters)
            : base(visitedGraph, verticesPositions, oldParameters)
        {
        }

        /// <inheritdoc />
        protected override FRLayoutParametersBase DefaultParameters { get; } = new FreeFRLayoutParameters();

        #region AlgorithmBase

        /// <inheritdoc />
        protected override void Initialize()
        {
            base.Initialize();

            // Initializing the positions
            if (Parameters is BoundedFRLayoutParameters boundedFRParams)
            {
                InitializeWithRandomPositions(boundedFRParams.Width, boundedFRParams.Height);
                _maxWidth = boundedFRParams.Width;
                _maxHeight = boundedFRParams.Height;
            }
            else
            {
                InitializeWithRandomPositions(10.0, 10.0);
            }

            Parameters.VertexCount = VisitedGraph.VertexCount;
        }

        /// <inheritdoc />
        protected override void InternalCompute()
        {
            // Actual temperature of the 'mass'. Used for cooling.
            double minimalTemperature = Parameters.InitialTemperature * 0.01;
            _temperature = Parameters.InitialTemperature;
            for (int i = 0;
                i < Parameters.IterationLimit
                && _temperature > minimalTemperature
                && State != QuikGraph.Algorithms.ComputationState.PendingAbortion;
                ++i)
            {
                IterateOne();

                // Make some cooling
                switch (Parameters.CoolingFunction)
                {
                    case FRCoolingFunction.Linear:
                        _temperature *= 1.0 - i / (double)Parameters.IterationLimit;
                        break;
                    case FRCoolingFunction.Exponential:
                        _temperature *= Parameters.Lambda;
                        break;
                }

                // Iteration ended, do some report
                if (ReportOnIterationEndNeeded)
                {
                    double statusInPercent = i / (double)Parameters.IterationLimit;
                    OnIterationEnded(i, statusInPercent, string.Empty, true);
                }
            }
        }

        #endregion

        /// <summary>
        /// First force application iteration.
        /// </summary>
        protected void IterateOne()
        {
            // Create the forces (zero forces)
            var forces = new Dictionary<TVertex, Vector>();

            #region Repulsive forces

            var force = new Vector(0, 0);
            foreach (TVertex v in VisitedGraph.Vertices)
            {
                force.X = 0;
                force.Y = 0;
                
                Point posV = VerticesPositions[v];
                foreach (TVertex u in VisitedGraph.Vertices)
                {
                    // Doesn't repulse itself
                    if (u.Equals(v))
                        continue;

                    // Calculate repulsive force
                    Vector delta = posV - VerticesPositions[u];
                    double length = Math.Max(delta.Length, double.Epsilon);
                    delta = delta / length * Parameters.ConstantOfRepulsion / length;

                    force += delta;
                }
                
                forces[v] = force;
            }

            #endregion

            #region Attractive forces

            foreach (TEdge edge in VisitedGraph.Edges)
            {
                TVertex source = edge.Source;
                TVertex target = edge.Target;

                // Compute attraction point between 2 vertices
                Vector delta = VerticesPositions[source] - VerticesPositions[target];
                double length = Math.Max(delta.Length, double.Epsilon);
                delta = delta / length * Math.Pow(length, 2) / Parameters.ConstantOfAttraction;

                forces[source] -= delta;
                forces[target] += delta;
            }

            #endregion

            #region Limit displacement

            foreach (TVertex vertex in VisitedGraph.Vertices)
            {
                Point position = VerticesPositions[vertex];

                Vector delta = forces[vertex];
                double length = Math.Max(delta.Length, double.Epsilon);
                delta = delta / length * Math.Min(delta.Length, _temperature);

                position += delta;

                // Ensure bounds
                position.X = Math.Min(_maxWidth, Math.Max(0, position.X));
                position.Y = Math.Min(_maxHeight, Math.Max(0, position.Y));
                VerticesPositions[vertex] = position;
            }

            #endregion
        }
    }
}