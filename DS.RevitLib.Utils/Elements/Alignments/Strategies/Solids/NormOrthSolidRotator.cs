﻿using Autodesk.Revit.DB;
using DS.ClassLib.VarUtils;
using DS.RevitLib.Utils.Extensions;
using DS.RevitLib.Utils.GPExtractor;
using DS.RevitLib.Utils.Solids.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DS.RevitLib.Utils.Elements.Alignments.Strategies
{
    internal class NormOrthSolidRotator : AlignmentRotator<SolidModelExt>
    {
        public NormOrthSolidRotator(SolidModelExt operationElement, Element targetElement) : 
            base(operationElement, targetElement)
        {
        }

        protected override XYZ GetOperationBaseVector()
        {
            List<XYZ> normOrths = ElementUtils.GetOrthoNormVectors(_operationElement.Element);
            return ElementUtils.GetMaxSizeOrth(_operationElement.Element, normOrths);
        }

        protected override double GetRotationAngle(XYZ targetBaseVector, XYZ operationBaseVector)
        {
            double angleRad = targetBaseVector.AngleTo(operationBaseVector);
            double angleDeg = angleRad.RadToDeg();
            int rotDir = GetRotationSide(_targetBaseVector, _operationBaseVector, _rotationAxis.Direction);

            return angleRad * rotDir;
        }

        protected override Line GetRotationAxis()
        {
            return _operationLine;
        }

        protected override XYZ GetTargetBaseVector()
        {
            List<XYZ> normOrths = ElementUtils.GetOrthoNormVectors(_targetElement);
            return ElementUtils.GetMaxSizeOrth(_targetElement, normOrths);
        }

        public override SolidModelExt Rotate()
        {
            if (_rotationAngle == 0)
            {
                return _operationElement;
            }

            Transform rotateTransform = Transform.CreateRotationAtPoint(_rotationAxis.Direction, _rotationAngle, _operationElement.Center);
            _operationElement.Transform(rotateTransform);

            return _operationElement;
        }

        private int GetRotationSide(XYZ alignAxe, XYZ vectorToRotateNorm, XYZ rotationAxe)
        {
            if (XYZUtils.BasisEqualToOrigin(alignAxe, vectorToRotateNorm, rotationAxe))
            {
                return -1;
            }

            return 1;
        }

        protected override Line GetOperationLine(SolidModelExt operationElement)
        {
            return operationElement.Line;
        }
    }
}
