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
using Zilf.Diagnostics;
using Zilf.Emit;
using Zilf.Interpreter.Values;
using Zilf.Language;
using Zilf.ZModel;
using Zilf.ZModel.Vocab;

namespace Zilf.Compiler
{
    partial class Compilation
    {
        void BuildEarlySyntaxTables()
        {
            var dict = new Dictionary<string, ITableBuilder>();

            // TODO: encapsulate this in the VocabFormat classes
            if (Context.GetGlobalOption(StdAtom.NEW_PARSER_P))
                BuildNewFormatSyntaxTables(dict);
            else
                BuildOldFormatSyntaxTables(dict);

            foreach (var pair in dict)
                Constants.Add(Context.RootObList[pair.Key], pair.Value);
        }

        void BuildOldFormatSyntaxTables([NotNull] IDictionary<string, ITableBuilder> tables)
        {
            // TODO: emit VTBL as the first impure table, followed by syntax lines, which is what ztools expects?
            var verbTable = Game.DefineTable("VTBL", true);
            var actionTable = Game.DefineTable("ATBL", true);
            var preactionTable = Game.DefineTable("PATBL", true);

            tables.Add("VTBL", verbTable);
            tables.Add("ATBL", actionTable);
            tables.Add("PATBL", preactionTable);

            // compact syntaxes?
            var compact = Context.GetGlobalOption(StdAtom.COMPACT_SYNTAXES_P);

            var vf = Context.ZEnvironment.VocabFormat;

            // verb table
            var query = from s in Context.ZEnvironment.Syntaxes
                        group s by s.Verb into g
                        orderby vf.GetVerbValue(g.Key) descending
                        select g;

            var actions = new Dictionary<ZilAtom, Action>();

            foreach (var verb in query)
            {
                // syntax table
                var stbl = Game.DefineTable("ST?" + verb.Key.Atom, true);
                verbTable.AddShort(stbl);

                stbl.AddByte((byte)verb.Count());

                // make two passes over the syntax line definitions:
                // first in definition order to create/validate the Actions, second in reverse order to emit the syntax lines
                foreach (var line in verb)
                {
                    ValidateAction(actions, line);
                }

                foreach (var line in verb.Reverse())
                {
                    if (actions.TryGetValue(line.ActionName, out var act) == false)
                    {
                        // this can happen if an exception (e.g. undefined action routine) stops us from adding the action during the first pass.
                        continue;
                    }

                    try
                    {
                        using (DiagnosticContext.Push(line.SourceLine))
                        {
                            if (compact)
                            {
                                if (line.Preposition1 != null)
                                {
                                    var pn = vf.GetPrepositionValue(line.Preposition1);
                                    stbl.AddByte((byte)((pn & 63) | (line.NumObjects << 6)));
                                }
                                else
                                {
                                    stbl.AddByte((byte)(line.NumObjects << 6));
                                }
                                stbl.AddByte(act.Constant);

                                if (line.NumObjects > 0)
                                {
                                    stbl.AddByte((IOperand)GetFlag(line.FindFlag1) ?? Game.Zero);
                                    stbl.AddByte(line.Options1);

                                    if (line.NumObjects > 1)
                                    {
                                        if (line.Preposition2 != null)
                                        {
                                            var pn = vf.GetPrepositionValue(line.Preposition2);
                                            stbl.AddByte((byte)(pn & 63));
                                        }
                                        else
                                        {
                                            stbl.AddByte(0);
                                        }

                                        stbl.AddByte((IOperand)GetFlag(line.FindFlag2) ?? Game.Zero);
                                        stbl.AddByte(line.Options2);
                                    }
                                }
                            }
                            else
                            {
                                stbl.AddByte((byte)line.NumObjects);
                                stbl.AddByte(GetPreposition(line.Preposition1) ?? Game.Zero);
                                stbl.AddByte(GetPreposition(line.Preposition2) ?? Game.Zero);
                                stbl.AddByte((IOperand)GetFlag(line.FindFlag1) ?? Game.Zero);
                                stbl.AddByte((IOperand)GetFlag(line.FindFlag2) ?? Game.Zero);
                                stbl.AddByte(line.Options1);
                                stbl.AddByte(line.Options2);
                                stbl.AddByte(act.Constant);
                            }
                        }
                    }
                    catch (ZilError ex)
                    {
                        Context.HandleError(ex);
                    }
                }
            }

            // action and preaction table
            var actquery = from a in actions
                           orderby a.Value.Index
                           select a.Value;
            foreach (var act in actquery)
            {
                actionTable.AddShort(act.Routine);
                preactionTable.AddShort((IOperand)act.PreRoutine ?? Game.Zero);
            }
        }

        void BuildNewFormatSyntaxTables([NotNull] IDictionary<string, ITableBuilder> tables)
        {
            var actionTable = Game.DefineTable("ATBL", true);
            var preactionTable = Game.DefineTable("PATBL", true);

            tables.Add("ATBL", actionTable);
            tables.Add("PATBL", preactionTable);

            var query = from s in Context.ZEnvironment.Syntaxes
                        group s by s.Verb into verbGrouping
                        let numObjLookup = verbGrouping.ToLookup(s => s.NumObjects)
                        select new
                        {
                            Word = verbGrouping.Key,
                            Nullary = numObjLookup[0].FirstOrDefault(),
                            Unary = numObjLookup[1].ToArray(),
                            Binary = numObjLookup[2].ToArray()
                        };

            // syntax lines are emitted in definition order, so we can validate actions and emit syntax lines in one pass
            var actions = new Dictionary<ZilAtom, Action>();

            foreach (var verb in query)
            {
                // syntax table
                var name = "ACT?" + verb.Word.Atom.Text;
                var acttbl = Game.DefineTable(name, true);
                tables.Add(name, acttbl);

                // 0-object syntaxes
                if (verb.Nullary != null)
                {
                    var act = ValidateAction(actions, verb.Nullary);
                    acttbl.AddShort(act != null ? act.Constant : Game.Zero);
                }
                else
                {
                    acttbl.AddShort(-1);
                }

                // reserved word
                acttbl.AddShort(0);

                // 1-object syntaxes
                if (verb.Unary.Length > 0)
                {
                    var utbl = Game.DefineTable(null, true);
                    utbl.AddShort((short)verb.Unary.Length);

                    foreach (var line in verb.Unary)
                    {
                        var act = ValidateAction(actions, line);
                        utbl.AddShort(act?.Constant ?? Game.Zero);

                        utbl.AddShort(line.Preposition1 == null ? (IOperand)Game.Zero : Vocabulary[line.Preposition1]);
                        utbl.AddByte((IOperand)GetFlag(line.FindFlag1) ?? Game.Zero);
                        utbl.AddByte(line.Options1);
                    }

                    acttbl.AddShort(utbl);
                }
                else
                {
                    acttbl.AddShort(0);
                }

                // 2-object syntaxes
                if (verb.Binary.Length > 0)
                {
                    var btbl = Game.DefineTable(null, true);
                    btbl.AddShort((short)verb.Binary.Length);

                    foreach (var line in verb.Binary)
                    {
                        var act = ValidateAction(actions, line);
                        btbl.AddShort(act?.Constant ?? Game.Zero);

                        btbl.AddShort(line.Preposition1 == null ? (IOperand)Game.Zero : Vocabulary[line.Preposition1]);
                        btbl.AddByte((IOperand)GetFlag(line.FindFlag1) ?? Game.Zero);
                        btbl.AddByte(line.Options1);

                        btbl.AddShort(line.Preposition2 == null ? (IOperand)Game.Zero : Vocabulary[line.Preposition2]);
                        btbl.AddByte((IOperand)GetFlag(line.FindFlag2) ?? Game.Zero);
                        btbl.AddByte(line.Options2);
                    }

                    acttbl.AddShort(btbl);
                }
                else
                {
                    acttbl.AddShort(0);
                }
            }

            // action and preaction table
            var actquery = from a in actions
                           orderby a.Value.Index
                           select a.Value;
            foreach (var act in actquery)
            {
                actionTable.AddShort(act.Routine);
                preactionTable.AddShort((IOperand)act.PreRoutine ?? Game.Zero);
            }
        }

        void BuildLateSyntaxTables()
        {
            var helpers = new BuildLateSyntaxTablesHelpers
            {
                CompileConstantDelegate = CompileConstant,
                GetGlobalDelegate = atom => Globals[atom],
                Vocabulary = Vocabulary
            };

            Context.ZEnvironment.VocabFormat.BuildLateSyntaxTables(helpers);
        }

        [CanBeNull]
        Action ValidateAction([NotNull] Dictionary<ZilAtom, Action> actions, [NotNull] Syntax line)
        {
            try
            {
                using (DiagnosticContext.Push(line.SourceLine))
                {
                    if (actions.TryGetValue(line.ActionName, out var act) == false)
                    {
                        if (Routines.TryGetValue(line.Action, out var routine) == false)
                            throw new CompilerError(CompilerMessages.Undefined_0_1, "action routine", line.Action);

                        IRoutineBuilder preRoutine = null;
                        if (line.Preaction != null &&
                            Routines.TryGetValue(line.Preaction, out preRoutine) == false)
                            throw new CompilerError(CompilerMessages.Undefined_0_1, "preaction routine", line.Preaction);

                        var actionName = line.ActionName;
                        int index = Context.ZEnvironment.NextAction++;

                        if (index >= Context.ZEnvironment.VocabFormat.MaxActionCount)
                            throw new InterpreterError(
                                InterpreterMessages.Too_Many_0_Only_1_Allowed_In_This_Vocab_Format,
                                "actions",
                                Context.ZEnvironment.VocabFormat.MaxActionCount);

                        var number = Game.MakeOperand(index);
                        var constant = Game.DefineConstant(actionName.Text, number);
                        Constants.Add(actionName, constant);
                        if (WantDebugInfo)
                        {
                            Debug.Assert(Game.DebugFile != null);
                            Game.DebugFile.MarkAction(constant, actionName.Text);
                        }

                        act = new Action(index, constant, routine, preRoutine, line.Action, line.Preaction);
                        actions.Add(actionName, act);
                    }
                    else
                    {
                        WarnIfActionRoutineDiffers(line, "action routine", line.Action, act.RoutineName);
                        WarnIfActionRoutineDiffers(line, "preaction routine", line.Preaction, act.PreRoutineName);
                    }

                    return act;
                }
            }
            catch (ZilError ex)
            {
                Context.HandleError(ex);
                return null;
            }
        }

        void WarnIfActionRoutineDiffers([NotNull] Syntax line, [NotNull] string description,
            [CanBeNull] ZilAtom thisRoutineName, [CanBeNull] ZilAtom lastRoutineName)
        {
            if (thisRoutineName != lastRoutineName)
                Context.HandleError(new CompilerError(line.SourceLine,
                    CompilerMessages._0_Mismatch_For_1_Using_2_As_Before,
                    description,
                    line.ActionName,
                    lastRoutineName?.ToString() ?? "no " + description));
        }

        /// <summary>
        /// Defines the appropriate constants for a word (W?FOO, A?FOO, ACT?FOO, PREP?FOO),
        /// creating the IWordBuilder if needed.
        /// </summary>
        /// <param name="word">The Word.</param>
        /// 
        void DefineWord([NotNull] IWord word)
        {
            string rawWord = word.Atom.Text;

            if (!Vocabulary.ContainsKey(word))
            {
                var wAtom = ZilAtom.Parse("W?" + rawWord, Context);
                if (Constants.TryGetValue(wAtom, out var constantValue) == false)
                {
                    var wb = Game.DefineVocabularyWord(rawWord);
                    Vocabulary.Add(word, wb);
                    Constants.Add(wAtom, wb);
                }
                else
                {
                    if (constantValue is IWordBuilder wb)
                    {
                        Vocabulary.Add(word, wb);
                    }
                    else
                    {
                        throw new CompilerError(CompilerMessages.Nonvocab_Constant_0_Conflicts_With_Vocab_Word_1, wAtom, word.Atom);
                    }
                }
            }

            foreach (var pair in Context.ZEnvironment.VocabFormat.GetVocabConstants(word))
            {
                var atom = ZilAtom.Parse(pair.Key, Context);
                if (!Constants.ContainsKey(atom))
                    Constants.Add(atom,
                        Game.DefineConstant(pair.Key,
                            Game.MakeOperand(pair.Value)));
            }
        }

        [CanBeNull]
        [ContractAnnotation("notnull => notnull")]
        IOperand GetPreposition([CanBeNull] IWord word)
        {
            if (word == null)
                return null;

            string name = "PR?" + word.Atom.Text;
            var atom = ZilAtom.Parse(name, Context);
            return Constants[atom];
        }
    }
}
