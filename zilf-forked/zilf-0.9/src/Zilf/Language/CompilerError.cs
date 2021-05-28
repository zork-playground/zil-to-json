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
using System.Runtime.Serialization;
using JetBrains.Annotations;
using Zilf.Diagnostics;

namespace Zilf.Language
{
    [Serializable]
    class CompilerError : ZilError<CompilerMessages>
    {
        public CompilerError(int code)
            : this(code, null)
        {
        }

        public CompilerError(int code, [ItemNotNull] params object[] messageArgs)
            : this(DiagnosticContext.Current.SourceLine, code, messageArgs)
        {
        }

        public CompilerError([CanBeNull] ISourceLine sourceLine, int code)
            : this(sourceLine, code, null)
        {
        }

        public CompilerError([CanBeNull] ISourceLine sourceLine, int code, [ItemNotNull] params object[] messageArgs)
            : base(MakeDiagnostic(sourceLine, code, messageArgs))
        {
        }

        public CompilerError([NotNull] IProvideSourceLine sourceLine, int code)
           : this(sourceLine, code, null)
        {
        }

        public CompilerError([NotNull] IProvideSourceLine node, int code, params object[] messageArgs)
            : base(MakeDiagnostic(node.SourceLine, code, messageArgs))
        {
        }

        [UsedImplicitly]
        public CompilerError([NotNull] Diagnostic diagnostic)
            : base(diagnostic)
        {
        }

        protected CompilerError([NotNull] SerializationInfo si, StreamingContext sc)
            : base(si, sc)
        {
        }

        [NotNull]
        public static CompilerError WrongArgCount([NotNull] string name, [NotNull] IEnumerable<ArgCountRange> ranges,
            int? acceptableVersion = null)
        {
            ArgCountHelpers.FormatArgCount(ranges, out var cs);
            return WrongArgCount(name, cs, acceptableVersion);
        }

        [NotNull]
        public static CompilerError WrongArgCount([NotNull] string name, ArgCountRange range,
            int? acceptableVersion = null)
        {
            ArgCountHelpers.FormatArgCount(range, out var cs);
            return WrongArgCount(name, cs, acceptableVersion);
        }

        [NotNull]
        static CompilerError WrongArgCount([NotNull] string name, CountableString cs, int? acceptableVersion)
        {
            var error = new CompilerError(CompilerMessages._0_Requires_1_Argument1s, name, cs);

            if (acceptableVersion != null)
            {
                var info = new CompilerError(
                    CompilerMessages.This_Arg_Count_Would_Be_Legal_In_Other_Zmachine_Versions_Eg_V0,
                    acceptableVersion);
                error = error.Combine(info);
            }

            return error;
        }
    }
}