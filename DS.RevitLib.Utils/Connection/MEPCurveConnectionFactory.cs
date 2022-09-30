﻿using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using DS.RevitLib.Utils.Connection.Strategies;
using DS.RevitLib.Utils.Extensions;
using DS.RevitLib.Utils.MEP;
using DS.RevitLib.Utils.MEP.Creator;
using DS.RevitLib.Utils.MEP.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DS.RevitLib.Utils.Connection
{
    public class MEPCurveConnectionFactory : IConnectionFactory
    {
        private readonly Document _doc;
        private readonly MEPCurveGeometryModel _mEPCurveModel1;
        private readonly MEPCurveGeometryModel _mEPCurveModel2;
        private readonly MEPCurveGeometryModel _mEPCurveModel3;
        private readonly double _minLength;

        /// <summary>
        /// Initiate factory object to connect two MEPCurves
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="mEPCurve1"></param>
        /// <param name="mEPCurve2"></param>
        /// <param name="mEPCurve3">Parent element. Optional parameter.</param>
        /// <param name="minLength">Minimum length between two MEPCurve's connectors.</param>
        public MEPCurveConnectionFactory(Document doc, MEPCurve mEPCurve1, MEPCurve mEPCurve2, MEPCurve mEPCurve3 = null, double minLength = 0)
        {
            _doc = doc;
            _mEPCurveModel1 = new MEPCurveGeometryModel(mEPCurve1);
            _mEPCurveModel2 = new MEPCurveGeometryModel(mEPCurve2);
            _mEPCurveModel3 = mEPCurve3 is null ? null :  new MEPCurveGeometryModel(mEPCurve3);
            _minLength = minLength;
        }

        /// <inheritdoc/>
        public bool Connect()
        {
            var strategy = GetStrategy();
            return strategy is not null && strategy.Connect();
        }

        private MEPCurveConnectionStrategy GetStrategy()
        {
            var (elem1Con, elem2Con) = ConnectorUtils.GetNeighbourConnectors(_mEPCurveModel1.MainConnectors, _mEPCurveModel2.MainConnectors);
            if (elem1Con is not null && elem2Con is not null && XYZUtils.Collinearity(_mEPCurveModel1.Direction, _mEPCurveModel2.Direction))
            {
                return new ConnectorMEPCurveStrategy(_doc, _mEPCurveModel1, _mEPCurveModel2, _minLength, elem1Con, elem2Con);
            }

            Line parentLine = Line.CreateUnbound(_mEPCurveModel2.Line.Origin, _mEPCurveModel2.Direction);
            Connector curve1Con = ConnectorUtils.GetClosest(parentLine, _mEPCurveModel1.MainConnectors);

            var projPoint = _mEPCurveModel2.Line.Project(curve1Con.Origin).XYZPoint;
            if (_mEPCurveModel3 is null)
            {
                if ((projPoint - _mEPCurveModel2.Line.GetEndPoint(0)).IsZeroLength() || 
                    (projPoint - _mEPCurveModel2.Line.GetEndPoint(1)).IsZeroLength())
                {
                    (elem1Con, elem2Con) = ConnectorUtils.GetClosest(_mEPCurveModel1.MainConnectors, _mEPCurveModel2.MainConnectors);
                    return new ElbowMEPCurveStrategy(_doc, _mEPCurveModel1, _mEPCurveModel2, _minLength, elem1Con, elem2Con);
                }
            }

            //try get tee or spud stratagies

            if (curve1Con is null || 
                !XYZUtils.Perpendicular(_mEPCurveModel1.Direction, _mEPCurveModel2.Direction) ||
                _mEPCurveModel3 is not null && !XYZUtils.Collinearity(_mEPCurveModel2.Direction, _mEPCurveModel3.Direction))
            {
                return null;
            }

            var routePrefManager = _mEPCurveModel1.MEPCurveType.RoutingPreferenceManager;          
            switch (routePrefManager.PreferredJunctionType)
            {
                case PreferredJunctionType.Tee:
                    {
                        return _mEPCurveModel3 is null ?
                            new TeeWithCutMEPCurveStrategy(_doc, _mEPCurveModel1, _mEPCurveModel2, _minLength, curve1Con) :
                            new TeeMEPCurveStrategy(_doc, _mEPCurveModel1, _mEPCurveModel2, _mEPCurveModel3, curve1Con, _minLength);
                    }
                case PreferredJunctionType.Tap:
                    {
                        return new SpudMEPCurveStrategy(_doc, _mEPCurveModel1, _mEPCurveModel2, _minLength, curve1Con);
                    }
                default:
                    break;
            }

            return null;

        }

        private MEPCurveConnectionStrategy OldGetStrategy()
        {
            var (elem1Con, elem2Con) = ConnectorUtils.GetNeighbourConnectors(_mEPCurveModel1.MainConnectors, _mEPCurveModel2.MainConnectors);
            if (elem1Con is not null && elem2Con is not null)
            {
                return XYZUtils.Collinearity(_mEPCurveModel1.Direction, _mEPCurveModel2.Direction) ?
                    new ConnectorMEPCurveStrategy(_doc, _mEPCurveModel1, _mEPCurveModel2, _minLength, elem1Con, elem2Con) :
                    new ElbowMEPCurveStrategy(_doc, _mEPCurveModel1, _mEPCurveModel2, _minLength, elem1Con, elem2Con);
            }

            //try get tee or spud stratagies
            XYZ mc2ConXYZ1 = _mEPCurveModel2.MainConnectors.First().Origin;
            XYZ mc2ConXYZ2 = _mEPCurveModel2.MainConnectors.Last().Origin;

            XYZ pointOnCurve = _mEPCurveModel1.MainConnectors.First().Origin.
                IsBetweenPoints(mc2ConXYZ1, mc2ConXYZ2) ? _mEPCurveModel1.MainConnectors.First().Origin : null;
            pointOnCurve ??= _mEPCurveModel1.MainConnectors.Last().Origin.
                    IsBetweenPoints(mc2ConXYZ1, mc2ConXYZ2) ? _mEPCurveModel1.MainConnectors.Last().Origin : null;

            //need only perpendicular MEPCurves
            if (pointOnCurve is null || !XYZUtils.Perpendicular(_mEPCurveModel1.Direction, _mEPCurveModel2.Direction))
            {
                return null;
            }

            var routePrefManager = _mEPCurveModel1.MEPCurveType.RoutingPreferenceManager;
            Connector curve1Con = _mEPCurveModel1.MainConnectors.
                            Where(obj => (obj.Origin - pointOnCurve).IsZeroLength()).First();
            switch (routePrefManager.PreferredJunctionType)
            {
                case PreferredJunctionType.Tee:
                    {
                        return _mEPCurveModel3 is null ?
                            new TeeWithCutMEPCurveStrategy(_doc, _mEPCurveModel1, _mEPCurveModel2, _minLength, curve1Con) :
                            new TeeMEPCurveStrategy(_doc, _mEPCurveModel1, _mEPCurveModel2, _mEPCurveModel3, curve1Con, _minLength);
                    }
                case PreferredJunctionType.Tap:
                    {
                        return new SpudMEPCurveStrategy(_doc, _mEPCurveModel1, _mEPCurveModel2, _minLength, curve1Con);
                    }
                default:
                    break;
            }

            return null;

        }

    }
}
