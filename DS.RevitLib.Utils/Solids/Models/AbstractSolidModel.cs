﻿using Autodesk.Revit.DB;

namespace DS.RevitLib.Utils.Solids.Models
{

    public class AbstractSolidModel
    {

        public AbstractSolidModel(Solid solid = null)
        {
            Solid = solid ?? null;
        }

        public Solid Solid { get; protected set; }
        public XYZ Center
        {
            get
            {
                return Solid.ComputeCentroid();
            }
        }

        public double GetSizeByVector(XYZ orth)
        {
            return DS.RevitLib.Utils.Solids.SolidUtils.GetSizeByVector(Solid, orth);
        }

    }
}
