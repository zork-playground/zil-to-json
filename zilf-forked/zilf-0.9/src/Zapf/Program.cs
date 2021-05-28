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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using JetBrains.Annotations;
using Zapf.Parsing;
using Zilf.Common.StringEncoding;
using Zapf.Parsing.Diagnostics;
using Zapf.Parsing.Directives;
using Zapf.Parsing.Expressions;
using Zapf.Parsing.Instructions;

namespace Zapf
{
    static class Program
    {
        // TODO: Blorb output

        public const byte DEFAULT_ZVERSION = 3;

        public static int Main([ItemNotNull] [NotNull] string[] args)
        {
            var ctx = ParseArgs(args);
            if (ctx == null)
            {
                Usage();
                return 1;
            }

            // show banner
            if (!ctx.Quiet)
                Console.Error.WriteLine(GetBanner());

            // TODO: move all of this logic into ZapfAssembler and use that instead

            // set up for the target version
            ctx.OpcodeDict = MakeOpcodeDict(ctx.ZVersion, ctx.InformMode);
            ctx.Restart();

            try
            {
                // assemble the code
                bool restart;
                do
                {
                    restart = false;
                    try
                    {
                        Assemble(ctx);
                    }
                    catch (RestartException)
                    {
                        if (!ctx.Quiet)
                            Console.Error.WriteLine("\nRestarting");

                        ctx.Restart();
                        restart = true;
                    }
                    catch (FatalError fer)
                    {
                        ctx.HandleFatalError(fer);
                        return 2;
                    }
                } while (restart);

                // list label addresses
                PrintLabelAddresses(ctx);

                // find abbreviations
                FindAndPrintAbbreviations(ctx);

                // report success or failure
                if (ctx.ErrorCount > 0)
                {
                    if (!ctx.Quiet)
                    {
                        Console.Error.WriteLine();
                        Console.Error.WriteLine("Failed ({0} error{1})",
                            ctx.ErrorCount,
                            ctx.ErrorCount == 1 ? "" : "s");
                    }
                    return 2;
                }

                return 0;
            }
            finally
            {
                ctx.CloseOutput();
                ctx.CloseDebugFile();
            }
        }

        [NotNull]
        internal static string GetVersion() =>
            typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                .InformationalVersion ?? "<?.??>";

        [NotNull]
        internal static string GetBanner() => $"ZAPF {GetVersion()}";

        static void FindAndPrintAbbreviations([NotNull] Context ctx)
        {
            if (ctx.AbbreviateMode)
            {
                const int maxAbbrevs = 96;
                Console.Error.WriteLine("Finding up to {0} abbreviations...", maxAbbrevs);

                Console.WriteLine("        ; Frequent words file for {0}", Path.GetFileName(ctx.InFile));
                Console.WriteLine();
                int num = 1, totalSavings = 0;
                foreach (var r in ctx.AbbrevFinder.GetResults(maxAbbrevs))
                {
                    Console.WriteLine("        .FSTR FSTR?{0},\"{1}\"\t\t; {2}x, saved {3}",
                        num++, SanitizeString(r.Text), r.Count, r.Score);
                    totalSavings += r.Score;
                }

                if (num < maxAbbrevs)
                    Console.WriteLine("        .FSTR FSTR?DUMMY,\"\"");
                Console.WriteLine("WORDS::");
                for (int i = 1; i < num; i++)
                    Console.WriteLine("        FSTR?{0}", i);
                for (int i = num; i < maxAbbrevs; i++)
                    Console.WriteLine("        FSTR?DUMMY");

                Console.WriteLine();
                Console.WriteLine("        .ENDI");

                Console.Error.WriteLine("Abbrevs would save {0} z-chars total (~{1} bytes)",
                    totalSavings, totalSavings * 2 / 3);
            }
        }

        static void PrintLabelAddresses([NotNull] Context ctx)
        {
            if (ctx.ListAddresses)
            {
                Console.Error.WriteLine();

                var query = from s in ctx.GlobalSymbols.Values
                            where s.Type == SymbolType.Function || s.Type == SymbolType.Label ||
                                  s.Type == SymbolType.String
                            let addr =
                            (s.Type == SymbolType.Label)
                                ? s.Value
                                : (s.Type == SymbolType.Function)
                                    ? s.Value * ctx.PackingDivisor + ctx.FunctionsOffset
                                    : /* (s.Type == SymbolType.String) ? */ s.Value * ctx.PackingDivisor + ctx.StringsOffset
                            orderby addr
                            select new { s.Name, Address = addr };

                Console.WriteLine("{0,-32} {1,-6} {2,-6}",
                    "Name",
                    "Addr",
                    "Length");

                var entries = query.ToArray();

                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];

                    object dist;
                    if (i < entries.Length - 1)
                        dist = entries[i + 1].Address - entries[i].Address;
                    else
                        dist = "to end";

                    Console.WriteLine("{0,-32} ${1:x5} {2,6}",
                        entry.Name,
                        entry.Address,
                        dist);
                }
            }
        }

        [NotNull]
        static string SanitizeString(string text)
        {
            var sb = new StringBuilder(text);

            for (int i = sb.Length - 1; i >= 0; i--)
                if (sb[i] == '"')
                    sb.Insert(i, '"');

            return sb.ToString();
        }

        [NotNull]
        internal static Dictionary<string, KeyValuePair<ushort, ZOpAttribute>> MakeOpcodeDict(
            int zversion, bool inform)
        {
            var fields = typeof(Opcodes).GetFields(BindingFlags.Static | BindingFlags.Public);
            var result = new Dictionary<string, KeyValuePair<ushort, ZOpAttribute>>(fields.Length);

            int effectiveVersion = zversion;

            if (zversion == 7 || zversion == 8)
                effectiveVersion = 5;

            foreach (var fi in fields)
            {
                var attrs = fi.GetCustomAttributes(typeof(ZOpAttribute), false);
                foreach (ZOpAttribute attr in attrs)
                    if (effectiveVersion >= attr.MinVer && effectiveVersion <= attr.MaxVer)
                    {
                        var pair = new KeyValuePair<ushort, ZOpAttribute>((ushort)fi.GetValue(null), attr);
                        result.Add(inform ? attr.InformName : attr.ClassicName, pair);
                        break;
                    }
            }

            return result;
        }

        [CanBeNull]
        internal static Context ParseArgs([ItemNotNull] [NotNull] IReadOnlyList<string> args)
        {
            var result = new Context();

            for (int i = 0; i < args.Count; i++)
            {
                switch (args[i])
                {
                    case "-ab":
                        result.AbbreviateMode = true;
                        break;

                    case "-q":
                        result.Quiet = true;
                        break;

                    case "-i":
                        result.InformMode = true;
                        break;

                    case "-la":
                        result.ListAddresses = true;
                        break;

                    case "-r":
                        if (++i == args.Count)
                            return null;
                        result.Release = short.Parse(args[i]);
                        break;

                    case "-s":
                        if (++i == args.Count)
                            return null;
                        result.Serial = args[i];
                        break;

                    case "-c":
                        if (++i == args.Count)
                            return null;
                        result.Creator = args[i];
                        break;

                    case "-c0":
                        result.Creator = null;
                        break;

                    case "-dx":
                        result.XmlDebugMode = true;
                        break;

                    case "-?":
                    case "--help":
                    case "/?":
                        return null;

                    default:
                        if (result.InFile == null)
                            result.InFile = args[i];
                        else if (result.OutFile == null)
                            result.OutFile = args[i];
                        else
                            return null;
                        break;
                }
            }

            // validate
            if (result.InFile == null)
                return null;

            if (result.OutFile == null)
                result.OutFile = Path.ChangeExtension(result.InFile, ".z#");

            result.DebugFile = Path.ChangeExtension(result.OutFile, result.XmlDebugMode ? ".dbg.xml" : ".dbg");

            return result;
        }

        static void Usage()
        {
            Console.Error.WriteLine(GetBanner());
            Console.Error.WriteLine(
@"Assemble: zapf [switches] <inFile.zap> [<outFile.z#>]

General switches:
  -i                    use Inform opcode/stack names
  -q                    quiet (no banner)
  -la                   list global label addresses
  -r #                  set release number (this overrides RELEASEID)
  -s ######             set serial number
  -c ####               set creator version
  -ab                   also optimize abbreviations and print ZAPF code
  -dx                   use XML debug format");

        }

        /// <exception cref="FatalError">An <see cref="IOException"/> occurred while reading the input file(s).</exception>
        internal static void Assemble([NotNull] Context ctx)
        {
            List<AsmLine> file;
            ctx.PushFile(ctx.InFile);
            try
            {
                var roots = ReadRootsFromFile(ctx, ctx.InFile);
                file = new List<AsmLine>(ReadAllCode(ctx, roots));
            }
            catch (IOException ex)
            {
                Errors.ThrowFatal(ex.Message);
                // ReSharper disable once HeuristicUnreachableCode
                return; // never gets here
            }

            // first pass: discover label addresses and header flags
            if (!ctx.Quiet)
                Console.Error.Write("Measuring");

            ctx.FinalPass = false;

            do
            {
                ctx.MeasureAgain = false;

                // write dummy header
                ctx.Position = 0;
                if (ctx.ZVersion < 5)
                    WriteHeader(ctx, false);

                for (int i = 0; i < file.Count; i++)
                {
                    try
                    {
                        if (file[i] is EndDirective)
                            break;

                        PassOne(ctx, file[i], ref i);
                    }
                    catch (SeriousError ser)
                    {
                        ctx.HandleSeriousError(ser);
                    }
                }

                ctx.CheckForUndefinedSymbols();

                if (ctx.Fixups.Count > 0)
                    ctx.MeasureAgain = true;

                ctx.CheckLimits();
                ctx.ResetBetweenPasses();

                if (!ctx.Quiet && ctx.MeasureAgain && ctx.ErrorCount == 0)
                    Console.Error.Write('.');
            } while (ctx.MeasureAgain && ctx.ErrorCount == 0);

            if (!ctx.Quiet)
                Console.Error.WriteLine();

            if (ctx.ErrorCount == 0)
            {
                // open output file and write mostly-real header
                ctx.OpenOutput();
                if (ctx.ZVersion < 5)
                {
                    try
                    {
                        WriteHeader(ctx, true);
                    }
                    catch (SeriousError ser)
                    {
                        ctx.HandleSeriousError(ser);
                    }
                }
            }

            // verify packed address labels
            foreach (var sym in ctx.GlobalSymbols.Values)
            {
                if (sym.Value < 65536)
                    continue;

                switch (sym.Type)
                {
                    case SymbolType.Function:
                        Errors.Warn(ctx, null, "packed address overflow for function: {0}", sym.Name);
                        break;

                    case SymbolType.String:
                        Errors.Warn(ctx, null, "packed address overflow for string: {0}", sym.Name);
                        break;
                }
            }

            // stop early if errors detected
            if (ctx.ErrorCount > 0)
                return;

            // second pass: generate object code
            if (!ctx.Quiet)
                Console.Error.WriteLine("Assembling");

            ctx.FinalPass = true;

            for (int i = 0; i < file.Count; i++)
            {
                try
                {
                    if (file[i] is EndDirective)
                        break;

                    PassTwo(ctx, file[i], ref i);
                }
                catch (SeriousError ser)
                {
                    ctx.HandleSeriousError(ser);
                }
            }

            if (ctx.Fixups.Count > 0)
                Errors.Serious(ctx, "unresolved references after final pass");

            // pad if necessary, finalize header length and checksum, close file
            try
            {
                FinalizeOutput(ctx);
            }
            catch (SeriousError ser)
            {
                ctx.HandleSeriousError(ser);
            }
        }

        /// <summary>
        /// Apply the first assembler pass to a node.
        /// </summary>
        /// <remarks>
        /// This pass discovers label addresses and header flags.
        /// It also handles the .NEW directive.
        /// </remarks>
        /// <param name="ctx">The current context.</param>
        /// <param name="node">The node to process.</param>
        /// <param name="nodeIndex">The current node's index. The method may change this
        /// to rewind the source file.</param>
        static void PassOne(Context ctx, [NotNull] AsmLine node, ref int nodeIndex)
        {
            switch (node)
            {
                case NewDirective newd:
                    var versionExpr = newd.Version;
                    int version = versionExpr == null ? 4 : EvalExpr(ctx, versionExpr).Value;
                    if (version < 3 || version > 8)
                        Errors.ThrowFatal("Only Z-machine versions 3-8 are supported");
                    if (version != ctx.ZVersion)
                    {
                        ctx.ZVersion = (byte)version;
                        ctx.OpcodeDict = MakeOpcodeDict(ctx.ZVersion, ctx.InformMode);
                        throw new RestartException();
                    }
                    break;

                case TimeDirective _:
                    if (ctx.ZVersion == 3)
                    {
                        ctx.ZFlags |= 2;
                    }
                    else
                    {
                        Errors.ThrowFatal(".TIME is only supported in Z-machine version 3");
                    }
                    break;

                case SoundDirective _:
                    switch (ctx.ZVersion)
                    {
                        case 3:
                            ctx.ZFlags2 |= 16;
                            break;
                        case 4:
                            ctx.ZFlags2 |= 128;
                            break;
                        default:
                            Errors.ThrowFatal(".SOUND is only supported in Z-machine versions 3-4");
                            break;
                    }
                    break;

                case Instruction inst:
                    HandleInstruction(ctx, inst);
                    break;

                case BareSymbolLine bsl:
                    if (bsl.UsedAsInstruction)
                        Errors.Serious(ctx, node, "unrecognized opcode: " + bsl.Text);
                    else
                        HandleDirective(ctx, node, nodeIndex, false);
                    break;

                case LocalLabel _:
                case GlobalLabel _:
                    HandleLabel(ctx, node, ref nodeIndex);
                    break;

                default:
                    HandleDirective(ctx, node, nodeIndex, false);
                    break;
            }
        }

        static void WriteHeader([NotNull] Context ctx, bool strict)
        {
            ctx.WriteByte(ctx.ZVersion);
            ctx.WriteByte(ctx.ZFlags);
            // release number
            ctx.WriteWord((ushort)ctx.GetHeaderValue("RELEASEID", "ZORKID", false));
            // high memory mark (ENDLOD)
            var endlod = ctx.GetHeaderValue("ENDLOD", strict);
            ctx.WriteWord((ushort)endlod);
            // initial program counter (START)
            var start = ctx.GetHeaderValue("START", strict);
            ctx.WriteWord((ushort)start);
            // pointer to dictionary (VOCAB)
            ctx.WriteWord((ushort)ctx.GetHeaderValue("VOCAB", strict));
            // pointer to objects (OBJECT)
            ctx.WriteWord((ushort)ctx.GetHeaderValue("OBJECT", strict));
            // pointer to global variables (GLOBAL)
            ctx.WriteWord((ushort)ctx.GetHeaderValue("GLOBAL", strict));
            // static memory mark (IMPURE - the memory *below* this point is impure)
            var impure = ctx.GetHeaderValue("IMPURE", strict);
            ctx.WriteWord((ushort)impure);
            // flags 2 word
            ctx.WriteWord(ctx.ZFlags2);
            // serial number (6 bytes)
            ctx.WriteWord(0);   // filled in later
            ctx.WriteWord(0);
            ctx.WriteWord(0);
            // pointer to abbreviations (WORDS)
            ctx.WriteWord((ushort)ctx.GetHeaderValue("WORDS", strict));
            // packed program length
            ctx.WriteWord(0);   // we'll fill this in later
            // checksum
            ctx.WriteWord(0);   // this too

            while (ctx.Position < 64)
                ctx.WriteByte(0);

            // validate header fields
            if (start > 65536)
                Errors.ThrowSerious("START must be in the first 64k (currently {0})", start);
            if (impure > 65536)
                Errors.ThrowSerious("IMPURE must be in the first 64k (currently {0})", impure);
            if (endlod < impure)
                Errors.ThrowSerious("ENDLOD must be after IMPURE");
        }

        [CanBeNull]
        static Symbol GetDebugMapValue([NotNull] Context ctx, [NotNull] string name)
        {
            ctx.GlobalSymbols.TryGetValue(name, out var sym);
            return sym;
        }

        static void FinalizeOutput([NotNull] Context ctx)
        {
            const int MINSIZE = 512;

            // pad file to a minimum size (for the benefit of tools that assume one)
            while (ctx.Position < MINSIZE)
                ctx.WriteByte(0);

            // pad file to a nice round number
            while (ctx.Position % ctx.HeaderLengthDivisor != 0)
                ctx.WriteByte(0);

            // calculate file length
            int length = ctx.Position;

            int maxLength = 0;
            switch (ctx.ZVersion)
            {
                case 1:
                case 2:
                case 3:
                    maxLength = 128;
                    break;
                case 4:
                case 5:
                    maxLength = 256;
                    break;
                case 7:
                    maxLength = 320;
                    break;
                case 6:
                case 8:
                    maxLength = 512;
                    break;
            }

            if (length > maxLength * 1024)
            {
                string protip;
                if (ctx.StringEncoder.AbbreviationCount < 96)
                {
                    protip = " (generating text abbreviations may help; see documentation for the -ab switch)";
                }
                else
                {
                    protip = "";
                }

                Errors.ThrowSerious("file length of {0} exceeds platform limit by {1} bytes{2}",
                    length, length - maxLength * 1024, protip);
            }

            // calculate file checksum
            ctx.Position = 64;
            ushort checksum = 0;

            while (ctx.Position < length)
                checksum += ctx.ReadByte();

            // write Z-code version into header
            ctx.Position = 0;
            ctx.WriteByte(ctx.ZVersion);

            // write release number into header (overriding RELEASEID if set)
            if (ctx.Release != null)
            {
                ctx.Position = 2;
                ctx.WriteWord((ushort)ctx.Release);
            }

            // write serial number into header
            if (ctx.Serial == null)
                ctx.Serial = DateTime.Now.ToString("yyMMdd");
            else if (ctx.Serial.Length != 6)
                ctx.Serial = ctx.Serial.PadRight(6).Substring(0, 6);

            ctx.Position = 0x12;
            foreach (char c in ctx.Serial)
                ctx.WriteByte((byte)c);

            // write length and checksum into header
            ctx.Position = 0x1A;
            ctx.WriteWord((ushort)(length / ctx.HeaderLengthDivisor));
            ctx.WriteWord(checksum);

            // write creator ID
            if (ctx.Creator != null)
            {
                if (ctx.Creator.Length != 4)
                    ctx.Creator = ctx.Creator.PadRight(4).Substring(0, 4);

                ctx.Position = 0x3C;
                foreach (char c in ctx.Creator)
                    ctx.WriteByte((byte)c);
            }

            // done
            if (!ctx.Quiet)
                Console.Error.WriteLine("Wrote {0} bytes to {1}", length, ctx.OutFile);

            // finalize debug file
            if (ctx.IsDebugFileOpen)
            {
                Debug.Assert(ctx.DebugWriter != null);

                // finish map
                if (!ctx.DebugFileMap.ContainsKey(DEBF.AbbrevMapName))
                    ctx.DebugFileMap[DEBF.AbbrevMapName] = GetDebugMapValue(ctx, "WORDS");
                if (!ctx.DebugFileMap.ContainsKey(DEBF.GlobalsMapName))
                    ctx.DebugFileMap[DEBF.GlobalsMapName] = GetDebugMapValue(ctx, "GLOBAL");
                if (!ctx.DebugFileMap.ContainsKey(DEBF.ObjectsMapName))
                {
                    var objTable = GetDebugMapValue(ctx, "OBJECT");
                    if (objTable != null)
                    {
                        var objTree = new Symbol
                        {
                            Type = objTable.Type,
                            Value = objTable.Value + (ctx.ZVersion < 4 ? 31 : 63)
                        };
                        ctx.DebugFileMap[DEBF.ObjectsMapName] = objTree;
                    }
                }
                if (!ctx.DebugFileMap.ContainsKey(DEBF.PropsMapName))
                    ctx.DebugFileMap[DEBF.PropsMapName] = GetDebugMapValue(ctx, "OBJECT");
                if (!ctx.DebugFileMap.ContainsKey(DEBF.VocabMapName))
                    ctx.DebugFileMap[DEBF.VocabMapName] = GetDebugMapValue(ctx, "VOCAB");

                // write map
                ctx.DebugWriter.WriteMap(
                    ctx.DebugFileMap.Select(
                        p => new KeyValuePair<string, int>(p.Key, p.Value.Value)));

                // write header
                ctx.DebugWriter.WriteHeader(ctx.GetHeader());

                // done
                ctx.CloseDebugFile();

                if (!ctx.Quiet)
                    Console.Error.WriteLine("Wrote debugging info to {0}", ctx.DebugFile);
            }

            ctx.CloseOutput();
        }

        /// <summary>
        /// Apply the second assembler pass to a node.
        /// </summary>
        /// <remarks>
        /// This pass generates code.
        /// </remarks>
        /// <param name="ctx">The current context.</param>
        /// <param name="node">The node to process.</param>
        /// <param name="nodeIndex">The current node's index. The method may change this
        /// to rewind the source file.</param>
        static void PassTwo(Context ctx, [NotNull] AsmLine node, ref int nodeIndex)
        {
            switch (node)
            {
                case Instruction inst:
                    HandleInstruction(ctx, inst);
                    break;

                case LocalLabel _:
                case GlobalLabel _:
                    HandleLabel(ctx, node, ref nodeIndex);
                    break;

                default:
                    HandleDirective(ctx, node, nodeIndex, true);
                    break;
            }
        }

        static IEnumerable<AsmLine> ReadAllCode(Context ctx, [NotNull] IEnumerable<AsmLine> roots)
        {
            foreach (var node in roots)
            {
                if (node is InsertDirective insert)
                {
                    string arg = insert.InsertFileName;
                    var insertedFile = ctx.FindInsertedFile(arg);

                    if (insertedFile == null)
                        Errors.ThrowFatal(node, "inserted file not found: {0}", arg);

                    ctx.PushFile(insertedFile);
                    try
                    {
                        var insertedRoots = ReadRootsFromFile(ctx, insertedFile);
                        foreach (var insertedNode in ReadAllCode(ctx, insertedRoots))
                            if (insertedNode is EndiDirective || insertedNode is EndDirective)
                                break;
                            else
                                yield return insertedNode;
                    }
                    finally
                    {
                        ctx.PopFile();
                    }
                }
                else
                {
                    yield return node;
                }
            }
        }

        static IEnumerable<AsmLine> ReadRootsFromFile([NotNull] Context ctx, [NotNull] string path)
        {
            using (var stream = ctx.OpenFile(path, false))
            {
                Debug.Assert(ctx.OpcodeDict != null);
                var parser = new ZapParser(ctx, ctx.OpcodeDict);
                var result = parser.Parse(stream, path);

                if (result.NumberOfSyntaxErrors > 0)
                    Errors.ThrowFatal("syntax error");

                return result.Lines;
            }
        }

        static Symbol EvalExpr(Context ctx, [NotNull] AsmExpr node)
        {
            switch (node)
            {
                case NumericLiteral num:
                    return new Symbol(num.Value);

                case SymbolExpr sym:
                    if (!ctx.LocalSymbols.TryGetValue(sym.Text, out var result) &&
                        !ctx.GlobalSymbols.TryGetValue(sym.Text, out result))
                    {
                        if (ctx.FinalPass)
                        {
                            Errors.ThrowFatal(node, "undefined symbol: {0}", sym.Text);
                        }
                        else
                        {
                            result = new Symbol(sym.Text, SymbolType.Unknown, 0);
                            ctx.GlobalSymbols.Add(sym.Text, result);
                        }
                    }

                    return result;

                case AdditionExpr add:
                    var left = EvalExpr(ctx, add.Left);
                    var right = EvalExpr(ctx, add.Right);
                    if (!ctx.FinalPass &&
                        (left.Type == SymbolType.Unknown || right.Type == SymbolType.Unknown))
                    {
                        return new Symbol(null, SymbolType.Unknown, 0);
                    }
                    if (Addable(left.Type) && Addable(right.Type))
                        return new Symbol(null, SymbolType.Constant, left.Value + right.Value);
                    throw new NotImplementedException($"Unimplemented symbol addition: {left.Type} + {right.Type}");

                    // we can add numbers and non-packed addresses
                    bool Addable(SymbolType type)
                    {
                        switch (type)
                        {
                            case SymbolType.Constant:
                            case SymbolType.Label:
                            case SymbolType.Object:
                                return true;

                            default:
                                return false;
                        }
                    }

                default:
                    throw new NotImplementedException();
            }
        }

        static void EvalOperand(Context ctx, AsmExpr node, out byte type, out ushort value, [CanBeNull] out Fixup fixup)
        {
            EvalOperand(ctx, node, out type, out value, out fixup, false);
        }

        static void EvalOperand(Context ctx, AsmExpr node, out byte type, out ushort value, [CanBeNull] out Fixup fixup,
            bool allowLocalLabel)
        {
            fixup = null;

            bool apos = false;
            if (node is QuoteExpr quote)
            {
                apos = true;
                node = quote.Inner;
            }

            if (node is SymbolExpr symExpr)
            {
                var text = symExpr.Text;
                if (ctx.LocalSymbols.TryGetValue(text, out var sym))
                {
                    if (sym.Type == SymbolType.Label)
                    {
                        if (!allowLocalLabel)
                            Errors.Serious(ctx, node, "local label used as operand");

                        // note: returned value is relative to the current position,
                        // which is probably not where the value will be written.
                        // caller must correct it.
                        type = OPERAND_WORD;
                        value = (ushort)(sym.Value - ctx.Position);
                    }
                    else
                    {
                        Debug.Assert(sym.Type == SymbolType.Variable);
                        type = OPERAND_VAR;
                        value = (byte)sym.Value;
                    }
                }
                else if (ctx.GlobalSymbols.TryGetValue(text, out sym))
                {
                    if (sym.Type == SymbolType.Variable)
                        type = OPERAND_VAR;
                    else if ((sym.Value & 0xff) == sym.Value)
                        type = OPERAND_BYTE;
                    else
                        type = OPERAND_WORD;
                    value = (ushort)sym.Value;
                }
                else if (ctx.FinalPass && !allowLocalLabel)
                {
                    Errors.ThrowFatal(node, "undefined symbol: {0}", text);
                    // ReSharper disable once HeuristicUnreachableCode
                    type = 0;
                    value = 0;
                }
                else
                {
                    // not defined yet
                    type = OPERAND_BYTE;
                    value = 0;
                    if (allowLocalLabel)
                        ctx.MarkUnknownBranch(text);
                    else
                        fixup = new Fixup(text);
                }
            }
            else
            {
                var sym = EvalExpr(ctx, node);
                value = (ushort)sym.Value;
                type = value < 256 ? OPERAND_BYTE : OPERAND_WORD;
            }

            if (apos)
                type = OPERAND_BYTE;
        }

        static bool IsLongConstant(Context ctx, [NotNull] AsmExpr node)
        {
            switch (node)
            {
                case NumericLiteral num:
                    return ((uint)num.Value & 0xffffff00) != 0;

                case SymbolExpr symExpr:
                    if (ctx.LocalSymbols.TryGetValue(symExpr.Text, out _))
                    {
                        // the only legal local symbol operand is a local variable
                        return false;
                    }
                    else if (ctx.GlobalSymbols.TryGetValue(symExpr.Text, out var sym))
                    {
                        return sym.Value < 0 || sym.Value > 255;
                    }
                    else
                    {
                        // not defined yet, assume it's a faraway global label
                        // (could also be a local label if we're assembling a JUMP instruction,
                        // but that's assembled as a word anyway)
                        return true;
                    }

                case AdditionExpr add:
                    if (IsLongConstant(ctx, add.Left) || IsLongConstant(ctx, add.Right))
                        return true;
                    var left = EvalExpr(ctx, add.Left);
                    var right = EvalExpr(ctx, add.Right);
                    return ((uint)(left.Value + right.Value) & 0xffffff00) != 0;

                case StringLiteral _:
                case QuoteExpr _:
                    return false;

                default:
                    throw new ArgumentException("Unexpected expr type: " + node.GetType().Name, nameof(node));
            }
        }

        static void HandleDirective(Context ctx, [NotNull] AsmLine node, int nodeIndex, bool assembling)
        {
            // local scope is terminated by any directive except .DEBUG_LINE (not counting labels)
            if (!(node is DebugLineDirective))
                ctx.EndReassemblyScope(nodeIndex);

            switch (node)
            {
                case NullDirective _:
                    // nada
                    break;

                case NewDirective _:
                case TimeDirective _:
                case SoundDirective _:
                    // these are explicitly handled by PassOne or PassTwo
                    break;

                case LangDirective langNode:
                    ctx.SetLanguage(EvalExpr(ctx, langNode.LanguageId).Value, EvalExpr(ctx, langNode.EscapeChar).Value);
                    break;

                case ChrsetDirective chrsetNode:
                    if (ctx.ZVersion >= 5)
                    {
                        int charsetNum = EvalExpr(ctx, chrsetNode.CharsetNum).Value;

                        switch (charsetNum)
                        {
                            case 0:
                            case 1:
                            case 2:
                                ctx.StringEncoder.SetCharset(
                                    charsetNum,
                                    chrsetNode.Characters.Select(e => (byte)EvalExpr(ctx, e).Value));
                                break;

                            default:
                                Errors.ThrowFatal("no such character set");
                                break;
                        }
                    }
                    else
                    {
                        Errors.ThrowFatal(".CHRSET is only supported in Z-machine versions 5-8");
                    }
                    break;

                case FunctDirective functNode:
                    BeginFunction(ctx, functNode, nodeIndex);
                    break;

                case AlignDirective alignNode:
                    var divisor = EvalExpr(ctx, alignNode.Divisor);
                    if (divisor.Type != SymbolType.Constant)
                    {
                        Errors.ThrowSerious(alignNode.Divisor, "non-constant argument to .ALIGN");
                    }
                    AlignUnpacked(ctx, divisor.Value);
                    break;

                case TableDirective tableNode:
                    if (ctx.TableStart != null)
                    {
                        Errors.Warn(ctx, node, "starting new table before ending old table");
                    }
                    ctx.TableStart = ctx.Position;
                    ctx.TableSize = null;
                    if (tableNode.Size != null)
                    {
                        var sym = EvalExpr(ctx, tableNode.Size);
                        if (sym.Type != SymbolType.Constant)
                            Errors.Warn(ctx, node, "ignoring non-constant table size specifier");
                        else
                            ctx.TableSize = sym.Value;
                    }
                    break;

                case EndtDirective _:
                    if (ctx.TableStart == null)
                    {
                        Errors.Warn(ctx, node, "ignoring .ENDT outside of a table definition");
                    }
                    else if (ctx.TableSize != null)
                    {
                        if (ctx.Position - ctx.TableStart.Value != ctx.TableSize.Value)
                            Errors.Warn(ctx, node, "incorrect table size: expected {0}, actual {1}",
                                ctx.TableSize.Value,
                                ctx.Position - ctx.TableStart.Value);
                    }
                    ctx.TableStart = null;
                    ctx.TableSize = null;
                    break;

                case VocbegDirective vocbegNode:
                    if (ctx.InVocab)
                    {
                        Errors.Warn(ctx, node, "ignoring .VOCBEG inside another vocabulary block");
                    }
                    else
                    {
                        var sym1 = EvalExpr(ctx, vocbegNode.RecordSize);
                        var sym2 = EvalExpr(ctx, vocbegNode.KeySize);
                        if (sym1.Type != SymbolType.Constant || sym2.Type != SymbolType.Constant)
                            Errors.Warn(ctx, node, "ignoring .VOCBEG with non-constant size specifiers");
                        else
                            ctx.EnterVocab(sym1.Value, sym2.Value);
                    }
                    break;

                case VocendDirective _:
                    if (!ctx.InVocab)
                        Errors.Warn(ctx, node, "ignoring .VOCEND outside of a vocabulary block");
                    else
                        ctx.LeaveVocab(node);
                    break;

                case DataDirective dataNode:
                    var elements = dataNode.Elements;
                    foreach (var expr in elements)
                    {
                        var sym = EvalExpr(ctx, expr);
                        if (sym.Type == SymbolType.Unknown && ctx.FinalPass)
                        {
                            Errors.ThrowFatal(expr, "unrecognized symbol: " + ((SymbolExpr)expr).Text);
                        }
                        if (node is ByteDirective)
                        {
                            if (sym.Type == SymbolType.Label && ctx.InVocab)
                            {
                                Errors.ThrowFatal(expr, "global label refs inside vocab section must be assembled as words");
                            }

                            ctx.WriteByte((byte)sym.Value);
                        }
                        else
                        {
                            if (sym.Type == SymbolType.Label && ctx.InVocab)
                            {
                                // global labels inside the vocab table need to be fixed up at .VOCEND,
                                // since they may refer to other vocab words
                                var fixup = new Fixup(sym.Name) { Location = ctx.Position };
                                ctx.Fixups.Add(fixup);
                            }

                            ctx.WriteWord((ushort)sym.Value);
                        }
                    }
                    break;

                case FstrDirective fstrNode:
                    AddAbbreviation(ctx, fstrNode);
                    break;

                case GstrDirective fstrNode:
                    PackString(ctx, fstrNode);
                    break;

                case StrDirective strNode:
                    ctx.WriteZString(strNode.Text, false);
                    break;

                case StrlDirective strlNode:
                    ctx.WriteZString(strlNode.Text, true);
                    break;

                case LenDirective lenNode:
                    ctx.WriteZStringLength(lenNode.Text);
                    break;

                case ZwordDirective zwordNode:
                    ctx.WriteZWord(zwordNode.Text);
                    break;

                case EqualsDirective equalsNode:
                    var rvalue = EvalExpr(ctx, equalsNode.Right);
                    if (rvalue.Type == SymbolType.Unknown && ctx.FinalPass)
                    {
                        Errors.ThrowFatal(equalsNode.Right, "unrecognized symbol");
                    }
                    else
                    {
                        ctx.GlobalSymbols[equalsNode.Left] = rvalue;
                    }
                    break;

                case GvarDirective gvarNode:
                    ctx.AddGlobalVar(gvarNode.Name);
                    if (gvarNode.InitialValue != null)
                    {
                        var sym = EvalExpr(ctx, gvarNode.InitialValue);
                        if (sym.Type == SymbolType.Unknown && ctx.FinalPass)
                        {
                            Errors.ThrowFatal(gvarNode.InitialValue, "unrecognized symbol");
                        }
                        ctx.WriteWord((ushort)sym.Value);
                    }
                    else
                    {
                        ctx.WriteWord(0);
                    }
                    break;

                case ObjectDirective od:
                    ctx.AddObject(od.Name);
                    const string ObjectVersionError = "wrong .OBJECT syntax for this version";
                    if (ctx.ZVersion < 4)
                    {
                        // 2 flag words
                        ctx.WriteWord(EvalExpr(ctx, od.Flags1));
                        ctx.WriteWord(EvalExpr(ctx, od.Flags2));
                        if (od.Flags3 != null)
                        {
                            Errors.Serious(ctx, node, ObjectVersionError);
                        }
                        // 3 object link bytes
                        ctx.WriteByte(EvalExpr(ctx, od.Parent));
                        ctx.WriteByte(EvalExpr(ctx, od.Sibling));
                        ctx.WriteByte(EvalExpr(ctx, od.Child));
                        // property table
                        ctx.WriteWord(EvalExpr(ctx, od.PropTable));
                    }
                    else
                    {
                        // 3 flag words
                        ctx.WriteWord(EvalExpr(ctx, od.Flags1));
                        ctx.WriteWord(EvalExpr(ctx, od.Flags2));
                        if (od.Flags3 == null)
                        {
                            Errors.Serious(ctx, node, ObjectVersionError);
                            ctx.WriteWord(0);
                        }
                        else
                        {
                            ctx.WriteWord(EvalExpr(ctx, od.Flags3));
                        }
                        // 3 object link words
                        ctx.WriteWord(EvalExpr(ctx, od.Parent));
                        ctx.WriteWord(EvalExpr(ctx, od.Sibling));
                        ctx.WriteWord(EvalExpr(ctx, od.Child));
                        // property table
                        ctx.WriteWord(EvalExpr(ctx, od.PropTable));
                    }
                    break;

                case PropDirective propNode:
                    var size = EvalExpr(ctx, propNode.Size);
                    var prop = EvalExpr(ctx, propNode.Prop);
                    if (ctx.FinalPass &&
                        (size.Type != SymbolType.Constant || prop.Type != SymbolType.Constant))
                    {
                        Errors.Serious(ctx, node, "non-constant arguments to .PROP");
                    }
                    if (ctx.ZVersion < 4)
                    {
                        if (size.Value > 8)
                            Errors.Serious(ctx, node, "property too long (8 bytes max in V3)");
                        ctx.WriteByte((byte)(32 * (size.Value - 1) + prop.Value));
                    }
                    else if (size.Value > 2)
                    {
                        if (size.Value > 64)
                            Errors.Serious(ctx, node, "property too long (64 bytes max in V4+)");
                        ctx.WriteByte((byte)(prop.Value | 128));
                        ctx.WriteByte((byte)(size.Value | 128));
                    }
                    else
                    {
                        var b = (byte)prop.Value;
                        if (size.Value == 2)
                            b |= 64;
                        ctx.WriteByte(b);
                    }
                    break;

                case DebugDirective debugNode:
                    if (assembling)
                    {
                        if (!ctx.IsDebugFileOpen)
                            ctx.OpenDebugFile();

                        HandleDebugDirective(ctx, debugNode);
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        static void HandleDebugDirective([NotNull] Context ctx, [NotNull] DebugDirective node)
        {
            Debug.Assert(ctx.DebugWriter != null);

            if (ctx.DebugWriter.InRoutine &&
                !(node is DebugLineDirective || node is DebugRoutineEndDirective))
            {
                Errors.Serious(ctx, node, "debug directives other than .DEBUG-LINE not allowed inside routines");
                return;
            }

            LineRef MakeLineRef(AsmExpr file, AsmExpr line, AsmExpr column)
            {
                return new LineRef(
                    file: (byte)EvalExpr(ctx, file).Value,
                    line: (ushort)EvalExpr(ctx, line).Value,
                    col: (byte)EvalExpr(ctx, column).Value);
            }

            switch (node)
            {
                case DebugActionDirective dact:
                    ctx.DebugWriter.WriteAction(
                        (ushort)EvalExpr(ctx, dact.Number).Value,
                        dact.Name);
                    break;

                case DebugArrayDirective darr:
                    if (ctx.GlobalSymbols.TryGetValue("GLOBAL", out var sym1) == false)
                    {
                        Errors.Serious(ctx, node, "define GLOBAL before using .DEBUG-ARRAY");
                        return;
                    }
                    var sym2 = EvalExpr(ctx, darr.Number);
                    ctx.DebugWriter.WriteArray(
                        (ushort)(sym2.Value - sym1.Value),
                        darr.Name);
                    break;

                case DebugAttrDirective dattr:
                    ctx.DebugWriter.WriteAttr(
                        (ushort)EvalExpr(ctx, dattr.Number).Value,
                        dattr.Name);
                    break;

                case DebugClassDirective dclass:
                    ctx.DebugWriter.WriteClass(
                        dclass.Name,
                        MakeLineRef(dclass.StartFile, dclass.StartLine, dclass.StartColumn),
                        MakeLineRef(dclass.EndFile, dclass.EndLine, dclass.EndColumn));
                    break;

                case DebugFakeActionDirective dfake:
                    ctx.DebugWriter.WriteFakeAction(
                        (ushort)EvalExpr(ctx, dfake.Number).Value,
                        dfake.Name);
                    break;

                case DebugFileDirective dfile:
                    ctx.DebugWriter.WriteFile(
                        (byte)EvalExpr(ctx, dfile.Number).Value,
                        dfile.IncludeName,
                        dfile.ActualName);
                    break;

                case DebugGlobalDirective dglob:
                    ctx.DebugWriter.WriteGlobal(
                        (byte)(EvalExpr(ctx, dglob.Number).Value - 16),
                        dglob.Name);
                    break;

                case DebugLineDirective dline:
                    if (!ctx.DebugWriter.InRoutine)
                    {
                        Errors.Serious(ctx, node, ".DEBUG-LINE outside of .DEBUG-ROUTINE");
                    }
                    else
                    {
                        ctx.DebugWriter.WriteLine(
                            MakeLineRef(dline.TheFile, dline.TheLine, dline.TheColumn),
                            ctx.Position);
                    }
                    break;

                case DebugMapDirective dmap:
                    ctx.DebugFileMap[dmap.Key] =
                        dmap.Value != null ? EvalExpr(ctx, dmap.Value) : new Symbol(ctx.Position);
                    break;

                case DebugObjectDirective dobj:
                    ctx.DebugWriter.WriteObject(
                        (ushort)EvalExpr(ctx, dobj.Number).Value,
                        ((DebugObjectDirective)node).Name,
                        MakeLineRef(dobj.StartFile, dobj.StartLine, dobj.StartColumn),
                        MakeLineRef(dobj.EndFile, dobj.EndLine, dobj.EndColumn));
                    break;

                case DebugPropDirective dprop:
                    ctx.DebugWriter.WriteProp(
                        (ushort)EvalExpr(ctx, dprop.Number).Value,
                        dprop.Name);
                    break;

                case DebugRoutineDirective drtn:
                    AlignRoutine(ctx);
                    ctx.DebugWriter.StartRoutine(
                        MakeLineRef(drtn.TheFile, drtn.TheLine, drtn.TheColumn),
                        ctx.Position,
                        drtn.Name,
                        drtn.Locals);
                    break;

                case DebugRoutineEndDirective drend:
                    if (!ctx.DebugWriter.InRoutine)
                    {
                        Errors.Serious(ctx, node, ".DEBUG-ROUTINE-END outside of .DEBUG-ROUTINE");
                    }
                    else
                    {
                        ctx.DebugWriter.EndRoutine(
                            MakeLineRef(drend.TheFile, drend.TheLine, drend.TheColumn),
                            ctx.Position);
                    }
                    break;
            }
        }

        static void BeginFunction([NotNull] [ProvidesContext] Context ctx, [NotNull] FunctDirective node, int nodeIndex)
        {
            var localNames = new List<string>();
            var localValues = new List<ushort>();
            bool gotDefaultValues = false;

            string name = node.Name;
            foreach (var local in node.Locals)
            {
                if (ctx.LocalSymbols.ContainsKey(local.Name))
                {
                    Errors.ThrowSerious(node, "duplicate local: {0}", local.Name);
                }

                localNames.Add(local.Name);
                ctx.LocalSymbols.Add(local.Name,
                    new Symbol(local.Name, SymbolType.Variable, localNames.Count));

                if (local.DefaultValue != null)
                {
                    gotDefaultValues = true;

                    var defv = EvalExpr(ctx, local.DefaultValue);
                    localValues.Add((ushort)defv.Value);
                }
                else
                {
                    localValues.Add(0);
                }
            }

            if (localNames.Count > 15)
                Errors.ThrowSerious(node, "too many local variables");

            AlignRoutine(ctx);

            int paddr = (ctx.Position - ctx.FunctionsOffset) / ctx.PackingDivisor;
            if (ctx.GlobalSymbols.TryGetValue(name, out var sym) == false)
            {
                sym = new Symbol(name, SymbolType.Function, paddr);
                ctx.GlobalSymbols.Add(name, sym);
            }
            else if (sym.Type != SymbolType.Unknown && (!sym.Phantom || sym.Type != SymbolType.Function))
            {
                Errors.ThrowSerious("function redefined: " + name);
            }
            else
            {
                sym.Type = SymbolType.Function;
                sym.Phantom = false;
                if (sym.Value != paddr)
                {
                    sym.Value = paddr;
                    ctx.MeasureAgain = true;
                }
            }

            ctx.BeginReassemblyScope(nodeIndex, sym);

            ctx.WriteByte((byte)localNames.Count);

            if (ctx.ZVersion < 5)
            {
                foreach (ushort val in localValues)
                    ctx.WriteWord(val);
            }
            else if (gotDefaultValues)
            {
                Errors.Warn(ctx, node, "ignoring default local variable values");
            }
        }

        static void AlignUnpacked([NotNull] Context ctx, int divisor)
        {
            while (ctx.Position % divisor != 0)
                ctx.WriteByte(0);
        }

        static void AlignPacked([NotNull] Context ctx, ref int offset)
        {
            if (ctx.UsePackingOffsets && offset == 0)
            {
                // offset has to be a multiple of 8, and at least 8 bytes before the current position so its packed address is nonzero
                while (ctx.Position % ctx.PackingOffsetDivisor != 0 || ctx.Position < ctx.PackingOffsetDivisor)
                    ctx.WriteByte(0);

                offset = ctx.Position - ctx.PackingOffsetDivisor;
            }

            while ((ctx.Position - offset) % ctx.PackingDivisor != 0)
                ctx.WriteByte(0);
        }

        static void AlignRoutine([NotNull] Context ctx)
        {
            AlignPacked(ctx, ref ctx.FunctionsOffset);
        }

        static void AlignString([NotNull] Context ctx)
        {
            AlignPacked(ctx, ref ctx.StringsOffset);
        }

        static void PackString([NotNull] Context ctx, [NotNull] GstrDirective node)
        {
            string name = node.Name;

            AlignString(ctx);

            int paddr = (ctx.Position - ctx.StringsOffset) / ctx.PackingDivisor;
            if (ctx.GlobalSymbols.TryGetValue(name, out var sym) == false)
            {
                sym = new Symbol(name, SymbolType.String, paddr);
                ctx.GlobalSymbols.Add(name, sym);
            }
            else if (sym.Type != SymbolType.Unknown && (!sym.Phantom || sym.Type != SymbolType.String))
            {
                Errors.ThrowSerious("string redefined: " + name);
            }
            else
            {
                sym.Type = SymbolType.String;
                sym.Phantom = false;
                if (sym.Value != paddr)
                {
                    sym.Value = paddr;
                    ctx.MeasureAgain = true;
                }
            }

            ctx.WriteZString(node.Text, false);
        }

        static void AddAbbreviation([NotNull] Context ctx, [NotNull] FstrDirective node)
        {
            if (ctx.StringEncoder.Frozen)
                Errors.ThrowSerious(node, "abbreviations must be defined before strings");

            string name = node.Name;

            if (ctx.Position % 2 != 0)
                ctx.WriteByte(0);

            ctx.GlobalSymbols[name] = new Symbol(name, SymbolType.Constant, ctx.Position / 2);

            string text = node.Text;
            ctx.WriteZString(text, false, StringEncoderMode.NoAbbreviations);

            if (text.Length > 0)
                ctx.StringEncoder.AddAbbreviation(text);
        }

        const byte OPERAND_WORD = 0;
        const byte OPERAND_BYTE = 1;
        const byte OPERAND_VAR = 2;
        const byte OPERAND_OMITTED = 3;

        static readonly byte[] tmpOperandTypes = new byte[8];
        static readonly ushort[] tmpOperandValues = new ushort[8];
        static readonly Fixup[] tmpOperandFixups = new Fixup[8];

        static void HandleInstruction([NotNull] Context ctx, [NotNull] Instruction node)
        {
            Debug.Assert(ctx.OpcodeDict != null);
            var pair = ctx.OpcodeDict[node.Name];
            ushort opcode = pair.Key;
            var attr = pair.Value;
            int operandCount = node.Operands.Count;

            // substitute the opcode number if it has more than 4 operands
            if (attr.WhenExtra != null && operandCount > 4)
            {
                pair = ctx.OpcodeDict[attr.WhenExtra];
                opcode = pair.Key;
                attr = pair.Value;
            }

            // force a 2OP instruction to EXT mode if it uses long constants
            // or has more than 2 operands
            if (opcode < 128 && (operandCount > 2 || node.Operands.Any(o => IsLongConstant(ctx, o))))
                opcode += 192;

            if (opcode < 128)
            {
                // 2OP
                if (operandCount != 2)
                    Errors.ThrowSerious(node, "expected 2 operands");

                var b = (byte)opcode;
                EvalOperand(ctx, node.Operands[0], out tmpOperandTypes[0], out tmpOperandValues[0], out tmpOperandFixups[0]);
                EvalOperand(ctx, node.Operands[1], out tmpOperandTypes[1], out tmpOperandValues[1], out tmpOperandFixups[1]);
                Debug.Assert(tmpOperandFixups[0] == null);
                Debug.Assert(tmpOperandFixups[1] == null);
                Debug.Assert(tmpOperandTypes[0] != OPERAND_WORD);
                Debug.Assert(tmpOperandTypes[1] != OPERAND_WORD);

                if (tmpOperandTypes[0] == OPERAND_VAR)
                    b |= 0x40;
                if (tmpOperandTypes[1] == OPERAND_VAR)
                    b |= 0x20;

                ctx.WriteByte(b);
                ctx.WriteByte((byte)tmpOperandValues[0]);
                ctx.WriteByte((byte)tmpOperandValues[1]);
            }
            else if (opcode < 176)
            {
                // 1OP
                if (operandCount != 1)
                    Errors.ThrowSerious(node, "expected 1 operand");

                var b = (byte)opcode;
                EvalOperand(ctx, node.Operands[0], out tmpOperandTypes[0], out tmpOperandValues[0], out tmpOperandFixups[0],
                    (attr.Flags & ZOpFlags.Label) != 0);

                if ((attr.Flags & ZOpFlags.Label) != 0)
                {
                    // correct label offset (-3 for the opcode and operand, +2 for the normal jump bias)
                    tmpOperandValues[0]--;
                }

                b |= (byte)(tmpOperandTypes[0] << 4);

                ctx.WriteByte(b);
                if (tmpOperandFixups[0] != null)
                {
                    tmpOperandFixups[0].Location = ctx.Position;
                    ctx.Fixups.Add(tmpOperandFixups[0]);
                    tmpOperandFixups[0] = null;
                }
                if (tmpOperandTypes[0] == OPERAND_WORD)
                    ctx.WriteWord(tmpOperandValues[0]);
                else
                    ctx.WriteByte((byte)tmpOperandValues[0]);
            }
            else if (opcode < 192)
            {
                // 0OP
                if ((attr.Flags & ZOpFlags.String) == 0)
                {
                    if (operandCount != 0)
                        Errors.ThrowSerious(node, "expected 0 operands");

                    ctx.WriteByte((byte)opcode);
                }
                else
                {
                    if (operandCount != 1)
                        Errors.ThrowSerious(node, "expected literal string as only operand");

                    ctx.WriteByte((byte)opcode);
                    ctx.WriteZString(((StringLiteral)node.Operands[0]).Text, false);
                }
            }
            else
            {
                // EXT
                int maxArgs = ((attr.Flags & ZOpFlags.Extra) == 0) ? 4 : 8;

                if (operandCount > maxArgs)
                    Errors.ThrowSerious(node, "expected 0-{0} operands", maxArgs);

                if (opcode >= 256)
                {
                    ctx.WriteByte(190);
                    ctx.WriteByte((byte)(opcode - 256));
                }
                else
                    ctx.WriteByte((byte)opcode);

                // operand types
                byte typeByte = 0;
                for (int i = 0; i < maxArgs; i++)
                {
                    byte t;
                    if (i < operandCount)
                    {
                        EvalOperand(ctx, node.Operands[i], out t, out tmpOperandValues[i], out tmpOperandFixups[i]);
                        tmpOperandTypes[i] = t;
                    }
                    else
                        t = OPERAND_OMITTED;

                    typeByte |= (byte)(t << 6 - i % 4 * 2);

                    if (i % 4 == 3)
                    {
                        ctx.WriteByte(typeByte);
                        typeByte = 0;
                    }
                }

                // operands
                for (int i = 0; i < operandCount; i++)
                {
                    if (tmpOperandFixups[i] != null)
                    {
                        tmpOperandFixups[i].Location = ctx.Position;
                        ctx.Fixups.Add(tmpOperandFixups[i]);
                        tmpOperandFixups[i] = null;
                    }

                    if (tmpOperandTypes[i] == OPERAND_WORD)
                        ctx.WriteWord(tmpOperandValues[i]);
                    else
                        ctx.WriteByte((byte)tmpOperandValues[i]);
                }
            }

            if ((attr.Flags & ZOpFlags.Store) != 0)
            {
                if (node.StoreTarget == null)
                {
                    // default to stack
                    ctx.WriteByte(0);
                }
                else
                {
                    if (ctx.LocalSymbols.TryGetValue(node.StoreTarget, out var sym) == false &&
                        ctx.GlobalSymbols.TryGetValue(node.StoreTarget, out sym) == false ||
                        sym.Type != SymbolType.Variable)
                    {
                        Errors.ThrowSerious(node, "expected local or global variable as store target");
                    }

                    ctx.WriteByte((byte)sym.Value);
                }
            }

            if ((attr.Flags & ZOpFlags.Branch) != 0)
            {
                if (node.BranchTarget == null)
                {
                    Errors.ThrowSerious(node, "expected branch target");
                }
                else
                {
                    Debug.Assert(node.BranchPolarity != null);
                    bool polarity = (bool)node.BranchPolarity, far = false;
                    int offset;
                    switch (node.BranchTarget)
                    {
                        case Instruction.BranchTrue:
                            offset = 1;
                            break;
                        case Instruction.BranchFalse:
                            offset = 0;
                            break;
                        default:
                            if (ctx.LocalSymbols.TryGetValue(node.BranchTarget, out var sym))
                            {
                                offset = sym.Value - (ctx.Position + 1) + 2;
                                if (offset < 2 || offset > 63)
                                {
                                    far = true;
                                    offset--;
                                }
                            }
                            else
                            {
                                // not yet defined (we'll reassemble this later once it's defined)
                                offset = 2;
                                ctx.MarkUnknownBranch(node.BranchTarget);
                            }
                            break;
                    }

                    if (offset < -8192 || offset > 8191)
                        Errors.Serious(ctx, node, "branch target is too far away");

                    if (far)
                        ctx.WriteWord((ushort)((polarity ? 0x8000 : 0) | (offset & 0x3fff)));
                    else
                        ctx.WriteByte((byte)((polarity ? 0xc0 : 0x40) | (offset & 0x3f)));
                }
            }
        }

        static void HandleLabel([NotNull] Context ctx, [NotNull] AsmLine node, ref int nodeIndex)
        {
            string name;

            switch (node)
            {
                case GlobalLabel globalNode:
                    name = globalNode.Name;
                    if (ctx.GlobalSymbols.TryGetValue(name, out var sym))
                    {
                        // we don't require it to be a phantom because a global label might be
                        // defined inside a routine, and reassembly could cause it to be defined twice
                        if (sym.Type != SymbolType.Label && sym.Type != SymbolType.Unknown /*|| !sym.Phantom*/)
                            Errors.ThrowSerious(node, "redefining global label");

                        if (ctx.InVocab)
                        {
                            if (!ctx.AtVocabRecord)
                                Errors.ThrowSerious(node, "unaligned global label in vocab section");

                            sym.Value = ctx.Position;
                        }
                        else if (sym.Value != ctx.Position)
                        {
                            var expected = sym.Value;

                            void CheckMismatch()
                            {
                                if (sym.Value == expected)
                                    return;

                                if (ctx.FinalPass)
                                    Errors.ThrowFatal(node, "global label {0} seems to have moved: was {1}, now {2}",
                                        name, expected, sym.Value);

                                ctx.MeasureAgain = true;
                            }

                            sym.Value = ctx.Position;

                            if (ctx.InReassemblyScope)
                            {
                                // don't panic yet, since the value might be correct after reassembly.
                                // just save the expected value and check it at the end of the reassembly scope.
                                ctx.DeferGlobalLabelStabilityCheck(sym, CheckMismatch);
                            }
                            else
                            {
                                CheckMismatch();
                            }
                        }

                        sym.Type = SymbolType.Label;
                        sym.Phantom = false;
                    }
                    else
                    {
                        ctx.GlobalSymbols.Add(name, new Symbol(name, SymbolType.Label, ctx.Position));
                    }
                    break;

                case LocalLabel localNode:
                    if (!ctx.InReassemblyScope)
                        Errors.ThrowSerious(node, "local labels not allowed outside a function");

                    name = localNode.Name;
                    if (ctx.LocalSymbols.TryGetValue(name, out sym) == false)
                    {
                        if (ctx.CausesReassembly(name))
                            nodeIndex = ctx.Reassemble(name) - 1;
                        else
                            ctx.LocalSymbols.Add(name, new Symbol(name, SymbolType.Label, ctx.Position));
                    }
                    else if (sym.Type == SymbolType.Label && sym.Phantom)
                    {
                        if (sym.Value != ctx.Position)
                            nodeIndex = ctx.Reassemble(name) - 1;
                        else
                            sym.Phantom = false;
                    }
                    else
                    {
                        Errors.ThrowSerious(node, "redefining local label");
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
