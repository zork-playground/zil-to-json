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

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Zilf.Diagnostics;
using Zilf.Interpreter;
using Zilf.Interpreter.Values;
using Zilf.Interpreter.Values.Tied;
using Zilf.Language;

namespace Zilf.ZModel.Values
{
    [BuiltinType(StdAtom.ROUTINE, PrimType.LIST)]
    sealed class ZilRoutine : ZilTiedListBase
    {
        [ItemNotNull]
        [NotNull]
        ZilObject[] body;

        public ZilRoutine([CanBeNull] ZilAtom name, [CanBeNull] ZilAtom activationAtom,
            [NotNull] IEnumerable<ZilObject> argspec, [ItemNotNull] [NotNull] IEnumerable<ZilObject> body, RoutineFlags flags)
        {
            Name = name;
            ArgSpec = ArgSpec.Parse("ROUTINE", name, activationAtom, argspec);
            this.body = body.ToArray();
            Flags = flags;
        }

        /// <exception cref="InterpreterError"><paramref name="list"/> has the wrong number or types of elements.</exception>
        [NotNull]
        [ChtypeMethod]
        public static ZilRoutine FromList([NotNull] ZilListBase list)
        {
            if (list.Rest?.IsEmpty != true)
                throw new InterpreterError(
                    InterpreterMessages._0_Must_Have_1_Element1s,
                    "list coerced to ROUTINE",
                    new CountableString("at least 2", true));

            if (list.First is ZilList argList)
            {
                return new ZilRoutine(null, null, argList, list.Rest, RoutineFlags.None);
            }

            throw new InterpreterError(InterpreterMessages.Element_0_Of_1_Must_Be_2, 1, "list coerced to ROUTINE", "a list");
        }

        protected override TiedLayout GetLayout()
        {
            return TiedLayout.Create<ZilRoutine>(
                x => x.ArgSpecAsList,
                x => x.BodyAsList);
        }

        [NotNull]
        public ArgSpec ArgSpec { get; private set; }

        [NotNull]
        public IEnumerable<ZilObject> Body => body;
        public int BodyLength => body.Length;

        [CanBeNull]
        public ZilAtom Name { get; }

        [CanBeNull]
        public ZilAtom ActivationAtom => ArgSpec.ActivationAtom;
        public RoutineFlags Flags { get; }

        [NotNull]
        ZilList ArgSpecAsList => ArgSpec.ToZilList();

        [NotNull]
        ZilList BodyAsList => new ZilList(body);

        public override StdAtom StdTypeAtom => StdAtom.ROUTINE;

        public override bool StructurallyEquals(ZilObject obj)
        {
            if (!(obj is ZilRoutine other))
                return false;

            if (!other.ArgSpec.Equals(ArgSpec))
                return false;

            if (other.body.Length != body.Length)
                return false;

            for (int i = 0; i < body.Length; i++)
                if (!other.body[i].StructurallyEquals(body[i]))
                    return false;

            return true;
        }

        internal void ExpandInPlace([NotNull] Context ctx)
        {
            IEnumerable<ZilObject> RecursiveExpandWithSplice(ZilObject zo)
            {
                ZilObject result;

                ZilObject SetSourceLine(ZilResult zr)
                {
                    var newObj = (ZilObject)zr;
                    newObj.SourceLine = zo.SourceLine;
                    return newObj;
                }

                switch (zo)
                {
                    case ZilList list:
                        result = new ZilList(list.SelectMany(RecursiveExpandWithSplice));
                        break;

                    case ZilVector vector:
                        result = new ZilVector(vector.SelectMany(RecursiveExpandWithSplice).ToArray());
                        break;

                    case ZilForm form:
                        ZilObject expanded;
                        try
                        {
                            using (DiagnosticContext.Push(form.SourceLine))
                            {
                                expanded = (ZilObject)form.Expand(ctx);
                            }
                        }
                        catch (InterpreterError ex)
                        {
                            ctx.HandleError(ex);
                            return new[] { ctx.FALSE };
                        }
                        if (expanded is IMayExpandAfterEvaluation expandAfter &&
                            expandAfter.ShouldExpandAfterEvaluation)
                        {
                            return expandAfter.ExpandAfterEvaluation().AsResultSequence()
                                .Select(SetSourceLine)
                                .Select(xo => ReferenceEquals(xo, form) ? xo : new ZilMacroResult(xo));
                        }
                        else if (!ReferenceEquals(expanded, form))
                        {
                            expanded.SourceLine = zo.SourceLine;
                            return RecursiveExpandWithSplice(expanded)
                                .Select(xo => new ZilMacroResult(xo));
                        }
                        else
                        {
                            result = new ZilForm(form.SelectMany(RecursiveExpandWithSplice));
                        }
                        break;

                    case ZilAdecl adecl:
                        result = new ZilAdecl(new ZilVector(adecl.SelectMany(RecursiveExpandWithSplice).ToArray()));
                        break;

                    default:
                        return ExpandWithSplice(ctx, zo)
                            .Select(SetSourceLine)
                            .Select(xo => ReferenceEquals(xo, zo) ? xo : new ZilMacroResult(xo));
                }

                result.SourceLine = zo.SourceLine;
                return new[] { result };
            }

            // expand argument defaults
            ArgSpec = ArgSpec.Parse(
                "<macro expansion>",
                ArgSpec.Name,
                null,
                ArgSpec.AsZilListBody().SelectMany(RecursiveExpandWithSplice));

            // expand body
            body = body.SelectMany(RecursiveExpandWithSplice).ToArray();
        }
    }
}