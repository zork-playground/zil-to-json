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
using System.Reflection;
using JetBrains.Annotations;
using Zilf.Language;

namespace Zilf.Interpreter.Values.Tied
{
    [SuppressMessage("ReSharper", "PatternAlwaysOfType")]
    abstract class ZilTiedListBase : ZilListoidBase
    {
        [NotNull]
        protected abstract TiedLayout GetLayout();

        TiedLayout MyLayout
        {
            get => TiedLayout.Layouts[GetType()];
            set => TiedLayout.Layouts[GetType()] = value;
        }

        [SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        protected ZilTiedListBase()
        {
            var myType = GetType();

            if (!TiedLayout.Layouts.ContainsKey(myType))
            {
                // ReSharper disable once VirtualMemberCallInConstructor
                MyLayout = GetLayout();
            }
        }

        [NotNull]
        public sealed override ZilObject GetPrimitive(Context ctx)
        {
            return new ZilList(this);
        }

        public override string ToString()
        {
            return SequenceToString(
                this,
                "#" + StdTypeAtom + " (",
                ")",
                zo => zo.ToString());
        }

        protected override string ToStringContextImpl(Context ctx, bool friendly)
        {
            return SequenceToString(
                this,
                "#" + GetTypeAtom(ctx).ToStringContext(ctx, friendly) + " (",
                ")",
                zo => zo.ToStringContext(ctx, friendly));
        }

        static readonly ObList detachedObList = new ObList();

        [NotNull]
        protected static ZilAtom GetStdAtom(StdAtom stdAtom)
        {
            /* Tied values with atoms in their printed representation may need to return
             * atoms from tied properties even when no Context is available.
             */
            if (Diagnostics.DiagnosticContext.Current.Frame?.Context is Context ctx)
                return ctx.GetStdAtom(stdAtom);

            return detachedObList[stdAtom.ToString()];
        }

        [NotNull]
        protected static ZilObject FALSE
        {
            get
            {
                if (Diagnostics.DiagnosticContext.Current.Frame?.Context is Context ctx)
                    return ctx.FALSE;

                return new ZilFalse(new ZilList(null, null));
            }
        }

        /// <exception cref="NotSupportedException" accessor="set">The element being written is tied to a read-only property.</exception>
        /// <exception cref="ArgumentOutOfRangeException" accessor="set"><paramref name="index"/> is out of range.</exception>
        public sealed override ZilObject this[int index]
        {
            get
            {
                var layout = MyLayout;

                if (index >= 0)
                {
                    if (index < layout.MinLength)
                        return (ZilObject)layout.PropertyInfos[index].GetValue(this);

                    if (layout.CatchAllPropertyInfo is PropertyInfo pi)
                    {
                        var catchAll = (IStructure)pi.GetValue(this);
                        return catchAll[index - layout.MinLength];
                    }
                }

                return null;
            }

            set
            {
                var layout = MyLayout;

                if (index >= 0)
                {
                    if (index < layout.MinLength)
                    {
                        var pi = layout.PropertyInfos[index];

                        if (pi.CanWrite)
                        {
                            pi.SetValue(this, value);
                        }
                        else
                        {
                            throw new NotSupportedException("read-only property");
                        }
                    }
                    else if (layout.CatchAllPropertyInfo is PropertyInfo pi)
                    {
                        if (pi.CanWrite)
                        {
                            var catchAll = (IStructure)pi.GetValue(this);
                            catchAll[index - layout.MinLength] = value;
                            pi.SetValue(this, catchAll);
                        }
                        else
                        {
                            throw new NotSupportedException("read-only property");
                        }
                    }
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
            }
        }

        public sealed override bool IsEmpty => GetLength(1) != 0;

        public sealed override IEnumerator<ZilObject> GetEnumerator()
        {
            var layout = MyLayout;

            var query = layout.PropertyInfos.Select(pi => (ZilObject)pi.GetValue(this));

            if (layout.CatchAllPropertyInfo is PropertyInfo pi2)
                query = query.Concat((IStructure)pi2.GetValue(this));

            return query.GetEnumerator();
        }

        public sealed override ZilObject First
        {
            get
            {
                var layout = MyLayout;

                if (layout.MinLength > 0)
                    return (ZilObject)layout.PropertyInfos[0].GetValue(this);

                if (layout.CatchAllPropertyInfo is PropertyInfo pi)
                    return ((IStructure)pi.GetValue(this)).GetFirst();

                return null;
            }

            set => this[0] = value;
        }

        // ReSharper disable once AnnotationConflictInHierarchy
        [NotNull]
        public sealed override ZilListoidBase Rest
        {
            get => GetRest(1) ?? new ZilList(null, null);
            set => throw new NotSupportedException();
        }

        public sealed override int GetLength()
        {
            var result = MyLayout.PropertyInfos.Count;

            if (MyLayout.CatchAllPropertyInfo is PropertyInfo pi)
                result += ((IStructure)pi.GetValue(this)).GetLength();

            return result;
        }

        public sealed override int? GetLength(int limit)
        {
            var result = MyLayout.PropertyInfos.Count;

            if (result > limit)
                return null;

            if (MyLayout.CatchAllPropertyInfo is PropertyInfo pi)
            {
                var more = ((IStructure)pi.GetValue(this)).GetLength(limit - result);

                if (more == null)
                    return null;

                result += (int)more;

                if (result > limit)
                    return null;
            }

            return result;
        }

        public sealed override ZilListoidBase GetRest(int skip)
        {
            return this.HasLengthAtLeast(skip) ? new Wrapper(this, skip) : null;
        }

        [BuiltinAlternate(typeof(ZilList))]
        sealed class Wrapper : ZilListoidBase
        {
            readonly ZilTiedListBase orig;
            readonly int offset;

            public Wrapper(ZilTiedListBase orig, int offset = 0)
            {
                this.orig = orig;
                this.offset = offset;
            }

            public override ZilObject this[int index]
            {
                get => orig[offset + index];
                set => orig[offset + index] = value;
            }

            public override StdAtom StdTypeAtom => StdAtom.LIST;

            public override bool IsEmpty => GetLength(1) != 0;

            public override IEnumerator<ZilObject> GetEnumerator()
            {
                return orig.Skip(offset).GetEnumerator();
            }

            public override ZilObject First
            {
                get => orig[offset];
                set => orig[offset] = value;
            }

            // ReSharper disable once AnnotationConflictInHierarchy
            [NotNull]
            public override ZilListoidBase Rest
            {
                get
                {
                    var rest = GetRest(1);
                    return rest == null ? new ZilList(null, null) : new ZilList(rest);
                }
                set => throw new NotSupportedException();
            }

            public override int GetLength() => orig.GetLength() - offset;

            public override int? GetLength(int limit) => orig.GetLength(limit + offset) - offset;

            [NotNull]
            public override ZilObject GetPrimitive(Context ctx)
            {
                return new ZilList(this);
            }

            public override ZilListoidBase GetRest(int skip)
            {
                return GetLength(skip) < skip ? null : new Wrapper(orig, offset + skip);
            }

            public override string ToString()
            {
                return SequenceToString(
                    this,
                    "(",
                    ")",
                    zo => zo.ToString());
            }

            protected override string ToStringContextImpl(Context ctx, bool friendly)
            {
                return SequenceToString(
                    this,
                    "(",
                    ")",
                    zo => zo.ToStringContext(ctx, friendly));
            }
        }
    }
}
