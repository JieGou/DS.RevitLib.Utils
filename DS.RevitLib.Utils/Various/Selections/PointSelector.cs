﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DS.RevitLib.Utils.SelectionFilters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DS.RevitLib.Utils.Various.Selections
{
    /// <inheritdoc/>
    public class PointSelector : SelectorBase<Element>
    {
        /// <inheritdoc/>
        public PointSelector(UIDocument uiDoc) : base(uiDoc)
        {
        }

        /// <inheritdoc/>
        public XYZ Point { get; private set; }

        /// <inheritdoc/>
        protected override ISelectionFilter Filter => new PointSelectionFilter<Element>() { AllowLink = AllowLink};

        /// <inheritdoc/>
        protected override ISelectionFilter FilterInLink => new ElementInLinkSelectionFilter<Element>(_doc);

        /// <inheritdoc/>
        public override Element Pick(string statusPrompt = null, string promptSuffix = null)
        {
            Reference reference = _uiDoc.Selection.
                PickObject(ObjectType.PointOnElement, Filter, GetStatusPrompt(statusPrompt, promptSuffix));
            Point = reference.GlobalPoint;
            var element = _doc.GetElement(reference);
            return element is RevitLinkInstance ?
                PickInLink(element as RevitLinkInstance) :
                element;
        }
    }
}
