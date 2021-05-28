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
using System.Collections;
using System.Collections.Generic;
using Zilf.Language;
using Zilf.Diagnostics;
using System.Linq;
using JetBrains.Annotations;

namespace Zilf.Interpreter.Values
{
    [BuiltinType(StdAtom.SEGMENT, PrimType.LIST)]
    class ZilSegment : ZilObject, IStructure, IMayExpandBeforeEvaluation
    {
        [NotNull]
        readonly ZilForm form;

        public ZilSegment([NotNull] ZilObject obj)
        {
            if (obj is ZilForm objForm)
                form = objForm;
            else
                throw new ArgumentException("Segment must be based on a FORM");
        }

        [NotNull]
        [ChtypeMethod]
        public static ZilSegment FromList([NotNull] ZilListBase list)
        {
            if (!(list is ZilForm form))
            {
                form = new ZilForm(list) { SourceLine = SourceLines.Chtyped };
            }

            return new ZilSegment(form);
        }

        [NotNull]
        public ZilForm Form => form;

        [NotNull]
        public override string ToString() => "!" + form;

        public override StdAtom StdTypeAtom => StdAtom.SEGMENT;

        public override PrimType PrimType => PrimType.LIST;

        public bool ShouldExpandBeforeEvaluation => true;

        [NotNull]
        public override ZilObject GetPrimitive(Context ctx) => new ZilList(form);

        protected override ZilResult EvalImpl(Context ctx, LocalEnvironment environment, ZilAtom originalType) =>
            throw new InterpreterError(InterpreterMessages.A_SEGMENT_Can_Only_Be_Evaluated_Inside_A_Structure);

        public override bool StructurallyEquals(ZilObject obj) =>
            obj is ZilSegment other && other.form.StructurallyEquals(form);

        public override int GetHashCode() => form.GetHashCode();

        #region IStructure Members

        public ZilObject GetFirst() => form.GetFirst();

        public IStructure GetRest(int skip) => form.GetRest(skip);

        public IStructure GetBack(int skip) => throw new NotSupportedException();

        public IStructure GetTop() => throw new NotSupportedException();

        public void Grow(int end, int beginning, ZilObject defaultValue) =>
            throw new NotSupportedException();

        public bool IsEmpty => form.IsEmpty;

        [CanBeNull]
        public ZilObject this[int index]
        {
            get => form[index];
            set => form[index] = value;
        }

        public int GetLength() => form.GetLength();

        public int? GetLength(int limit) => form.GetLength(limit);

        #endregion

        public IEnumerator<ZilObject> GetEnumerator() => form.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerable<ZilResult> ExpandBeforeEvaluation([NotNull] Context ctx, LocalEnvironment env)
        {
            var result = Form.Eval(ctx, env);

            if (result.ShouldPass())
                return Enumerable.Repeat(result, 1);

            if ((ZilObject)result is IEnumerable<ZilObject> sequence)
                return sequence.AsResultSequence();

            throw new InterpreterError(
                InterpreterMessages._0_1_Must_Return_2,
                InterpreterMessages.NoFunction,
                "segment evaluation",
                "a structure");
        }
    }
}