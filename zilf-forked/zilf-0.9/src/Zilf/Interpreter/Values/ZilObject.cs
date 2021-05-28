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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Zilf.Diagnostics;
using Zilf.Language;

namespace Zilf.Interpreter.Values
{
    abstract class ZilObject : IProvideSourceLine, ISettableSourceLine
    {
        /// <summary>
        /// Gets or sets a value indicating the object's source code location.
        /// </summary>
        public virtual ISourceLine SourceLine { get; set; }

        [NotNull]
        public static IEnumerable<ZilObject> ExpandTemplateToken([NotNull] ZilObject selector, [CanBeNull, ItemNotNull] ZilObject[] templateParams)
        {
            if (templateParams == null)
                throw new InterpreterError(InterpreterMessages.Templates_Cannot_Be_Used_Here);

            switch (selector)
            {
                case ZilFix fix
                    when fix.Value >= 0 && fix.Value < templateParams.Length:

                    return new[] { templateParams[fix.Value] };

                case ZilAdecl adecl
                    when adecl.First is ZilFix idx &&
                         idx.Value >= 0 && idx.Value < templateParams.Length &&
                         adecl.Second is ZilAtom atom &&
                         atom.StdAtom == StdAtom.SPLICE &&
                         templateParams[idx.Value] is IEnumerable<ZilObject> result:

                    return result;
            }

            throw new InterpreterError(InterpreterMessages.Unrecognized_0_1, "template reference", selector);
        }

        /// <summary>
        /// Determines whether another <see cref="ZilObject"/> is "the same object" as the current one,
        /// for the purposes of <c>==?</c> and <c>N==?</c> comparisons.
        /// </summary>
        /// <param name="other">The other object.</param>
        /// <returns><see langword="true"/> if the objects are identical; otherwise <see langword="false"/>.</returns>
        /// <remarks>
        /// MDL defines <c>==?</c> as being true iff the objects are made up of the same bits in
        /// MDL's internal representation. For non-structured types, this means they have the same <c>TYPE</c> and value.
        /// For structured types, it means they reference the same location in the same structure; for values of
        /// <c>PRIMTYPE</c> <c>LIST</c>, they may also both be empty.
        /// </remarks>
        /// <seealso cref="StructurallyEquals"/>
        public virtual bool ExactlyEquals(ZilObject other)
        {
            return ReferenceEquals(this, other);
        }

        /// <summary>
        /// Determines whether another <see cref="ZilObject"/> is "structurally equal" or "looks the same"
        /// as the current one, for the purposes of <c>=?</c> and <c>N=?</c> comparisons.
        /// </summary>
        /// <param name="other">The other object.</param>
        /// <returns><see langword="true"/> if the objects are structurally equal; otherwise <see langword="false"/>.</returns>
        /// <remarks>
        /// MDL defines <c>=?</c> as being true for non-structured objects iff they are "the same object" (<c>==?</c>);
        /// and for structured objects if they have the same <c>TYPE</c>, same length, and each element in one is
        /// <c>=?</c> to the corresponding element in the other.
        /// </remarks>
        /// <seealso cref="ExactlyEquals"/>
        public virtual bool StructurallyEquals([CanBeNull] ZilObject other)
        {
            return Equals(other);
        }

        /// <summary>
        /// Redirects to <see cref="ExactlyEquals"/>.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns><see langword="true"/> if <paramref name="obj"/> is a <see cref="ZilObject"/> and is <c>==?</c> to this object;
        /// otherwise <see langword="false"/>.</returns>
        public sealed override bool Equals(object obj)
        {
            return obj is ZilObject other && ExactlyEquals(other);
        }

        /// <summary>
        /// Redirects to <see cref="object.ReferenceEquals"/>.
        /// </summary>
        /// <param name="left">The first object to compare.</param>
        /// <param name="right">The second object to compare.</param>
        /// <returns><see langword="true"/> if the objects are identical; otherwise <see langword="false"/>.</returns>
        /// <remarks>This silences a ReSharper warning that shouldn't be suppressed completely.</remarks>
        public static bool operator ==([CanBeNull] ZilObject left, [CanBeNull] ZilObject right)
        {
            return ReferenceEquals(left, right);
        }

        /// <summary>
        /// Redirects to <see cref="object.ReferenceEquals"/>.
        /// </summary>
        /// <param name="left">The first object to compare.</param>
        /// <param name="right">The second object to compare.</param>
        /// <returns><see langword="false"/> if the objects are identical; otherwise <see langword="true"/>.</returns>
        /// <remarks>This silences a ReSharper warning that shouldn't be suppressed completely.</remarks>
        public static bool operator !=([CanBeNull] ZilObject left, [CanBeNull] ZilObject right)
        {
            return !ReferenceEquals(left, right);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            // keep the compiler happy
            // ReSharper disable once BaseObjectGetHashCodeCallInGetHashCode
            return base.GetHashCode();
        }

        /// <summary>
        /// Converts the ZIL object to a string (in reparsable format, if possible).
        /// </summary>
        /// <returns>A string representation of the object.</returns>
        /// <remarks>This method is unaffected by PRINTTYPE, since it has no context.</remarks>
        public abstract override string ToString();

        /// <summary>
        /// Converts the ZIL object to a string, given a context, and optionally
        /// in "friendly" (PRINC) format rather than reparsable format.
        /// </summary>
        /// <param name="ctx">The current context.</param>
        /// <param name="friendly">true if the string should be returned without
        /// quotes, escapes, or qualifiers.</param>
        /// <param name="ignorePrintType">true to ignore any PRINTTYPE set for this type
        /// and use the built-in formatting.</param>
        /// <returns>A string representation of the object.</returns>
        /// <remarks>If a PRINTTYPE is used, <paramref name="friendly"/> has no effect.</remarks>
        [NotNull]
        public string ToStringContext([NotNull] Context ctx, bool friendly, bool ignorePrintType = false)
        {
            if (!ignorePrintType)
            {
                var del = ctx.GetPrintTypeDelegate(GetTypeAtom(ctx));
                if (del != null)
                    return del(this);
            }

            return ToStringContextImpl(ctx, friendly);
        }

        /// <summary>
        /// Converts the ZIL object to a string, given a context, and optionally
        /// in "friendly" (PRINC) format rather than reparsable format.
        /// </summary>
        /// <param name="ctx">The current context.</param>
        /// <param name="friendly">true if the string should be returned without
        /// quotes, escapes, or qualifiers.</param>
        /// <returns>A string representation of the object.</returns>
        /// <remarks>This method is not affected by PRINTTYPE, which is handled
        /// by <see cref="ToStringContext(Context, bool, bool)"/>.</remarks>
        [NotNull]
        protected virtual string ToStringContextImpl([NotNull] Context ctx, bool friendly)
        {
            return ToString();
        }

        /// <summary>
        /// Gets an atom representing this object's type.
        /// </summary>
        /// <param name="ctx">The current context.</param>
        /// <returns>The type atom.</returns>
        [NotNull]
        [System.Diagnostics.Contracts.Pure]
        public virtual ZilAtom GetTypeAtom([NotNull] Context ctx)
        {
            var stdAtom = StdTypeAtom;
            return ctx.GetStdAtom(stdAtom);
        }

        /// <summary>
        /// Gets a <see cref="StdAtom"/> representing the object's type, or
        /// <see cref="StdAtom.None"/> if the object belongs to a user-defined type.
        /// </summary>
        [System.Diagnostics.Contracts.Pure]
        public abstract StdAtom StdTypeAtom { get; }

        /// <summary>
        /// Gets a value indicating the type of this object's primitive form.
        /// </summary>
        [System.Diagnostics.Contracts.Pure]
        public abstract PrimType PrimType { get; }

        /// <summary>
        /// Gets the primitive form of this object.
        /// </summary>
        /// <param name="ctx">The current context.</param>
        /// <returns>An object of the type indicated by <see cref="PrimType"/>.</returns>
        public abstract ZilObject GetPrimitive(Context ctx);

        /// <summary>
        /// Evaluates an object: performs function calls, duplicates lists, etc.
        /// </summary>
        /// <param name="ctx">The current context.</param>
        /// <param name="environment">The environment in which to evaluate the object,
        /// or <see langword="null"/> to use the current environment.</param>
        /// <returns>The result of evaluating this object, which may be the same object.</returns>
        [SuppressMessage("ReSharper", "ConvertIfStatementToReturnStatement")]
        public ZilResult Eval([NotNull] Context ctx, [CanBeNull] LocalEnvironment environment = null)
        {
            var del = ctx.GetEvalTypeDelegate(GetTypeAtom(ctx));

            if (del != null)
            {
                if (environment != null)
                    return ctx.ExecuteInEnvironment(environment, () => del(this));

                return del(this);
            }

            return EvalImpl(ctx, environment, null);
        }

        /// <summary>
        /// Evaluates an object on behalf of another type.
        /// </summary>
        /// <param name="ctx">The current context.</param>
        /// <param name="originalType">The type of the original object, which must have the
        /// same primtype.</param>
        /// <returns>The result of evaluating the object.</returns>
        /// <remarks>This is invoked when the original type has an EVALTYPE pointing to this
        /// type. The object being evaluated is temporarily CHTYPEd to this type in order to
        /// call this method. <see cref="EvalImpl(Context, LocalEnvironment, ZilAtom)"/> may
        /// use the knowledge of the original type to return a different result; for example,
        /// <see cref="ZilList.EvalImpl(Context, LocalEnvironment, ZilAtom)"/> returns a list
        /// CHTYPEd to the original type.</remarks>
        internal ZilResult EvalAsOtherType([NotNull] Context ctx, [NotNull] ZilAtom originalType)
        {
            return EvalImpl(ctx, null, originalType);
        }

        /// <summary>
        /// Evaluates an object: performs function calls, duplicates lists, etc.
        /// </summary>
        /// <param name="ctx">The current context.</param>
        /// <param name="environment">The environment in which to evaluate the object,
        /// or <see langword="null"/> to use the current environment.</param>
        /// <param name="originalType">The type atom of the original object being evaluated, if
        /// EVALTYPE processing has caused it to be changed to the current type, or <see langword="null"/> in
        /// the usual case.</param>
        /// <returns>The result of evaluating this object, which may be the same object.</returns>
        /// <remarks>
        /// <para>EVALTYPE is handled by <see cref="Eval(Context, LocalEnvironment)"/>.</para>
        /// <para><paramref name="originalType"/> is set to a type atom in cases where one type has
        /// its EVALTYPE set to the name of another type. When evaluating an object of the first type,
        /// it is CHTYPEd to the second type, and the second type's EvalImpl is called with the first
        /// type as a parameter. EvalImpl may use this to produce an object of the appropriate type;
        /// for example, see <see cref="ZilList.EvalImpl"/>.</para>
        /// </remarks>
        protected virtual ZilResult EvalImpl([NotNull] Context ctx, [CanBeNull] LocalEnvironment environment,
            [CanBeNull] ZilAtom originalType)
        {
            return this;
        }

        /// <summary>
        /// Expands a macro invocation.
        /// </summary>
        /// <param name="ctx">The current context.</param>
        /// <returns>The result of expanding this object, or the same object if this is
        /// not a macro invocation.</returns>
        public virtual ZilResult Expand([NotNull] Context ctx)
        {
            return this;
        }

        /// <summary>
        /// Gets a value indicating whether this object is "true", i.e. non-FALSE.
        /// </summary>
        public virtual bool IsTrue => true;

        /// <summary>
        /// Gets a value indicating whether this object is a local variable reference (.FOO).
        /// </summary>
        /// <param name="atom">Set to the referenced atom, or null.</param>
        /// <returns>True if the object is an LVAL.</returns>
        [ContractAnnotation("=> false, atom: null; => true, atom: notnull")]
        public virtual bool IsLVAL([CanBeNull] out ZilAtom atom)
        {
            atom = null;
            return false;
        }

        /// <summary>
        /// Gets a value indicating whether this object is a global variable reference (,FOO).
        /// </summary>
        /// <param name="atom">Set to the referenced atom, or null.</param>
        /// <returns>True if the object is a GVAL.</returns>
        [ContractAnnotation("=> false, atom: null; => true, atom: notnull")]
        public virtual bool IsGVAL([CanBeNull] out ZilAtom atom)
        {
            atom = null;
            return false;
        }

        /// <summary>
        /// Evaluates a series of expressions, returning the value of the last expression.
        /// </summary>
        /// <param name="ctx">The current context.</param>
        /// <param name="prog">The expressions to evaluate.</param>
        /// <returns>The value of the last expression evaluated.</returns>
        public static ZilResult EvalProgram([NotNull] Context ctx, [NotNull] [ItemNotNull] ZilObject[] prog)
        {
            ZilResult result = null;

            foreach (var zo in prog)
            {
                result = zo.Eval(ctx);
                if (result.ShouldPass())
                    break;
            }

            return result;
        }

        [NotNull]
        public static IEnumerable<ZilResult> ExpandOrEvalWithSplice([NotNull] Context ctx, [NotNull] ZilObject obj,
            [CanBeNull] LocalEnvironment environment)
        {
            if (obj is IMayExpandBeforeEvaluation expandBefore && expandBefore.ShouldExpandBeforeEvaluation)
                return expandBefore.ExpandBeforeEvaluation(ctx, environment);

            var result = obj.Eval(ctx, environment);
            if (result.ShouldPass())
                return Enumerable.Repeat(result, 1);

            if ((ZilObject)result is IMayExpandAfterEvaluation expandAfter && expandAfter.ShouldExpandAfterEvaluation)
                return expandAfter.ExpandAfterEvaluation().AsResultSequence();

            return Enumerable.Repeat(result, 1);
        }

        /// <summary>
        /// Evaluates a sequence of expressions, expanding segment references (!.X) when encountered.
        /// </summary>
        /// <param name="ctx">The current context.</param>
        /// <param name="sequence">The sequence to evaluate.</param>
        /// <param name="environment">The environment in which to evaluate the expressions,
        /// or <see langword="null"/> to use the current environment.</param>
        /// <returns>A sequence of evaluation results.</returns>
        /// <remarks>The values obtained by expanding segment references are not evaluated in turn.</remarks>
        [NotNull]
        public static IEnumerable<ZilResult> EvalSequence([NotNull] Context ctx, [ItemNotNull] [NotNull] IEnumerable<ZilObject> sequence,
            [CanBeNull] LocalEnvironment environment = null)
        {
            return sequence.SelectMany(zo => ExpandOrEvalWithSplice(ctx, zo, environment));
        }

        [NotNull]
        protected static IEnumerable<ZilResult> ExpandWithSplice([NotNull] Context ctx, [NotNull] ZilObject obj)
        {
            if (obj is IMayExpandBeforeEvaluation expandBefore && expandBefore.ShouldExpandBeforeEvaluation)
                return expandBefore.ExpandBeforeEvaluation(ctx, ctx.LocalEnvironment);

            var result = obj.Expand(ctx);
            var resultObj = (ZilObject)result;

            if (resultObj != obj && resultObj is IMayExpandAfterEvaluation expandAfter && expandAfter.ShouldExpandAfterEvaluation)
                return expandAfter.ExpandAfterEvaluation().AsResultSequence();

            return Enumerable.Repeat(result, 1);
        }

        [NotNull]
        protected static string SequenceToString([NotNull] IEnumerable<ZilObject> items,
            [NotNull] string start, [NotNull] string end, [NotNull] Func<ZilObject, string> convert)
        {
            var sb = new StringBuilder();
            sb.Append(start);

            if (items is ZilListBase list)
            {
                items = list.EnumerateNonRecursive();
            }

            bool first = true;

            foreach (var zo in items)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    sb.Append(' ');
                }

                sb.Append(zo == null ? "..." : convert(zo));
            }

            sb.Append(end);
            return sb.ToString();
        }
    }
}