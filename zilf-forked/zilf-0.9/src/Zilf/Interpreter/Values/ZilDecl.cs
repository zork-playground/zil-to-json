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
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using Zilf.Language;
using Zilf.Diagnostics;

namespace Zilf.Interpreter.Values
{
    [BuiltinType(StdAtom.DECL, PrimType.LIST)]
    class ZilDecl : ZilListBase
    {
        public ZilDecl([ItemNotNull] [NotNull] IEnumerable<ZilObject> sequence)
            : base(sequence)
        {
        }

        [NotNull]
        [ChtypeMethod]
        public static ZilDecl FromList([NotNull] ZilListBase list) => new ZilDecl(list);

        public override StdAtom StdTypeAtom => StdAtom.DECL;

        /// <exception cref="InterpreterError">The DECL syntax is invalid.</exception>
        public IEnumerable<KeyValuePair<ZilAtom, ZilObject>> GetAtomDeclPairs()
        {
            ZilListoidBase list = this;

            while (!list.IsEmpty)
            {
                if (!list.StartsWith(out ZilList atoms, out ZilObject decl))
                    break;

                if (!atoms.All(a => a is ZilAtom))
                    break;

                foreach (var zo in atoms)
                {
                    var atom = (ZilAtom)zo;
                    yield return new KeyValuePair<ZilAtom, ZilObject>(atom, decl);
                }

                list = list.GetRest(2);
                Debug.Assert(list != null);
            }

            if (!list.IsEmpty)
                throw new InterpreterError(InterpreterMessages.Malformed_DECL_Object);
        }
    }
}
