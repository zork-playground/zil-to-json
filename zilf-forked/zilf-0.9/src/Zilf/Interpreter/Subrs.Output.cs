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
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Zilf.Interpreter.Values;
using Zilf.Language;
using Zilf.Diagnostics;
using JetBrains.Annotations;

namespace Zilf.Interpreter
{
    static partial class Subrs
    {
        /// <exception cref="InterpreterError">Bad OUTCHAN.</exception>
        [NotNull]
        [Subr]
        public static ZilObject PRINT([NotNull] Context ctx, [NotNull] ZilObject value, ZilChannel channel = null)
        {
            if (channel == null)
            {
                channel =
                    ctx.GetLocalVal(ctx.GetStdAtom(StdAtom.OUTCHAN)) as ZilChannel ??
                    ctx.GetGlobalVal(ctx.GetStdAtom(StdAtom.OUTCHAN)) as ZilChannel;
                if (channel == null)
                    throw new InterpreterError(InterpreterMessages._0_Bad_OUTCHAN, "PRINT");
            }

            var str = value.ToStringContext(ctx, false);

            // TODO: check for I/O error
            channel.WriteNewline();
            channel.WriteString(str);
            channel.WriteChar(' ');

            return value;
        }

        /// <exception cref="InterpreterError">Bad OUTCHAN.</exception>
        [NotNull]
        [Subr]
        public static ZilObject PRIN1([NotNull] Context ctx, [NotNull] ZilObject value, ZilChannel channel = null)
        {
            if (channel == null)
            {
                channel =
                    ctx.GetLocalVal(ctx.GetStdAtom(StdAtom.OUTCHAN)) as ZilChannel ??
                    ctx.GetGlobalVal(ctx.GetStdAtom(StdAtom.OUTCHAN)) as ZilChannel;
                if (channel == null)
                    throw new InterpreterError(InterpreterMessages._0_Bad_OUTCHAN, "PRIN1");
            }

            var str = value.ToStringContext(ctx, false);

            // TODO: check for I/O error
            channel.WriteString(str);

            return value;
        }

        /// <exception cref="InterpreterError">Bad OUTCHAN.</exception>
        [NotNull]
        [Subr]
        public static ZilObject PRINC([NotNull] Context ctx, [NotNull] ZilObject value, ZilChannel channel = null)
        {
            if (channel == null)
            {
                channel =
                    ctx.GetLocalVal(ctx.GetStdAtom(StdAtom.OUTCHAN)) as ZilChannel ??
                    ctx.GetGlobalVal(ctx.GetStdAtom(StdAtom.OUTCHAN)) as ZilChannel;
                if (channel == null)
                    throw new InterpreterError(InterpreterMessages._0_Bad_OUTCHAN, "PRINC");
            }

            var str = value.ToStringContext(ctx, true);

            // TODO: check for I/O error
            channel.WriteString(str);

            return value;
        }

        /// <exception cref="InterpreterError">Bad OUTCHAN.</exception>
        [NotNull]
        [Subr]
        public static ZilObject CRLF([NotNull] Context ctx, ZilChannel channel = null)
        {
            if (channel == null)
            {
                channel =
                    ctx.GetLocalVal(ctx.GetStdAtom(StdAtom.OUTCHAN)) as ZilChannel ??
                    ctx.GetGlobalVal(ctx.GetStdAtom(StdAtom.OUTCHAN)) as ZilChannel;
                if (channel == null)
                    throw new InterpreterError(InterpreterMessages._0_Bad_OUTCHAN, "CRLF");
            }

            // TODO: check for I/O error
            channel.WriteNewline();

            return ctx.TRUE;
        }

        /// <exception cref="InterpreterError"><paramref name="printer"/> is an atom which has no local or global value.</exception>
        [Subr("PRINT-MANY")]
        public static ZilObject PRINT_MANY([NotNull] Context ctx, ZilChannel channel,
            [Decl("<OR ATOM APPLICABLE>")] ZilObject printer, [NotNull] ZilObject[] items)
        {
            if (printer is ZilAtom atom)
            {
                printer = ctx.GetGlobalVal(atom) ?? ctx.GetLocalVal(atom);
                if (printer == null)
                    throw new InterpreterError(
                        InterpreterMessages._0_Atom_1_Has_No_2_Value,
                        "PRINT-MANY",
                        atom.ToStringContext(ctx, false),
                        "local or global");
            }

            var applicablePrinter = printer.AsApplicable(ctx);
            if (applicablePrinter == null)
                throw new InterpreterError(InterpreterMessages._0_Not_Applicable_1, "PRINT-MANY", printer.ToStringContext(ctx, false));

            var crlf = ctx.GetStdAtom(StdAtom.PRMANY_CRLF);
            var result = ctx.TRUE;

            using (var innerEnv = ctx.PushEnvironment())
            {
                innerEnv.Rebind(ctx.GetStdAtom(StdAtom.OUTCHAN), channel);

                var printArgs = new ZilObject[1];

                foreach (var item in items)
                {
                    result = item;

                    if (result == crlf)
                    {
                        CRLF(ctx);
                    }
                    else
                    {
                        printArgs[0] = result;
                        applicablePrinter.ApplyNoEval(ctx, printArgs);
                    }
                }
            }

            return result;
        }

        /// <exception cref="InterpreterError">Bad OUTCHAN.</exception>
        [NotNull]
        [Subr]
        public static ZilObject IMAGE([NotNull] Context ctx, [NotNull] ZilFix ch, ZilChannel channel = null)
        {
            if (channel == null)
            {
                channel =
                    ctx.GetLocalVal(ctx.GetStdAtom(StdAtom.OUTCHAN)) as ZilChannel ??
                    ctx.GetGlobalVal(ctx.GetStdAtom(StdAtom.OUTCHAN)) as ZilChannel;
                if (channel == null)
                    throw new InterpreterError(InterpreterMessages._0_Bad_OUTCHAN, "IMAGE");
            }

            // TODO: check for I/O error
            channel.WriteChar((char)ch.Value);

            return ch;
        }

        static readonly Regex RetroPathRE = new Regex(@"^(?:(?<device>[^:]+):)?(?:<(?<directory>[^>]+)>)?(?<filename>[^:<>]+)$");

        [NotNull]
        [Subr]
        public static ZilObject OPEN([NotNull] Context ctx, [Decl("'\"READ\"")] string mode, [NotNull] string path)
        {
            var result = new ZilFileChannel(ConvertPath(path), FileAccess.Read);
            result.Reset(ctx);
            return result;
        }

        [NotNull]
        static string ConvertPath([NotNull] string retroPath)
        {
            var match = RetroPathRE.Match(retroPath);
            return match.Success ? match.Groups["filename"].Value : retroPath;
        }

        [NotNull]
        [Subr]
        public static ZilObject CLOSE([NotNull] Context ctx, [NotNull] ZilChannel channel)
        {
            channel.Close();
            return channel;
        }

        [NotNull]
        [Subr("FILE-LENGTH")]
        public static ZilObject FILE_LENGTH([NotNull] Context ctx, [NotNull] ZilChannel channel)
        {
            var length = channel.GetFileLength();
            return length == null ? ctx.FALSE : new ZilFix((int)length.Value);
        }

        [NotNull]
        [Subr]
        public static ZilObject READSTRING([NotNull] Context ctx, [NotNull] ZilString dest, ZilChannel channel,
            [CanBeNull] [Decl("<OR FIX STRING>")] ZilObject maxLengthOrStopChars = null)
        {
            // TODO: support 1- and 4-argument forms?

            int maxLength = dest.Text.Length;
            ZilString stopChars = null;

            if (maxLengthOrStopChars != null)
            {
                var maxLengthFix = maxLengthOrStopChars as ZilFix;
                stopChars = maxLengthOrStopChars as ZilString;

                if (maxLengthFix != null)
                    maxLength = Math.Min(maxLengthFix.Value, maxLength);
            }

            var buffer = new StringBuilder(maxLength);
            bool reading;
            do
            {
                reading = false;
                if (buffer.Length < maxLength)
                {
                    var c = channel.ReadChar();
                    if (c != null &&
                        (stopChars == null || stopChars.Text.IndexOf(c.Value) < 0))
                    {
                        buffer.Append(c.Value);
                        reading = true;
                    }
                }
            } while (reading);

            var readCount = buffer.Length;
            buffer.Append(dest.Text.Substring(readCount));
            dest.Text = buffer.ToString();
            return new ZilFix(readCount);
        }

        /// <exception cref="InterpreterError">Not supported by this type of channel.</exception>
        [NotNull]
        [Subr("M-HPOS")]
        public static ZilObject M_HPOS([NotNull] Context ctx, [NotNull] ZilChannel channel)
        {
            if (!(channel is IChannelWithHPos hposChannel))
                throw new InterpreterError(InterpreterMessages._0_Not_Supported_By_This_Type_Of_Channel, "M-HPOS");

            return new ZilFix(hposChannel.HPos);
        }

        /// <exception cref="InterpreterError"><paramref name="position"/> is negative.</exception>
        [NotNull]
        [Subr("INDENT-TO")]
        public static ZilObject INDENT_TO([NotNull] Context ctx, [NotNull] ZilFix position, ZilChannel channel = null)
        {
            if (position.Value < 0)
                throw new InterpreterError(
                    InterpreterMessages._0_Expected_1,
                    "INDENT-TO: arg 1",
                    "a non-negative FIX");

            if (channel == null)
            {
                channel =
                    ctx.GetLocalVal(ctx.GetStdAtom(StdAtom.OUTCHAN)) as ZilChannel ??
                    ctx.GetGlobalVal(ctx.GetStdAtom(StdAtom.OUTCHAN)) as ZilChannel;
                if (channel == null)
                    throw new InterpreterError(InterpreterMessages._0_Bad_OUTCHAN, "INDENT-TO");
            }

            if (!(channel is IChannelWithHPos hposChannel))
                throw new InterpreterError(InterpreterMessages._0_Not_Supported_By_This_Type_Of_Channel, "INDENT-TO");

            var cur = hposChannel.HPos;
            while (cur < position.Value)
            {
                channel.WriteChar(' ');

                var next = hposChannel.HPos;
                if (next <= cur)
                {
                    // didn't move, or wrapped around
                    break;
                }

                cur = next;
            }

            return position;
        }
    }
}
