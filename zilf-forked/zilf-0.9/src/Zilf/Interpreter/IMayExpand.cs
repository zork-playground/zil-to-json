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
using JetBrains.Annotations;
using Zilf.Interpreter.Values;

namespace Zilf.Interpreter
{
    interface IMayExpandBeforeEvaluation
    {
        bool ShouldExpandBeforeEvaluation { get; }
        [NotNull]
        IEnumerable<ZilResult> ExpandBeforeEvaluation(Context ctx, LocalEnvironment env);
    }

    interface IMayExpandAfterEvaluation
    {
        bool ShouldExpandAfterEvaluation { get; }
        [NotNull]
        IEnumerable<ZilObject> ExpandAfterEvaluation();
    }
}
