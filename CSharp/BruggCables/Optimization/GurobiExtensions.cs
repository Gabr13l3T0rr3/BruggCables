using Gurobi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Optimization
{
    public static class GurobiExtensions
    {
        public static void Set(this GRBVar _var, double _value)
        {
            _var.Set(GRB.DoubleAttr.LB, _value);
            _var.Set(GRB.DoubleAttr.UB, _value);
            //_var.Set(GRB.DoubleAttr.Start, _value);
        }


        public static bool IsSetTo(this GRBVar _var, double _value)
        {
            return _var.Get(GRB.DoubleAttr.LB) == _value && _var.Get(GRB.DoubleAttr.UB) == _value;
        }

        public static IEnumerable<double> AsDouble(this GRBVar[] _var)
        {
            for (var x = 0; x < _var.Length; x++)
                yield return (double)_var[x].Get(GRB.DoubleAttr.X);
        }

        public static GRBLinExpr SumL1(this GRBVar[,] _vars)
        {
            var res = new GRBLinExpr();
            foreach (var v in _vars)
                if((object)v!=null)
                    res.Add(v);
            return res;
        }

        public static GRBLinExpr SumL1(this GRBVar[] _vars)
        {
            var res = new GRBLinExpr();
            foreach (var v in _vars)
                if((object)v!=null)
                    res.Add(v);
            return res;
        }

        public static GRBLinExpr SumL1(this GRBLinExpr[] _vars)
        {
            var res = new GRBLinExpr();
            foreach (var v in _vars)
                res.Add(v);
            return res;
        }

        public static GRBQuadExpr SumL2(this GRBVar[] _vars)
        {
            var res = new GRBQuadExpr();
            foreach (var v in _vars)
                res.Add(v*v);
            return res;
        }

        public static GRBLinExpr SumL1(this List<GRBLinExpr> _expressions)
        {
            var res = new GRBLinExpr();
            foreach (var v in _expressions)
                if ((object)v != null)
                    res.Add(v);
            return res;
        }

        public static void AddConstr(this GRBModel _m, GRBTempConstr _constr)
        {
            try
            {
                _m.AddConstr(_constr, null);
            }
            catch (GRBException _e) when(_e.ErrorCode==10003)
            {
                _m.AddQConstr(_constr, null);
            }
        }

        public static T Index<T>(this T[][] _m, int _index)
        {
            foreach (var line in _m)
                foreach (var v in line)
                {
                    if (_index == 0)
                        return v;
                    
                    _index--;
                }

            throw new IndexOutOfRangeException();
        }

        public static GRBVar AddVar(this GRBModel _m, double lb, double ub, char type, string _prefix = null)
        {
            return _m.AddVar(lb,ub,0,type, _prefix);
        }

        public static GRBVar[,] AddVars(this GRBModel _m, int _width, int _height, double lb, double ub, char type, string _prefix = null)
        {
            var vars = _m.AddVars(Enumerable.Repeat(lb, _width * _height).ToArray(), Enumerable.Repeat(ub, _width * _height).ToArray(), null, Enumerable.Repeat(type, _width * _height).ToArray(),
                    _prefix==null ? null : Enumerable.Range(0, _width * _height).Select(j => $"{_prefix}[{j % _width},{j/_width}]").ToArray()                
                );

            var i = 0;
            var res = new GRBVar[_width, _height];
            for (var y = 0; y < _height; y++)
                for (var x = 0; x < _width; x++)
                    res[x, y] = vars[i++];

            return res;
        }

        public static GRBVar[,,] AddVars(this GRBModel _m, int _width, int _height, int _depth, double lb, double ub, char type, string _prefix = null)
        {
            var vars = _m.AddVars(Enumerable.Repeat(lb, _width * _height * _depth).ToArray(), Enumerable.Repeat(ub, _width * _height * _depth).ToArray(), null, Enumerable.Repeat(type, _width * _height * _depth).ToArray(),
                    _prefix==null ? null : Enumerable.Range(0, _width * _height * _depth).Select(j => $"{_prefix}[{j % _width},{(j/_width) % _height},{j/_width/_height}]").ToArray()
                );

            var i = 0;
            var res = new GRBVar[_width, _height, _depth];
            for (var z = 0; z < _depth; z++)
                for (var y = 0; y < _height; y++)
                    for (var x = 0; x < _width; x++)
                        res[x, y, z] = vars[i++];

            return res;
        }

        public static GRBVar[,,,] AddVars(this GRBModel _m, int _width, int _height, int _depth, int _d4, double lb, double ub, char type, string _prefix = null)
        {
            var vars = _m.AddVars(Enumerable.Repeat(lb, _width * _height * _depth * _d4).ToArray(), Enumerable.Repeat(ub, _width * _height * _depth * _d4).ToArray(), null, Enumerable.Repeat(type, _width * _height * _depth * _d4).ToArray(),
                    _prefix==null ? null : Enumerable.Range(0, _width * _height * _depth * _d4).Select(j => $"{_prefix}[{j % _width},{(j/_width) % _height},{(j/_width/_height) % _depth},{j/_width/_height/_depth}]").ToArray()
                );

            var i = 0;
            var res = new GRBVar[_width, _height, _depth, _d4];
            for (var a = 0; a < _d4; a++)
                for (var z = 0; z < _depth; z++)
                    for (var y = 0; y < _height; y++)
                        for (var x = 0; x < _width; x++)
                            res[x, y, z, a] = vars[i++];
            return res;
        }

        public static GRBVar[] AddVars(this GRBModel _m, int _count, double lb, double ub, char type, string _prefix = null)
        {
            return _m.AddVars(Enumerable.Repeat(lb,_count).ToArray(),Enumerable.Repeat(ub,_count).ToArray(),null,Enumerable.Repeat(type,_count).ToArray(),
                    _prefix==null ? null : Enumerable.Range(0, _count).Select(i => $"{_prefix}[{i}]").ToArray()
                );
        }




        // COPYPASTA FROM http://stackoverflow.com/questions/2471588/how-to-get-index-using-linq
        ///<summary>Finds the index of the first item matching an expression in an enumerable.</summary>
        ///<param name="items">The enumerable to search.</param>
        ///<param name="predicate">The expression to test the items against.</param>
        ///<returns>The index of the first matching item, or -1 if no items match.</returns>
        public static int FindIndex<T>(this IEnumerable<T> items, Func<T, bool> predicate)
        {
            if (items == null) throw new ArgumentNullException("items");
            if (predicate == null) throw new ArgumentNullException("predicate");

            int retVal = 0;
            foreach (var item in items)
            {
                if (predicate(item))
                    return retVal;
                retVal++;
            }
            return -1;
        }

        // COPYPASTA FROM http://stackoverflow.com/questions/2471588/how-to-get-index-using-linq
        ///<summary>Finds the index of the first occurence of an item in an enumerable.</summary>
        ///<param name="items">The enumerable to search.</param>
        ///<param name="item">The item to find.</param>
        ///<returns>The index of the first matching item, or -1 if the item was not found.</returns>
        public static int IndexOf<T>(this IEnumerable<T> items, T item)
        {
            return items.FindIndex(i => EqualityComparer<T>.Default.Equals(item, i));
        }
    }
}
