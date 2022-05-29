﻿using Autodesk.Revit.DB;
using DS.MainUtils;
using DS.RevitLib.Utils.Extensions;
using Ivanov.RevitLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DS.RevitLib.Utils.MEP.Creator
{
    public class BuilderByPoints : MEPSystemBuilder
    {
        public BuilderByPoints(MEPCurve baseMEPCurve, List<XYZ> points) : base(baseMEPCurve)
        {
            this._Points = points;
        }

        private List<XYZ> _Points = new List<XYZ>();

        public override MEPCurvesModel BuildMEPCurves()
        {
            MEPCurveCreator mEPCurveCreator = new MEPCurveCreator(Doc, BaseMEPCurve);
            MEPCurve baseMEPCurve = null;
            for (int i = 0; i < _Points.Count - 1; i++)
            {
                XYZ p1 = _Points[i];
                XYZ p2 = _Points[i + 1];

                MEPCurve mEPCurve = mEPCurveCreator.CreateMEPCurveByPoints(p1, p2, baseMEPCurve);

                if (CheckRotate(baseMEPCurve, mEPCurve))
                {
                    Rotate(baseMEPCurve, mEPCurve);
                }

                if (CheckSwap(baseMEPCurve, mEPCurve))
                {
                    MEPCurveUtils.SwapSize(mEPCurve);
                }

                baseMEPCurve = mEPCurve;

                MEPSystemModel.AllElements.Add(mEPCurve);
                MEPSystemModel.MEPCurves.Add(mEPCurve);
            }

            return new MEPCurvesModel(MEPSystemModel, Doc);
        }

        /// <summary>
        /// Check if size of MEPCurve should be swapped.
        /// </summary>
        /// <param name="baseMEPCurve"></param>
        /// <param name="mEPCurve"></param>
        /// <returns>Return true if size of MEPCurve should be swapped.</returns>
        private static bool CheckSwap(MEPCurve baseMEPCurve, MEPCurve mEPCurve)
        {
            if (baseMEPCurve is not null)
            {
                if (baseMEPCurve.IsRecangular() &&
                    !MEPCurveUtils.IsDirectionEqual(baseMEPCurve, mEPCurve) &&
                    !MEPCurveUtils.IsEqualSize(baseMEPCurve, mEPCurve))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CheckRotate(MEPCurve baseMEPCurve, MEPCurve mEPCurve)
        {
            if (baseMEPCurve is not null)
            {
                if (baseMEPCurve.IsRecangular())
                {
                    return true;
                }
            }

            return false;
        }

        private void Rotate(MEPCurve baseMEPCurve, MEPCurve mEPCurve)
        {
            XYZ rotCenter = ElementUtils.GetLocationPoint(mEPCurve);

            //base axe for angle count
            XYZ xAxe = MEPCurveUtils.GetDirection(baseMEPCurve);

            //Rotation axe
            XYZ zAxe = MEPCurveUtils.GetDirection(mEPCurve);

            //positive rotation axe (counterclockwise)
            XYZ yAxe = zAxe.CrossProduct(xAxe);

            List<XYZ> normOrthoVectors = MEPCurveUtils.GetOrthoNormVectors(mEPCurve);
            List<double> angles = GetAngles(xAxe, normOrthoVectors);

            XYZ vectorToRotateNorm = normOrthoVectors.First();
          

            //Plane plane = null;
            //if (xAxe.IsAlmostEqualTo(zAxe) || xAxe.Negate().IsAlmostEqualTo(zAxe))
            //{
            //    plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, ElementUtils.GetLocationPoint(mEPCurve));
            //}
            //else
            //{
            //    plane = MEPCurveUtils.GetPlane(mEPCurve, baseMEPCurve);
            //}


            double angleRad = vectorToRotateNorm.AngleTo(xAxe);
            double angleDeg = angleRad.RadToDeg();
            //double angleDeg;

            //if (angleDeg <= 90)
            //{
            //    rotAngle = 90 - angleDeg;
            //}
            //else
            //{
            //    rotAngle = angleDeg - 90;
            //}

            double prec = 3.0;
            if (Math.Abs(angleDeg - 0) < prec || Math.Abs(angleDeg - 180) < prec ||
                Math.Abs(angleDeg - 90) < prec || Math.Abs(angleDeg - 270) < prec)
            {
                return;
            }
            int rotDir;
            //rotDir = GetRotateDirection(xAxe, yAxe, vectorToRotateNorm, rotCenter);
            if (XYZUtils.Is3DOrientationEqualToOrigin(xAxe, vectorToRotateNorm, zAxe))
            {
                rotDir = -1;
            }
            else
            {
                rotDir = 1;
            }
            mEPCurve = RotateMEPCurve(mEPCurve, angleRad * rotDir); 
        }

        private MEPCurve RotateMEPCurve(MEPCurve mEPCurve, double angleRad)
        {

            using (Transaction transNew = new Transaction(Doc, "CreateMEPCurveByPoints"))
            {
                try
                {
                    transNew.Start();

                    var locCurve = mEPCurve.Location as LocationCurve;
                    var line = locCurve.Curve as Line;

                    mEPCurve.Location.Rotate(line, angleRad);
                }
                catch (Exception e)

                { }
                if (transNew.HasStarted())
                {
                    transNew.Commit();
                }
            }

            return mEPCurve;
        }

        private List<double> GetAngles(XYZ dir, List<XYZ> normOrthoVectors)
        {
            List<double> angles = new List<double>();
            foreach (var v in normOrthoVectors)
            {
                double angleRad = dir.AngleTo(v);
                double angleDeg = angleRad.RadToDeg();
                angles.Add(angleDeg);
            }

            return angles;
        }

        private bool IsVectorsEqual(List<XYZ> normOrthoVectors, XYZ baseVector)
        {
            foreach (var vector in normOrthoVectors)
            {
                if (baseVector.IsAlmostEqualTo(vector) || baseVector.Negate().IsAlmostEqualTo(vector))
                {
                    return true;
                }
            }

            return false;
        }

        private int GetRotateDirection(XYZ xAxe, XYZ yAxe, XYZ vectorToRotateNorm, XYZ rotCenter)
        {
          XYZ A = rotCenter + vectorToRotateNorm;

          Line xLine = Line.CreateBound(rotCenter, rotCenter + xAxe).IncreaseLength(100);
          Line yLine = Line.CreateBound(rotCenter, rotCenter + yAxe).IncreaseLength(100);

            XYZ Ax = xLine.Project(A).XYZPoint;
            XYZ Ay = yLine.Project(A).XYZPoint;

            XYZ AxVector = Ax - rotCenter;
            XYZ AyVector = Ay - rotCenter;

            int resDirx = CheckDirection(AxVector, xAxe);
            int resDiry = CheckDirection(AyVector, yAxe);

            return resDirx * resDiry;           
        }

        private int CheckDirection(XYZ AxVector, XYZ xAxe)
        {
            double angleRad = AxVector.AngleTo(xAxe);
            double angleDeg = angleRad.RadToDeg();
           
            if (Math.Abs(angleDeg) > 3)
            {
                return -1;
            }

            return 1;
        }
    }
}
