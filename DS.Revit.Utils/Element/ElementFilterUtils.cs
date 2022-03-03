﻿using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DS.Revit.Utils
{
    public static class ElementFilterUtils
    {
        public static ExclusionFilter GetExclustionFilter(List<Element> elements)
        {
            if (elements == null || elements.Count ==0)
                return null;
            ICollection<ElementId> elementIds = elements.Select(el => el.Id).ToList();
            return new ExclusionFilter(elementIds);
        }
    }
}