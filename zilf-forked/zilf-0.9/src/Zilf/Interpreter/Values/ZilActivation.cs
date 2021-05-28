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
using Zilf.Language;
using Zilf.Diagnostics;
using JetBrains.Annotations;

namespace Zilf.Interpreter.Values
{
    [BuiltinType(StdAtom.ACTIVATION, PrimType.ATOM)]
    class ZilActivation : ZilObject, IDisposable, IEvanescent
    {
        readonly ZilAtom name;

        public ZilActivation(ZilAtom name)
        {
            this.name = name;
        }

        public void Dispose()
        {
            IsLegal = false;
        }

        /// <exception cref="InterpreterError">Always thrown.</exception>
        [ChtypeMethod]
        [ContractAnnotation("=> halt")]
        public static ZilActivation FromAtom([NotNull] Context ctx, [NotNull] ZilAtom name) =>
            throw new InterpreterError(InterpreterMessages.CHTYPE_To_0_Not_Supported, "ACTIVATION");

        public override StdAtom StdTypeAtom => StdAtom.ACTIVATION;

        public override PrimType PrimType => PrimType.ATOM;

        public bool IsLegal { get; private set; } = true;

        public override ZilObject GetPrimitive(Context ctx)
        {
            return name;
        }

        public override string ToString()
        {
            return $"#ACTIVATION {name}";
        }

        protected override string ToStringContextImpl(Context ctx, bool friendly)
        {
            return $"#ACTIVATION {name.ToStringContext(ctx, friendly)}";
        }
    }
}
