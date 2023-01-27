﻿using Autodesk.Revit.DB;
using DS.RevitLib.Utils.MEP;
using DS.RevitLib.Utils.Transactions;
using DS.RevitLib.Utils.Visualisators;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static System.Windows.Forms.LinkLabel;

namespace DS.RevitLib.Utils.Extensions
{
    public static class ElementExtension
    {
        /// <summary>
        /// Check if object is null or valid
        /// </summary>
        /// <param name="element"></param>
        /// <returns>Return false if object is null or not valid. Return true if object isn't null and valid</returns>
        public static bool NotNullValidObject(this Element element)
        {
            if (element is null || !element.IsValidObject)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Get ElementType object.
        /// </summary>
        /// <param name="element"></param>
        /// <returns>Return ElementType object.</returns>
        public static ElementType GetElementType2(this Element element)
        {
            ElementId id = element.GetTypeId();
            return element.Document.GetElement(id) as ElementType;
        }

        /// <summary>
        /// Order elements list by base point.
        /// </summary>
        /// <param name="basePoint"></param>
        /// <returns>Return ordered elements by descending distances from location points to base point.</returns>
        public static List<Element> OrderByPoint(this List<Element> elements, XYZ basePoint)
        {
            var distances = new Dictionary<double, Element>();

            foreach (var elem in elements)
            {
                XYZ point = ElementUtils.GetLocationPoint(elem);
                double distance = basePoint.DistanceTo(point);
                distances.Add(distance, elem);
            }

            distances = distances.OrderByDescending(x => x.Key).ToDictionary(x => x.Key, x => x.Value);

            return distances.Values.ToList();
        }

        /// <summary>
        /// Order elements list.
        /// </summary>
        /// <param name="basePoint"></param>
        /// <returns>Return ordered elements by its locations.</returns>
        public static List<Element> Order(this List<Element> elements)
        {
            //get location points of elements
            List<XYZ> pointsList = new List<XYZ>();

            foreach (var elem in elements)
            {
                var lp = ElementUtils.GetLocationPoint(elem);
                pointsList.Add(lp);
            }

            //find edge location points
            var (point1, point2) = XYZUtils.GetMaxDistancePoints(pointsList, out double maxDist);

            var distances = new Dictionary<double, Element>();

            foreach (var elem in elements)
            {
                XYZ point = ElementUtils.GetLocationPoint(elem);
                double distance = point1.DistanceTo(point);
                distances.Add(distance, elem);
            }

            distances = distances.OrderByDescending(x => x.Key).ToDictionary(x => x.Key, x => x.Value);

            return distances.Values.ToList();
        }

        /// <summary>
        /// Get elements without spuds
        /// </summary>
        /// <param name="elements"></param>
        /// <returns>Return list of elements without spuds.</returns>
        public static List<Element> ExludeSpudes(this List<Element> elements)
        {
            var roots = new List<Element>();

            foreach (var elem in elements)
            {
                if (!elem.IsSpud())
                {
                    roots.Add(elem);
                }
            }

            return roots.Any() ? roots : elements;
        }

        /// <summary>
        /// Check if element is spud or tap.
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public static bool IsSpud(this Element element)
        {
            if (element is not FamilyInstance familyInstance)
            {
                return false;
            }

            var pt = ElementUtils.GetPartType(familyInstance);
            if (pt == PartType.SpudPerpendicular || pt == PartType.SpudAdjustable ||
                pt == PartType.TapPerpendicular || pt == PartType.TapAdjustable)
            {
                return true;
            }

            return false;
        }

        public static FamilySymbol GetFamilySymbol(this Element element)
        {
            ElementId id = element.GetTypeId();
            return element.Document.GetElement(id) as FamilySymbol;
        }

        /// <summary>
        /// Get center line of element.
        /// </summary>
        /// <param name="famInst"></param>
        /// <returns>Returns center line of MEPCurve or family instance created by it's main connecors. 
        /// Returns null if element is not a MEPCuve or FamilyInstance type.</returns>
        public static Line GetCenterLine(this Element element)
        {
            if (element is MEPCurve)
            {
                return MEPCurveUtils.GetLine(element as MEPCurve);
            }
            else if (element is FamilyInstance)
            {
                FamilyInstance familyInstance = element as FamilyInstance;
                var (famInstCon1, famInstCon2) = ConnectorUtils.GetMainConnectors(familyInstance);
                return Line.CreateBound(famInstCon1.Origin, famInstCon2.Origin);
            }

            return null;
        }

        /// <summary>
        /// Check if element is geomety element.
        /// </summary>
        /// <param name="element"></param>
        /// <returns>Returns true if element has volume of solid.</returns>
        public static bool IsGeometryElement(this Element element)
        {
            var g = element.get_Geometry(new Options()
            {
                ComputeReferences = false,
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = false
            })
                ?.Cast<GeometryObject>().ToList();

            return CheckGeometry(g);

            bool CheckGeometry(List<GeometryObject> g)
            {
                if (g is null) return false;

                foreach (var elem in g)
                {
                    if (elem is Solid s && s.Volume > 1e-6)
                        return true;
                    else if (elem is GeometryInstance gi)
                    {
                        var go = gi.GetInstanceGeometry();
                        return CheckGeometry(go?.Cast<GeometryObject>().ToList());
                    }
                }
                return false;
            }

        }

        /// <summary>
        /// Get center point of element from center of it's solid. If element is elbow returns it's location point.
        /// </summary>
        /// <param name="element"></param>
        /// <returns>Returns element center point.</returns>
        public static XYZ GetCenterPoint(this Element element)
        {
            if (!element.IsGeometryElement())
            {
                return null;
            }

            //Check elbow type
            if (element is FamilyInstance familyInstance)
            {
                var partType = ElementUtils.GetPartType(familyInstance);
                if (partType == PartType.Elbow)
                {
                    var positionPoint = element.Location as LocationPoint;
                    return positionPoint.Point;
                }
            }


            Solid solid = ElementUtils.GetSolid(element);
            return solid.ComputeCentroid();
        }

        /// <summary>
        /// Get center point of any types of elements
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public static XYZ GetLocationPoint(this Element element)
        {
            // Get the Location property and judge whether it exists
            Location position = element.Location;

            // If the location is a point location, give the user information
            LocationPoint positionPoint = position as LocationPoint;
            if (null != positionPoint)
            {
                return positionPoint.Point;
            }
            else
            {
                // If the location is a curve location, give the user information
                LocationCurve positionCurve = position as LocationCurve;
                if (null != positionCurve)
                {
                    XYZ startPoint = positionCurve.Curve.GetEndPoint(0);
                    XYZ endPoint = positionCurve.Curve.GetEndPoint(1);

                    XYZ centerPoint = new XYZ((startPoint.X + endPoint.X) / 2,
                        (startPoint.Y + endPoint.Y) / 2,
                        (startPoint.Z + endPoint.Z) / 2);
                    return centerPoint;
                }
            }

            return null;
        }

        /// <summary>
        /// Show all edges of elements solid.
        /// </summary>
        /// <param name="element"></param>
        /// <remarks>Transaction is not provided, so methods should be wrapped to transacion.</remarks>
        public static void ShowEdges(this Element element)
        {
            Document doc = element.Document;
            var solids = ElementUtils.GetSolids(element);
            foreach (Solid s in solids)
            {
                s.ShowEdges(doc);
            }
        }

        /// <summary>
        /// Show <see cref="BoundingBoxXYZ"/> of <paramref name="element"/> in model.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="transactionBuilder"></param>
        public static void ShowBoundingBox(this Element element, AbstractTransactionBuilder transactionBuilder = null)
        {
            var doc = element.Document;
            transactionBuilder ??= new TransactionBuilder(doc);

            BoundingBoxXYZ boundingBox = element.get_BoundingBox(null);
            transactionBuilder.Build(() =>
            {
                var visualizator = new BoundingBoxVisualisator(boundingBox, doc);
                visualizator.Visualise();
            }, "show BoundingBox");
        }

        /// <summary>
        /// Show <see cref="BoundingBoxXYZ"/> of link <paramref name="element"/> in model.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="revitLink"></param>
        /// <param name="linkDoc">Link document containing <paramref name="element"/>.</param>
        /// <param name="transactionBuilder"></param>
        public static void ShowBoundingBox(this Element element, RevitLinkInstance revitLink,
            AbstractTransactionBuilder transactionBuilder = null)
        {
            var doc = revitLink.Document;
            transactionBuilder ??= new TransactionBuilder(doc);

            Transform transform = revitLink.GetTotalTransform();

            BoundingBoxXYZ boundingBox = element.get_BoundingBox(null);
            var transformBoundingBox = new BoundingBoxXYZ()
            {
                Min = transform.OfPoint(boundingBox.Min),
                Max = transform.OfPoint(boundingBox.Max)
            };
            transactionBuilder.Build(() =>
            {
                var visualizator = new BoundingBoxVisualisator(transformBoundingBox, doc);
                visualizator.Visualise();
            }, "show BoundingBox");
        }

        /// <summary>
        /// Check if current <paramref name="element"/> is conform given <paramref name="categoriesDict"/>.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="categoriesDict"></param>
        /// <returns>Returns true if it is conform both <paramref name="categoriesDict"/> key and value or 
        /// only key if <see cref="Autodesk.Revit.DB.PartType"/> value is undefined. 
        /// <para>Returns true if <paramref name="categoriesDict"/> is null or empty.</para>
        /// <para>Returns false if element is not <see cref="Autodesk.Revit.DB.FamilyInstance"/>.</para>
        /// </returns>
        public static bool IsCategoryElement(this Element element, Dictionary<BuiltInCategory, List<PartType>> categoriesDict)
        {
            if (categoriesDict is null || !categoriesDict.Any()) { return true; }
            if (element is not FamilyInstance) { return false; }

            var builtCat = CategoryExtension.GetBuiltInCategory(element.Category);
            var partType = ElementUtils.GetPartType(element as FamilyInstance);

            var dictBuilCat = categoriesDict.Keys.FirstOrDefault(c => (int)c == (int)builtCat);
            if (dictBuilCat is not 0)
            {
                categoriesDict.TryGetValue(dictBuilCat, out var dictPartTypes);
                foreach (var item in dictPartTypes)
                {
                    if (item == PartType.Undefined || partType == item)
                    {
                        return true;
                    }
                }
            }


            return false;
        }

        /// <summary>
        /// Connect <paramref name="elements"/> by common connectors.
        /// </summary>
        /// <param name="elements"></param>
        public static void Connect(this List<Element> elements)
        {
            for (int i = 0; i < elements.Count - 1; i++)
            {
                (Connector con1, Connector con2) = ConnectorUtils.GetCommonNotConnectedConnectors(elements[i], elements[i + 1]);
                if (con1 is not null && con2 is not null && !con1.IsConnectedTo(con2)) { con1.ConnectTo(con2); };
            }
        }
    }
}
