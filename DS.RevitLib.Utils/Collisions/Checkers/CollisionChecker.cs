﻿using Autodesk.Revit.DB;
using DS.ClassLib.VarUtils.Collisions;
using System.Collections.Generic;

namespace DS.RevitLib.Utils.Collisions.Checkers
{
    public abstract class CollisionChecker<T, P> : ICollisionChecker
    {
        private readonly List<P> _checkedObjects2;
        private readonly List<P> _exludedObjects;
        private List<T> _checkedObjects1;

        protected CollisionChecker(List<P> checkedObjects2, List<P> exludedObjects = null)
        {
            _checkedObjects2 = checkedObjects2;
            _exludedObjects = exludedObjects;
        }

        protected CollisionChecker(List<T> checkedObjects1, List<P> checkedObjects2, List<P> exludedObjects = null)
        {
            _checkedObjects1 = checkedObjects1;
            _checkedObjects2 = checkedObjects2;
            _exludedObjects = exludedObjects;
        }

        protected abstract Document Document { get; }

        protected abstract FilteredElementCollector Collector { get; set; }
        protected abstract ExclusionFilter ExclusionFilter { get; }

        public List<P> CheckedObjects2 => _checkedObjects2;

        public List<P> ExludedObjects => _exludedObjects;

        public List<T> CheckedObjects1 { get => _checkedObjects1; protected set => _checkedObjects1 = value; }

        //public List<ICollision> Collisions { get; protected set; }

        public abstract List<ICollision> GetCollisions();
        public abstract List<ICollision> GetCollisions(List<T> checkedObjects1);

        protected abstract ICollision BuildCollision(T object1, P object2);
    }
}
