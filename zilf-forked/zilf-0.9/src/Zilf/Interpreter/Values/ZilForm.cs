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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Zilf.Diagnostics;
using Zilf.Language;
using JetBrains.Annotations;

namespace Zilf.Interpreter.Values
{
    [BuiltinType(StdAtom.FORM, PrimType.LIST)]
    sealed class ZilForm : ZilListBase
    {
        public ZilForm([NotNull] IEnumerable<ZilObject> sequence)
            : base(sequence)
        {
        }

        public ZilForm(ZilObject first, ZilListoidBase rest)
            : base(first, rest) { }

        [NotNull]
        public override ISourceLine SourceLine
        {
            get => base.SourceLine ?? SourceLines.Unknown;
            set => base.SourceLine = value;
        }

        [NotNull]
        [ChtypeMethod]
        public static ZilForm FromList([NotNull] ZilListBase list) => new ZilForm(list.First, list.Rest);

        protected override string OpenBracket => "<";

        protected override string CloseBracket => ">";

        [NotNull]
        string ToString([NotNull] Func<ZilObject, string> convert)
        {
            if (Recursion.TryLock(this))
            {
                try
                {
                    // check for special forms
                    if (First is ZilAtom firstAtom && GetLength(2) == 2)
                    {
                        Debug.Assert(Rest != null);

                        var arg = Rest.First;

                        switch (firstAtom.StdAtom)
                        {
                            case StdAtom.GVAL:
                                return "," + convert(arg);

                            case StdAtom.LVAL:
                                return "." + convert(arg);

                            case StdAtom.QUOTE:
                                return "'" + convert(arg);
                        }
                    }

                    // otherwise display like a list with angle brackets
                    return SequenceToString(this, "<", ">", convert);
                }
                finally
                {
                    Recursion.Unlock(this);
                }
            }
            return "<...>";
        }

        public override string ToString()
        {
            return ToString(zo => zo?.ToString());
        }

        protected override string ToStringContextImpl(Context ctx, bool friendly)
        {
            return ToString(zo => zo?.ToStringContext(ctx, friendly));
        }

        public override StdAtom StdTypeAtom => StdAtom.FORM;

        /// <exception cref="InterpreterError">The form's first element is an atom that has no local or global value, or a non-applicable type.</exception>
        protected override ZilResult EvalImpl(Context ctx, LocalEnvironment environment, ZilAtom originalType)
        {
            if (environment != null)
            {
                return ctx.ExecuteInEnvironment(environment, () => Eval(ctx));
            }

            if (First == null)
            {
                Debug.Assert(Rest == null);
                return ctx.FALSE;
            }

            Debug.Assert(Rest != null);

            using (var frame = ctx.PushFrame(this))
            using (DiagnosticContext.Push(SourceLine, frame))
            {
                ZilObject target;
                if (First is ZilAtom fa)
                {
                    target = ctx.GetGlobalVal(fa) ?? ctx.GetLocalVal(fa);
                    if (target == null)
                        throw new InterpreterError(this, InterpreterMessages.Calling_Unassigned_Atom_0,
                            fa.ToStringContext(ctx, false));
                }
                else
                {
                    var result = First.Eval(ctx);
                    if (result.ShouldPass())
                        return result;

                    target = (ZilObject)result;
                }

                var applicable = target.AsApplicable(ctx);
                if (applicable != null)
                {
                    return applicable.Apply(ctx, Rest.ToArray());
                }

                throw new InterpreterError(this, InterpreterMessages.Not_An_Applicable_Type_0,
                    target.GetTypeAtom(ctx).ToStringContext(ctx, false));
            }
        }

        public override ZilResult Expand(Context ctx)
        {
            if (First == null || Rest == null)
                return this;

            ZilObject target;
            bool usedGlobal, usedLocal;
            switch (First)
            {
                case ZilAtom fa when (target = ctx.GetGlobalVal(fa)) != null:
                    usedGlobal = true;
                    usedLocal = false;
                    break;

                case ZilAtom fa when (target = ctx.GetLocalVal(fa)) != null:
                    usedLocal = true;
                    usedGlobal = false;
                    break;

                default:
                    target = First;
                    usedGlobal = usedLocal = false;
                    break;
            }

            switch (target)
            {
                case ZilEvalMacro macro:
                    using (var frame = ctx.PushFrame(this))
                    using (DiagnosticContext.Push(SourceLine, frame))
                    {
                        var result = macro.Expand(ctx, Rest.ToArray());
                        if (result.ShouldPass())
                            return result;

                        if (!((ZilObject)result is ZilForm resultForm) || ReferenceEquals(resultForm, this))
                            return result;

                        // set the source info on the expansion to match the macro invocation
                        resultForm = DeepRewriteSourceInfo(resultForm, SourceLine);
                        return resultForm.Expand(ctx);
                    }

                case ZilFix _:
                    // TODO: is rewriting in place really the right behavior here?

                    // if First was an atom, we need to make it an LVAL/GVAL
                    var index = usedGlobal ? ctx.ChangeType(First, ctx.GetStdAtom(StdAtom.GVAL))
                        : usedLocal ? ctx.ChangeType(First, ctx.GetStdAtom(StdAtom.LVAL))
                        : First;

                    if (this.Matches(out ZilObject _, out ZilObject structure, out ZilObject item))
                    {
                        // <1 FOO BAR> => <PUT FOO 1 BAR>
                        First = ctx.GetStdAtom(StdAtom.PUT);
                        Rest = new ZilList(structure,
                            new ZilList(index, new ZilList(item, new ZilList(null, null))));
                    }
                    else if (this.Matches(out ZilObject _, out structure))
                    {
                        // <1 FOO> => <GET FOO 1>
                        First = ctx.GetStdAtom(StdAtom.GET);
                        Rest = new ZilList(structure, new ZilList(index, new ZilList(null, null)));
                    }
                    break;
            }

            return this;
        }

        [NotNull]
        static ZilForm DeepRewriteSourceInfo([NotNull] ZilForm other, [NotNull] ISourceLine src)
        {
            return new ZilForm(DeepRewriteSourceInfoContents(other, src)) { SourceLine = src };
        }

        static IEnumerable<ZilObject> DeepRewriteSourceInfoContents(
            [ItemNotNull] [NotNull] IEnumerable<ZilObject> contents, [NotNull] ISourceLine src)
        {
            foreach (var item in contents)
            {
                if (item is ZilForm form)
                {
                    yield return DeepRewriteSourceInfo(form, src);
                }
                else
                {
                    yield return item;
                }
            }
        }

        [ContractAnnotation("=> true, atom: notnull; => false, atom: null")]
        public override bool IsLVAL(out ZilAtom atom) => IsTwoElementFormWithStdAtom(StdAtom.LVAL, out atom);

        [ContractAnnotation("=> true, atom: notnull; => false, atom: null")]
        public override bool IsGVAL(out ZilAtom atom) => IsTwoElementFormWithStdAtom(StdAtom.GVAL, out atom);

        bool IsTwoElementFormWithStdAtom(StdAtom stdAtom, [CanBeNull] out ZilAtom atom)
        {
            if (First is ZilAtom head &&
                head.StdAtom == stdAtom)
            {
                Debug.Assert(Rest != null);
                if (Rest.Rest?.IsEmpty == true
                    && Rest.First is ZilAtom name)
                {
                    atom = name;
                    return true;
                }
            }

            atom = null;
            return false;
        }

        public override bool ExactlyEquals(ZilObject other)
        {
            if (ReferenceEquals(this, other))
                return true;

            return (IsLVAL(out var myAtom) && other.IsLVAL(out var theirAtom) ||
                    IsGVAL(out myAtom) && other.IsGVAL(out theirAtom)) &&
                   myAtom == theirAtom;
        }

        public override int GetHashCode()
        {
            if (IsLVAL(out var atom))
                return atom.GetHashCode() ^ StdAtom.LVAL.GetHashCode();

            if (IsGVAL(out atom))
                return atom.GetHashCode() ^ StdAtom.GVAL.GetHashCode();

            return base.GetHashCode();
        }
    }
}