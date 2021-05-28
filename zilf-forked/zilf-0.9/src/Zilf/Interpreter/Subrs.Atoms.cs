﻿/* Copyright 2010-2018 Jesse McGrew
 * 
 * This file is part of ZILF.
 * 
 * ZILF is free software: you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * ZILF is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with ZILF.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;
using Zilf.Common;
using Zilf.Interpreter.Values;
using Zilf.Language;
using Zilf.Diagnostics;

namespace Zilf.Interpreter
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    static partial class Subrs
    {
        [NotNull]
        [Subr]
        [Subr("PNAME")]
        public static ZilObject SPNAME(Context ctx, [NotNull] ZilAtom atom)
        {
            return ZilString.FromString(atom.Text);
        }

        [Subr]
        public static ZilObject PARSE([NotNull] Context ctx, [NotNull] string text, [Decl("'10")] int radix = 10,
            [CanBeNull] [Either(typeof(ObList), typeof(ZilList))] ZilObject lookupObList = null)
        {
            return PerformParse(ctx, text, radix, lookupObList, "PARSE", true);
        }

        [Subr]
        public static ZilObject LPARSE([NotNull] Context ctx, [NotNull] string text, [Decl("'10")] int radix = 10,
            [CanBeNull] [Either(typeof(ObList), typeof(ZilList))] ZilObject lookupObList = null)
        {
            return PerformParse(ctx, text, radix, lookupObList, "LPARSE", false);
        }

        static ZilObject PerformParse([NotNull] [ProvidesContext] Context ctx, [NotNull] string text, int radix, ZilObject lookupObList,
            [NotNull] string name, bool singleResult)
        {
            if (radix != 10)
                throw new ArgumentOutOfRangeException(nameof(radix));

            using (var innerEnv = ctx.PushEnvironment())
            {
                if (lookupObList != null)
                {
                    if (lookupObList is ObList)
                        lookupObList = new ZilList(lookupObList, new ZilList(null, null));

                    innerEnv.Rebind(ctx.GetStdAtom(StdAtom.OBLIST), lookupObList);
                }

                var ztree = Program.Parse(ctx, text);        // TODO: move into FrontEnd class
                if (singleResult)
                {
                    try
                    {
                        return ztree.First();
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new InterpreterError(InterpreterMessages._0_No_Expressions_Found, name, ex);
                    }
                }
                return new ZilList(ztree);
            }
        }

        [NotNull]
        [Subr]
        public static ZilObject UNPARSE([NotNull] Context ctx, [NotNull] ZilObject arg)
        {
            // in MDL, this takes an optional second argument (radix), but we don't bother

            return ZilString.FromString(arg.ToStringContext(ctx, false));
        }

        [NotNull]
        [Subr]
        public static ZilObject LOOKUP([NotNull] Context ctx, [NotNull] string str, [NotNull] ObList oblist)
        {
            return oblist.Contains(str) ? oblist[str] : ctx.FALSE;
        }

        /// <exception cref="InterpreterError"><paramref name="oblist"/> already contains an atom named <paramref name="stringOrAtom"/>, or <paramref name="stringOrAtom"/> is an atom that is already on a different OBLIST.</exception>
        [NotNull]
        [Subr]
        public static ZilObject INSERT([NotNull] Context ctx,
            [NotNull, Either(typeof(string), typeof(ZilAtom))] object stringOrAtom,
            [NotNull] ObList oblist)
        {
            switch (stringOrAtom)
            {
                case string str when oblist.Contains(str):
                    throw new InterpreterError(InterpreterMessages._0_OBLIST_Already_Contains_An_Atom_Named_1, "INSERT", str);

                case string str:
                    return oblist[str];

                case ZilAtom atom when atom.ObList != null:
                    throw new InterpreterError(InterpreterMessages._0_Atom_1_Is_Already_On_An_OBLIST, "INSERT",
                        atom.ToStringContext(ctx, false));

                case ZilAtom atom when oblist.Contains(atom.Text):
                    throw new InterpreterError(InterpreterMessages._0_OBLIST_Already_Contains_An_Atom_Named_1, "INSERT",
                        atom.Text);

                case ZilAtom atom:
                    atom.ObList = oblist;
                    return atom;
            }

            throw new UnreachableCodeException();
        }

#pragma warning disable CS0649
        public static class RemoveParams
        {
            [ZilSequenceParam]
            public struct PnameAndObList
            {
                public string Pname;
                public ObList ObList;
            }
        }
#pragma warning restore CS0649

        [NotNull]
        [Subr]
        public static ZilObject REMOVE(Context ctx,
            [NotNull] [Either(typeof(ZilAtom), typeof(RemoveParams.PnameAndObList), DefaultParamDesc = "atom")] object atomOrNameAndObList)
        {
            if (atomOrNameAndObList is ZilAtom atom)
            {
                if (atom.ObList != null)
                {
                    atom.ObList = null;
                    return atom;
                }
                return ctx.FALSE;
            }

            var nameAndOblist = (RemoveParams.PnameAndObList)atomOrNameAndObList;
            var pname = nameAndOblist.Pname;
            var oblist = nameAndOblist.ObList;

            if (oblist.Contains(pname))
            {
                atom = oblist[pname];
                atom.ObList = null;
                return atom;
            }

            return ctx.FALSE;
        }

        /// <exception cref="InterpreterError"><paramref name="oblist"/> already contains an atom named <paramref name="str"/>.</exception>
        [Subr]
        public static ZilObject LINK([NotNull] Context ctx, ZilObject value, [NotNull] string str, [NotNull] ObList oblist)
        {
            if (oblist.Contains(str))
                throw new InterpreterError(InterpreterMessages._0_OBLIST_Already_Contains_An_Atom_Named_1, "LINK", str);

            var link = new ZilLink(str, oblist);
            oblist[str] = link;

            ctx.SetGlobalVal(link, value);
            return value;
        }

        [NotNull]
        [Subr]
        public static ZilObject ATOM(Context ctx, [NotNull] string pname)
        {
            return new ZilAtom(pname, null, StdAtom.NONE);
        }

        [NotNull]
        [Subr]
        public static ZilObject ROOT([NotNull] Context ctx)
        {
            return ctx.RootObList;
        }

        [NotNull]
        [Subr]
        public static ZilObject MOBLIST([NotNull] Context ctx, [NotNull] ZilAtom name)
        {
            return ctx.GetProp(name, ctx.GetStdAtom(StdAtom.OBLIST)) as ObList ?? ctx.MakeObList(name);
        }

        [NotNull]
        [Subr("OBLIST?")]
        public static ZilObject OBLIST_P(Context ctx, [NotNull] ZilAtom atom)
        {
            return atom.ObList ?? ctx.FALSE;
        }

        [NotNull]
        [Subr]
        public static ZilObject BLOCK([NotNull] Context ctx, [NotNull] ZilList list)
        {
            ctx.PushObPath(list);
            return list;
        }

        /// <exception cref="InterpreterError">ENDBLOCK is not allowed here.</exception>
        [NotNull]
        [Subr]
        public static ZilObject ENDBLOCK([NotNull] Context ctx)
        {
            try
            {
                return ctx.PopObPath() ?? ctx.FALSE;
            }
            catch (InvalidOperationException ex)
            {
                throw new InterpreterError(InterpreterMessages.Misplaced_0, "ENDBLOCK", ex);
            }
        }

        [Subr]
        [MdlZilRedirect(typeof(Subrs), nameof(GLOBAL), TopLevelOnly = true)]
        public static ZilObject SETG([NotNull] Context ctx, [NotNull] ZilAtom atom, ZilObject value)
        {
            ctx.SetGlobalVal(atom, value);
            return value;
        }

        [Subr]
        public static ZilObject SETG20([NotNull] Context ctx, [NotNull] ZilAtom atom, ZilObject value)
        {
            ctx.SetGlobalVal(atom, value);
            return value;
        }

        [Subr]
        public static ZilObject SET(Context ctx, [NotNull] ZilAtom atom, ZilObject value, [NotNull] LocalEnvironment env)
        {
            env.SetLocalVal(atom, value);
            return value;
        }

        /// <exception cref="InterpreterError"><paramref name="atom"/> has no global value.</exception>
        [NotNull]
        [Subr]
        public static ZilObject GVAL([NotNull] Context ctx, [NotNull] ZilAtom atom)
        {
            var result = ctx.GetGlobalVal(atom);
            if (result == null)
                throw new InterpreterError(
                    InterpreterMessages._0_Atom_1_Has_No_2_Value,
                    "GVAL",
                    atom.ToStringContext(ctx, false),
                    "global");

            return result;
        }

        [NotNull]
        [Subr("GASSIGNED?")]
        public static ZilObject GASSIGNED_P([NotNull] Context ctx, [NotNull] ZilAtom atom)
        {
            return ctx.GetGlobalVal(atom) != null ? ctx.TRUE : ctx.FALSE;
        }

        [NotNull]
        [Subr]
        public static ZilObject GUNASSIGN([NotNull] Context ctx, [NotNull] ZilAtom atom)
        {
            ctx.SetGlobalVal(atom, null);
            return atom;
        }

        [NotNull]
        [Subr("GBOUND?")]
        public static ZilObject GBOUND_P([NotNull] Context ctx, [NotNull] ZilAtom atom)
        {
            return ctx.GetGlobalBinding(atom, false) != null ? ctx.TRUE : ctx.FALSE;
        }

#pragma warning disable CS0649
        public static class DeclParams
        {
            [ZilSequenceParam]
            public struct AtomsDeclSequence
            {
                public AtomList Atoms;
                public ZilObject Decl;
            }

            [ZilStructuredParam(StdAtom.LIST)]
            public struct AtomList
            {
                public ZilAtom[] Atoms;
            }
        }
#pragma warning restore CS0649

        [NotNull]
        [FSubr]
        public static ZilObject GDECL([NotNull] Context ctx, [NotNull] DeclParams.AtomsDeclSequence[] pairs)
        {
            foreach (var pair in pairs)
            {
                foreach (var atom in pair.Atoms.Atoms)
                {
                    var binding = ctx.GetGlobalBinding(atom, true);
                    binding.Decl = pair.Decl;
                }
            }

            return ctx.TRUE;
        }

        [NotNull]
        [Subr("DECL?")]
        public static ZilObject DECL_P([NotNull] Context ctx, [NotNull] ZilObject value, [NotNull] ZilObject pattern)
        {
            return Decl.Check(ctx, value, pattern) ? ctx.TRUE : ctx.FALSE;
        }

        [NotNull]
        [Subr("DECL-CHECK")]
        public static ZilObject DECL_CHECK([NotNull] Context ctx, bool enable)
        {
            var wasEnabled = ctx.CheckDecls;
            ctx.CheckDecls = enable;
            return wasEnabled ? ctx.TRUE : ctx.FALSE;
        }

        [Subr("GET-DECL")]
        public static ZilResult GET_DECL(Context ctx, [NotNull] ZilObject item)
        {
            if (item is ZilOffset offset)
                return offset.StructurePattern;

            return GETPROP(ctx, item, ctx.GetStdAtom(StdAtom.DECL));
        }

        [NotNull]
        [Subr("PUT-DECL")]
        public static ZilObject PUT_DECL(Context ctx, [NotNull] ZilObject item, ZilObject pattern)
        {
            if (item is ZilOffset offset)
                return new ZilOffset(offset.Index, pattern, offset.ValuePattern);

            return PUTPROP(ctx, item, ctx.GetStdAtom(StdAtom.DECL), pattern);
        }

        /// <exception cref="InterpreterError"><paramref name="atom"/> has no local value in <paramref name="env"/>.</exception>
        [NotNull]
        [Subr]
        public static ZilObject LVAL(Context ctx, [NotNull] ZilAtom atom, [NotNull] LocalEnvironment env)
        {
            var result = env.GetLocalVal(atom);
            if (result == null)
                throw new InterpreterError(
                    InterpreterMessages._0_Atom_1_Has_No_2_Value,
                    "LVAL",
                    atom.ToStringContext(ctx, false),
                    "local");

            return result;
        }

        /// <exception cref="ArgumentNullException"><paramref name="env"/> is <see langword="null"/></exception>
        [NotNull]
        [Subr]
        public static ZilObject UNASSIGN(Context ctx, [NotNull] ZilAtom atom, [NotNull] LocalEnvironment env)
        {
            if (atom == null)
                throw new ArgumentNullException(nameof(atom));
            if (env == null)
                throw new ArgumentNullException(nameof(env));

            env.SetLocalVal(atom, null);
            return atom;
        }

        [NotNull]
        [Subr("ASSIGNED?")]
        public static ZilObject ASSIGNED_P([NotNull] Context ctx, [NotNull] ZilAtom atom, [NotNull] LocalEnvironment env)
        {
            return env.GetLocalVal(atom) != null ? ctx.TRUE : ctx.FALSE;
        }

        [NotNull]
        [Subr("BOUND?")]
        public static ZilObject BOUND_P([NotNull] Context ctx, [NotNull] ZilAtom atom, [NotNull] LocalEnvironment env)
        {
            return env.IsLocalBound(atom) ? ctx.TRUE : ctx.FALSE;
        }

        /// <exception cref="InterpreterError"><paramref name="atom"/> has no local or global value in <paramref name="env"/>.</exception>
        [NotNull]
        [Subr]
        public static ZilObject VALUE(Context ctx, [NotNull] ZilAtom atom, [NotNull] LocalEnvironment env)
        {
            var result = env.GetLocalVal(atom) ?? ctx.GetGlobalVal(atom);
            if (result == null)
                throw new InterpreterError(
                    InterpreterMessages._0_Atom_1_Has_No_2_Value,
                    "VALUE",
                    atom.ToStringContext(ctx, false),
                    "local or global");

            return result;
        }

        [Subr]
        public static ZilResult GETPROP([NotNull] Context ctx, [NotNull] ZilObject item, [NotNull] ZilObject indicator,
            [CanBeNull] ZilObject defaultValue = null)
        {
            return ctx.GetProp(item, indicator) ?? defaultValue?.Eval(ctx) ?? ctx.FALSE;
        }

        [NotNull]
        [Subr]
        public static ZilObject PUTPROP([NotNull] Context ctx, [NotNull] ZilObject item, [NotNull] ZilObject indicator,
            [CanBeNull] ZilObject value = null)
        {
            if (value == null)
            {
                // clear, and return previous value or <>
                var result = ctx.GetProp(item, indicator);
                ctx.PutProp(item, indicator, null);
                return result ?? ctx.FALSE;
            }

            // set, and return first arg
            ctx.PutProp(item, indicator, value);
            return item;
        }

        [NotNull]
        [Subr]
        public static ZilObject ASSOCIATIONS([NotNull] Context ctx)
        {
            var results = ctx.GetAllAssociations();

            return results.Length > 0 ? new ZilAsoc(results, 0) : ctx.FALSE;
        }

        [NotNull]
        [Subr]
        public static ZilObject NEXT(Context ctx, [NotNull] ZilAsoc asoc)
        {
            return asoc.GetNext() ?? ctx.FALSE;
        }

        [Subr]
        public static ZilObject ITEM(Context ctx, [NotNull] ZilAsoc asoc)
        {
            return asoc.Item;
        }

        [Subr]
        public static ZilObject INDICATOR(Context ctx, [NotNull] ZilAsoc asoc)
        {
            return asoc.Indicator;
        }

        [Subr]
        public static ZilObject AVALUE(Context ctx, [NotNull] ZilAsoc asoc)
        {
            return asoc.Value;
        }
    }
}
