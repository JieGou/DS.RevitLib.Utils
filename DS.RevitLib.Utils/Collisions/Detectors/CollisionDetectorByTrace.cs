﻿using Autodesk.Revit.DB;
using DS.ClassLib.VarUtils;
using DS.ClassLib.VarUtils.Basis;
using DS.ClassLib.VarUtils.Collisions;
using DS.ClassLib.VarUtils.Points;
using DS.RevitLib.Utils.Connections.PointModels;
using DS.RevitLib.Utils.Creation.Transactions;
using DS.RevitLib.Utils.Elements.MEPElements;
using DS.RevitLib.Utils.Extensions;
using DS.RevitLib.Utils.MEP;
using DS.RevitLib.Utils.Models;
using DS.RevitLib.Utils.Solids;
using DS.RevitLib.Utils.Various.Bases;
using MoreLinq;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace DS.RevitLib.Utils.Collisions.Detectors
{
    /// <summary>
    /// An object that detect collisions (intersections) between objects in Revit model by trace and <see cref="MEPCurve"/>.
    /// </summary>
    public class CollisionDetectorByTrace : ITraceCollisionDetector<Point3d>
    {
        private static readonly string _wallIntersectionParamName = "OLP_БезПересечений";
        private readonly Document _doc;
        private readonly MEPCurve _baseMEPCurve;
        private readonly ITraceSettings _traceSettings;
        private readonly IPoint3dConverter _pointConverter;
        private readonly ITransactionFactory _transactionFactory;
        private readonly SolidElementCollisionDetectorFactory _detectorFactory;
        private readonly double _offset;
        private BasisXYZ _sourceBasis;
        private Point3d _startPoint;
        private Point3d _endPoint;
        private ConnectionPoint _startConnectionPoint;
        private ConnectionPoint _endConnectionPoint;
        private readonly double _aTolerance = 3.DegToRad();


        /// <summary>
        /// Instantiate an object to create objects for collisions (intersections) detection 
        /// between objects in Revit model by trace and <paramref name="baseMEPCurve"/>.
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="baseMEPCurve"></param>
        /// <param name="traceSettings"></param>
        /// <param name="insulationAccount"></param>
        /// <param name="docElements"></param>
        /// <param name="linkElementsDict"></param>
        /// <param name="pointConverter"></param>
        /// <param name="transactionFactory"></param>
        public CollisionDetectorByTrace(Document doc,
                                        MEPCurve baseMEPCurve,
                                        ITraceSettings traceSettings,
                                        bool insulationAccount,
                                        List<Element> docElements,
                                        Dictionary<RevitLinkInstance, List<Element>> linkElementsDict = null,
                                        IPoint3dConverter pointConverter = null,
                                        ITransactionFactory transactionFactory = null)
        {
            _doc = doc;
            _baseMEPCurve = baseMEPCurve;
            var insulation = insulationAccount ? _baseMEPCurve.GetInsulationThickness() : 0;
            _offset = insulation + traceSettings.B - 0.03;
            _traceSettings = traceSettings;
            _pointConverter = pointConverter;
            _transactionFactory = transactionFactory;
            _transactionFactory ??= new ContextTransactionFactory(_doc);
            _detectorFactory = new SolidElementCollisionDetectorFactory(doc, docElements, linkElementsDict)
            {
                MinVolume = 0
            };
            SolidExtractor = new BestSolidOffsetExtractor(baseMEPCurve, _offset);
        }

        /// <inheritdoc/>
        public List<(object, object)> Collisions { get; private set; } = new List<(object, object)>();

        /// <summary>
        /// Check objects 2 to exclude from collisions detection. 
        /// </summary>
        public List<Element> ObjectsToExclude { get; set; }

        /// <summary>
        /// Specify if check offset <see cref="Solid"/> will be created up to the end point + <see cref="Solid"/> offset.
        /// </summary>
        public bool OffsetOnEndPoint { get; set; } = false;

        public BestSolidOffsetExtractor SolidExtractor { get; }

        public ConnectionPoint StartConnectionPoint
        {
            get => _startConnectionPoint;
            set
            {
                _startConnectionPoint = value;
                _startPoint = _pointConverter.ConvertToUCS2(value.Point.ToPoint3d());
            }
        }

        public ConnectionPoint EndConnectionPoint
        {
            get => _endConnectionPoint;
            set
            {
                _endConnectionPoint = value;
                _endPoint = _pointConverter.ConvertToUCS2(value.Point.ToPoint3d());
            }
        }

        public int Punishment { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="point1"></param>
        /// <param name="point2"></param>
        /// <param name="basis"></param>
        /// <returns>
        /// Returns collisions by <see cref="MEPCurve"/> between <paramref name="point1"/> and <paramref name="point2"/>.
        /// <para>
        /// Returns empty list if no collisions were detected.
        /// </para>
        /// </returns>
        public List<(object, object)> GetCollisions(Point3d point1, Point3d point2, Basis3d basis)
        {
            Punishment = 0;

            XYZ p1 = null;
            XYZ p2 = null;
            var direction = point2 - point1;
            direction = Vector3d.Divide(direction, direction.Length);

            var endSolidPoint = point2;
            if (OffsetOnEndPoint)
            {
                var size = _baseMEPCurve.GetMaxSize();
                var mult = size + _offset;
                endSolidPoint += Vector3d.Multiply(direction, mult);
            }

            if (_pointConverter is not null)
            {
                Point3d point1UCS1 = _pointConverter.ConvertToUCS1(point1);
                Point3d point2UCS1 = _pointConverter.ConvertToUCS1(endSolidPoint);
                p1 = new XYZ(point1UCS1.X, point1UCS1.Y, point1UCS1.Z);
                p2 = new XYZ(point2UCS1.X, point2UCS1.Y, point2UCS1.Z);
            }
            else
            {
                p1 = new XYZ(point1.X, point1.Y, point1.Z);
                p2 = new XYZ(endSolidPoint.X, endSolidPoint.Y, endSolidPoint.Z);
            }

            var uCS1Basis = _pointConverter.ConvertToUCS1(basis).ToXYZ();
            var checkSolid = SolidExtractor.Extract(p1, p2, uCS1Basis);

            var collisions = _detectorFactory.GetCollisions(checkSolid, ObjectsToExclude);
            var excludeWallsIds = GetExcludeWalls(collisions, direction);

            collisions = collisions.Where(c => !excludeWallsIds.Contains(c.Item2.Id)).ToList();

            return Collisions = collisions.
                Select(x => ((object)x.Item1, (object)x.Item2)).ToList();

            _transactionFactory.CreateAsync(() =>
            {
                checkSolid.ShowShape(_doc);
            }
            , "Show shape");

            return Collisions = _detectorFactory.GetCollisions(checkSolid, ObjectsToExclude).
                Select(x => ((object)x.Item1, (object)x.Item2)).ToList();
        }

        private List<ElementId> GetExcludeWalls(IEnumerable<(Solid, Element)> collisions, Vector3d dir)
        {
            var walls = new List<Wall>();
            List<ElementId> excludeWalls = new();

            var wallsCollisions = collisions.Where(c => c.Item2 is Wall);
            wallsCollisions.ForEach(wc => walls.Add(wc.Item2 as Wall));

            if (walls.Count > 0)
            {
                var uCS1dir = _pointConverter.ConvertToUCS1(dir);
                foreach (Wall w in walls)
                {
                    if (w.IsTraversable(uCS1dir))
                    { excludeWalls.Add(w.Id); Punishment++; }
                }
            }

            return excludeWalls;
        }

        public List<(object, object)> GetFirstCollisions(Point3d point2, Basis3d basis)
        {
            var point1 = _startPoint;

            if (_startConnectionPoint is null) { return GetCollisions(point1, point2, basis); }

            var connectedElements = ConnectorUtils.GetConnectedElements(_startConnectionPoint.Element);

            var cacheExcluded = new List<Element>();
            cacheExcluded.AddRange(ObjectsToExclude);

            ObjectsToExclude = ObjectsToExclude.Union(connectedElements).ToList();
            var objectToExcludeIds = ObjectsToExclude.Select(o => o.Id);

            foreach (var item in connectedElements)
            {
                InsulationLiningBase ins = null;
                try
                {
                    ins = InsulationLiningBase.GetInsulationIds(item.Document, item.Id).
                        Select(x => item.Document.GetElement(x) as InsulationLiningBase).FirstOrDefault();
                }
                catch (Exception)
                { }
                if (ins is not null && !objectToExcludeIds.Contains(ins.Id)) { ObjectsToExclude.Add(_doc.GetElement(ins.Id)); }
            }

            var collisions = GetCollisions(point1, point2, basis);
            ObjectsToExclude = cacheExcluded;

            return collisions;
        }

        public List<(object, object)> GetLastCollisions(Point3d point1, Basis3d basis)
        {
            var point2 = _endPoint;

            if (_endConnectionPoint is null) { return GetCollisions(point1, point2, basis); }

            var connectedElements = ConnectorUtils.GetConnectedElements(_endConnectionPoint.Element);

            var cacheExcluded = new List<Element>();
            cacheExcluded.AddRange(ObjectsToExclude);

            ObjectsToExclude = ObjectsToExclude.Union(connectedElements).ToList();
            var objectToExcludeIds = ObjectsToExclude.Select(o => o.Id);

            foreach (var item in connectedElements)
            {
                InsulationLiningBase ins = null;
                try
                {
                    ins = InsulationLiningBase.GetInsulationIds(item.Document, item.Id).
                        Select(x => item.Document.GetElement(x) as InsulationLiningBase).FirstOrDefault();
                }
                catch (Exception)
                { }
                if (ins is not null && !objectToExcludeIds.Contains(ins.Id)) { ObjectsToExclude.Add(_doc.GetElement(ins.Id)); }
            }

            var collisions = GetCollisions(point1, point2, basis);
            ObjectsToExclude = cacheExcluded;

            return collisions;
        }

        public List<(object, object)> GetCollisions(Point3d p1, Point3d p2, Basis3d basis,
    Point3d firstPoint, Point3d lastPoint, int tolerance = 3)
        {
            List<(object, object)> collisions;

            bool isFirstPoint = p1.Round(tolerance) == firstPoint.Round(tolerance);
            bool isLastPoint = p2.Round(tolerance) == lastPoint.Round(tolerance);

            if (isFirstPoint)
            { collisions = GetFirstCollisions(p2, basis); }
            else if (isLastPoint)
            { collisions = GetLastCollisions(p1, basis); }
            else
            { collisions = GetCollisions(p1, p2, basis); }

            return collisions;
        }
    }
}
