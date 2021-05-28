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

using System.Linq;
using Zilf.Compiler.Builtins;
using Zilf.Diagnostics;
using Zilf.Emit;
using Zilf.Interpreter;
using Zilf.Interpreter.Values;
using Zilf.Language;
using JetBrains.Annotations;

namespace Zilf.Compiler
{
    partial class Compilation
    {
        internal void CompileCondition([NotNull] IRoutineBuilder rb, [NotNull] ZilObject expr, [NotNull] ISourceLine src,
            [NotNull] ILabel label, bool polarity)
        {
            expr = expr.Unwrap(Context);
            var type = expr.StdTypeAtom;

            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (type)
            {
                case StdAtom.FALSE:
                    if (polarity == false)
                        rb.Branch(label);
                    return;

                case StdAtom.ATOM:
                    var atom = (ZilAtom)expr;
                    if (atom.StdAtom != StdAtom.T && atom.StdAtom != StdAtom.ELSE)
                    {
                        // could be a missing , or . before variable name
                        var warning = new CompilerError(src, CompilerMessages.Bare_Atom_0_Treated_As_True_Here, expr);

                        if (Locals.ContainsKey(atom) || Globals.ContainsKey(atom))
                            warning = warning.Combine(new CompilerError(src, CompilerMessages.Did_You_Mean_The_Variable));

                        Context.HandleError(warning);
                    }

                    if (polarity)
                        rb.Branch(label);
                    return;

                case StdAtom.FIX:
                    bool nonzero = ((ZilFix)expr).Value != 0;
                    if (polarity == nonzero)
                        rb.Branch(label);
                    return;

                case StdAtom.FORM:
                    // handled below
                    break;

                default:
                    Context.HandleError(new CompilerError(
                        expr.SourceLine ?? src,
                        CompilerMessages.Expressions_Of_Type_0_Cannot_Be_Compiled,
                        type));
                    return;
            }

            // it's a FORM
            var form = (ZilForm)expr;

            if (!(form.First is ZilAtom head))
            {
                Context.HandleError(new CompilerError(form, CompilerMessages.FORM_Must_Start_With_An_Atom));
                return;
            }

            // check for standard built-ins
            // prefer the predicate version, then value, value+predicate, void
            // (value+predicate is hard to clean up)
            var zversion = Context.ZEnvironment.ZVersion;
            var argCount = form.Count() - 1;
            if (ZBuiltins.IsBuiltinPredCall(head.Text, zversion, argCount))
            {
                ZBuiltins.CompilePredCall(head.Text, this, rb, form, label, polarity);
                return;
            }
            if (ZBuiltins.IsBuiltinValueCall(head.Text, zversion, argCount))
            {
                var result = ZBuiltins.CompileValueCall(head.Text, this, rb, form, rb.Stack);
                BranchIfNonZero(result);
                return;
            }
            if (ZBuiltins.IsBuiltinValuePredCall(head.Text, zversion, argCount))
            {
                if (rb.CleanStack)
                {
                    /* wasting the branch and checking the result with ZERO? is more efficient
                     * than using the branch and having to clean the result off the stack */
                    var noBranch = rb.DefineLabel();
                    ZBuiltins.CompileValuePredCall(head.Text, this, rb, form, rb.Stack, noBranch, true);
                    rb.MarkLabel(noBranch);
                    rb.BranchIfZero(rb.Stack, label, !polarity);
                }
                else
                {
                    ZBuiltins.CompileValuePredCall(head.Text, this, rb, form, rb.Stack, label, polarity);
                }
                return;
            }
            if (ZBuiltins.IsBuiltinVoidCall(head.Text, zversion, argCount))
            {
                ZBuiltins.CompileVoidCall(head.Text, this, rb, form);

                // void calls return true
                if (polarity)
                    rb.Branch(label);
                return;
            }

            // special cases
            var op1 = CompileAsOperand(rb, form, form.SourceLine);
            BranchIfNonZero(op1);

            void BranchIfNonZero(IOperand operand)
            {
                if (operand is INumericOperand numericResult)
                {
                    if (numericResult.Value != 0 == polarity)
                        rb.Branch(label);
                }
                else
                {
                    rb.BranchIfZero(operand, label, !polarity);
                }
            }
        }

        internal void CompileBoolean([NotNull] IRoutineBuilder rb, [NotNull] ZilObject[] args, [NotNull] ISourceLine src,
            bool and, [NotNull] ILabel label, bool polarity)
        {
            if (args.Length == 0)
            {
                // <AND> is true, <OR> is false
                if (and == polarity)
                    rb.Branch(label);
            }
            else if (args.Length == 1)
            {
                CompileCondition(rb, args[0], src, label, polarity);
            }
            else if (and == polarity)
            {
                // AND or NOR
                var failure = rb.DefineLabel();
                for (int i = 0; i < args.Length - 1; i++)
                    CompileCondition(rb, args[i], src, failure, !and);

                /* QUIRK: ZILCH considered <AND ... <SET X 0>> to be true,
                 * even though <SET X 0> is false. We emulate this issue by compiling the
                 * last element as a statement instead of a condition when it fits
                 * this pattern. */
                var last = args[args.Length - 1];
                if (and && last.IsSetToZeroForm())
                {
                    Context.HandleError(new CompilerError(last.SourceLine, CompilerMessages.Treating_SET_To_0_As_True_Here));
                    CompileStmt(rb, last, false);
                }
                else
                    CompileCondition(rb, last, src, label, and);

                rb.MarkLabel(failure);
            }
            else
            {
                // NAND or OR
                for (int i = 0; i < args.Length - 1; i++)
                    CompileCondition(rb, args[i], src, label, !and);

                /* QUIRK: Emulate the aforementioned SET issue. */
                var last = args[args.Length - 1];
                if (and && last.IsSetToZeroForm())
                {
                    Context.HandleError(new CompilerError(last.SourceLine, CompilerMessages.Treating_SET_To_0_As_True_Here));
                    CompileStmt(rb, last, false);
                }
                else
                    CompileCondition(rb, last, src, label, !and);
            }
        }

        [CanBeNull]
        [ContractAnnotation("wantResult: true => notnull")]
        [ContractAnnotation("wantResult: false => canbenull")]
        internal IOperand CompileBoolean([NotNull] IRoutineBuilder rb, [NotNull] ZilListoidBase args, [NotNull] ISourceLine src,
            bool and, bool wantResult, [CanBeNull] IVariable resultStorage)
        {
            if (!args.IsCons(out var first, out var rest))
                return and ? Game.One : Game.Zero;

            if (rest.IsEmpty)
            {
                if (wantResult)
                    return CompileAsOperand(rb, first, src, resultStorage);

                CompileStmt(rb, first, false);
                return Game.Zero;
            }

            ILabel lastLabel;

            if (!wantResult)
            {
                // easy path - don't need to preserve the values
                lastLabel = rb.DefineLabel();

                while (!rest.IsEmpty)
                {
                    var nextLabel = rb.DefineLabel();

                    CompileCondition(rb, first, src, nextLabel, and);

                    rb.Branch(lastLabel);
                    rb.MarkLabel(nextLabel);

                    (first, rest) = rest;
                }

                CompileStmt(rb, first, false);
                rb.MarkLabel(lastLabel);

                return Game.Zero;
            }

            // hard path - need to preserve the values and return the last one evaluated
            var tempAtom = ZilAtom.Parse("?TMP", Context);
            lastLabel = rb.DefineLabel();
            IVariable tempVar = null;
            ILabel trueLabel = null;

            resultStorage = resultStorage ?? rb.Stack;
            var nonStackResultStorage = resultStorage == rb.Stack ? null : resultStorage;

            IVariable TempVarProvider()
            {
                if (tempVar != null)
                    return tempVar;

                PushInnerLocal(rb, tempAtom, LocalBindingType.CompilerTemporary, src);
                tempVar = Locals[tempAtom].LocalBuilder;
                return tempVar;
            }

            ILabel TrueLabelProvider()
            {
                return trueLabel ?? (trueLabel = rb.DefineLabel());
            }

            IOperand result;
            while (!rest.IsEmpty)
            {
                var nextLabel = rb.DefineLabel();

                /* TODO: use "value or predicate" context here - if the expr is naturally a predicate,
                 * branch to a final label and synthesize the value without using a temp var,
                 * otherwise use the returned value */

                if (and)
                {
                    // for AND we only need the result of the last expr; otherwise we only care about truth value
                    CompileCondition(rb, first, src, nextLabel, true);
                    rb.EmitStore(resultStorage, Game.Zero);
                    rb.Branch(lastLabel);
                }
                else
                {
                    // for OR, if the value is true we want to return it; otherwise discard it and try the next expr
                    // however, if the expr is a predicate anyway, we can branch out of the OR if it's true;
                    // otherwise fall through to the next expr
                    if (first.IsPredicate(Context.ZEnvironment.ZVersion))
                    {
                        CompileCondition(rb, first, src, TrueLabelProvider(), true);
                        // fall through to nextLabel
                    }
                    else
                    {
                        result = CompileAsOperandWithBranch(rb, first, nonStackResultStorage, nextLabel, false,
                            TempVarProvider);

                        if (result != resultStorage)
                            rb.EmitStore(resultStorage, result);

                        rb.Branch(lastLabel);
                    }
                }

                rb.MarkLabel(nextLabel);

                (first, rest) = rest;
            }

            result = CompileAsOperand(rb, first, src, resultStorage);
            if (result != resultStorage)
                rb.EmitStore(resultStorage, result);

            if (trueLabel != null)
            {
                rb.Branch(lastLabel);
                rb.MarkLabel(trueLabel);
                rb.EmitStore(resultStorage, Game.One);
            }

            rb.MarkLabel(lastLabel);

            if (tempVar != null)
                PopInnerLocal(tempAtom);

            return resultStorage;
        }

        // TODO: refactor COND-like control structures to share an implementation, a la CompileBoundedLoop
        [CanBeNull]
        [ContractAnnotation("wantResult: true => notnull")]
        internal IOperand CompileCOND([NotNull] IRoutineBuilder rb, [NotNull] ZilListoidBase clauses, [NotNull] ISourceLine src,
            bool wantResult, [CanBeNull] IVariable resultStorage)
        {
            var nextLabel = rb.DefineLabel();
            var endLabel = rb.DefineLabel();
            bool elsePart = false;

            resultStorage = resultStorage ?? rb.Stack;
            while (!clauses.IsEmpty)
            {
                ZilObject clause, origCondition, condition;
                ZilListoidBase body;

                (clause, clauses) = clauses;
                clause = clause.Unwrap(Context);

                switch (clause)
                {
                    case ZilFalse _:
                        // previously, FALSE was only allowed when returned by a macro call, but now we expand macros before generating any code
                        continue;

                    case ZilListoidBase list when list.IsEmpty:
                    default:
                        throw new CompilerError(CompilerMessages.All_Clauses_In_0_Must_Be_Lists, "COND");

                    case ZilListoidBase list:
                        (origCondition, body) = list;
                        condition = origCondition.Unwrap(Context);
                        break;
                }

                // if condition is always true (i.e. not a FORM or a FALSE), this is the "else" part
                switch (condition)
                {
                    case ZilForm _:
                        // must be evaluated
                        MarkSequencePoint(rb, condition);
                        CompileCondition(rb, condition, condition.SourceLine, nextLabel, false);
                        break;

                    case ZilFalse _:
                        // never true
                        if (!(origCondition is ZilMacroResult))
                        {
                            Context.HandleError(new CompilerError(condition, CompilerMessages._0_Condition_Is_Always_1,
                                "COND", "false"));
                        }
                        continue;

                    case ZilAtom atom when atom.StdAtom == StdAtom.T || atom.StdAtom == StdAtom.ELSE:
                        // non-shady else part
                        elsePart = true;
                        break;

                    default:
                        // shady else part (always true, but not T or ELSE)
                        Context.HandleError(new CompilerError(condition, CompilerMessages._0_Condition_Is_Always_1, "COND", "true"));
                        elsePart = true;
                        break;
                }

                // emit code for clause
                var clauseResult = CompileClauseBody(rb, body, wantResult, resultStorage);
                if (wantResult && clauseResult != resultStorage)
                    rb.EmitStore(resultStorage, clauseResult);

                // jump to end
                if (!clauses.IsEmpty || wantResult && !elsePart)
                    rb.Branch(endLabel);

                rb.MarkLabel(nextLabel);

                if (elsePart)
                {
                    if (!clauses.IsEmpty)
                    {
                        Context.HandleError(new CompilerError(src, CompilerMessages._0_Clauses_After_Else_Part_Will_Never_Be_Evaluated, "COND"));
                    }

                    break;
                }

                nextLabel = rb.DefineLabel();
            }

            if (wantResult && !elsePart)
                rb.EmitStore(resultStorage, Game.Zero);

            rb.MarkLabel(endLabel);
            return wantResult ? resultStorage : null;
        }

        [CanBeNull]
        [ContractAnnotation("wantResult: true => notnull")]
        IOperand CompileClauseBody([NotNull] IRoutineBuilder rb, [NotNull] ZilListoidBase clause, bool wantResult,
            [CanBeNull] IVariable resultStorage)
        {
            if (clause.IsEmpty)
                return Game.One;

            IOperand result = null;

            do
            {
                var (first, rest) = clause;

                // only want the result of the last statement (if any)
                bool wantThisResult = wantResult && rest.IsEmpty;

                var stmt = first.Unwrap(Context);

                switch (stmt)
                {
                    case ZilForm form:
                        MarkSequencePoint(rb, form);

                        result = CompileForm(
                            rb,
                            form,
                            wantThisResult,
                            wantThisResult ? resultStorage : null);
                        break;

                    case ZilList _:
                        throw new CompilerError(stmt,
                                CompilerMessages.Expressions_Of_Type_0_Cannot_Be_Compiled,
                                stmt.GetTypeAtom(Context))
                            .Combine(new CompilerError(CompilerMessages.Misplaced_Bracket_In_COND_Or_Loop));

                    default:
                        if (wantThisResult)
                        {
                            result = CompileConstant(stmt);

                            if (result == null)
                            {
                                // TODO: show "expressions of this type cannot be compiled" warning even if wantResult is false?
                                throw new CompilerError(
                                    stmt,
                                    CompilerMessages.Expressions_Of_Type_0_Cannot_Be_Compiled,
                                    stmt.GetTypeAtom(Context));
                            }
                        }
                        break;
                }

                clause = rest;
            } while (!clause.IsEmpty);

            return result;
        }

        [CanBeNull]
        [ContractAnnotation("wantResult: true => notnull")]
        internal IOperand CompileVERSION_P([NotNull] IRoutineBuilder rb, [NotNull] ZilListoidBase clauses, [NotNull] ISourceLine src,
            bool wantResult, [CanBeNull] IVariable resultStorage)
        {
            resultStorage = resultStorage ?? rb.Stack;
            while (!clauses.IsEmpty)
            {
                ZilObject clause;

                (clause, clauses) = clauses;

                if (!(clause is ZilListoidBase list) || list.IsEmpty)
                    throw new CompilerError(CompilerMessages.All_Clauses_In_0_Must_Be_Lists, "VERSION?");

                var (condition, body) = list;

                // check version condition
                int condVersion;
                switch (condition)
                {
                    case ZilAtom atom:
                        // ReSharper disable once SwitchStatementMissingSomeCases
                        switch (atom.StdAtom)
                        {
                            case StdAtom.ZIP:
                                condVersion = 3;
                                break;
                            case StdAtom.EZIP:
                                condVersion = 4;
                                break;
                            case StdAtom.XZIP:
                                condVersion = 5;
                                break;
                            case StdAtom.YZIP:
                                condVersion = 6;
                                break;
                            case StdAtom.ELSE:
                            case StdAtom.T:
                                condVersion = 0;
                                break;
                            default:
                                throw new CompilerError(CompilerMessages.Unrecognized_Atom_In_VERSION_Must_Be_ZIP_EZIP_XZIP_YZIP_ELSET);
                        }
                        break;

                    case ZilFix fix:
                        condVersion = fix.Value;
                        if (condVersion < 3 || condVersion > 8)
                            throw new CompilerError(CompilerMessages.Version_Number_Out_Of_Range_Must_Be_38);
                        break;

                    default:
                        throw new CompilerError(CompilerMessages.Conditions_In_In_VERSION_Clauses_Must_Be_Atoms);
                }

                // does this clause match?
                if (condVersion != Context.ZEnvironment.ZVersion && condVersion != 0)
                    continue;

                // emit code for clause
                var clauseResult = CompileClauseBody(rb, body, wantResult, resultStorage);

                if (condVersion == 0 && !clauses.IsEmpty)
                {
                    Context.HandleError(new CompilerError(src, CompilerMessages._0_Clauses_After_Else_Part_Will_Never_Be_Evaluated, "VERSION?"));
                }

                return wantResult ? clauseResult : null;
            }

            // no matching clauses
            if (wantResult)
                rb.EmitStore(resultStorage, Game.Zero);

            return wantResult ? resultStorage : null;
        }

        [CanBeNull]
        [ContractAnnotation("wantResult: true => notnull")]
        internal IOperand CompileIFFLAG([NotNull] IRoutineBuilder rb, [NotNull] ZilListoidBase clauses, [NotNull] ISourceLine src,
            bool wantResult, [CanBeNull] IVariable resultStorage)
        {
            resultStorage = resultStorage ?? rb.Stack;

            while (!clauses.IsEmpty)
            {
                ZilObject clause;

                (clause, clauses) = clauses;

                if (!(clause is ZilListoidBase list) || list.IsEmpty)
                    throw new CompilerError(CompilerMessages.All_Clauses_In_0_Must_Be_Lists, "IFFLAG");

                var (flag, body) = list;

                ZilObject value;
                bool match, isElse = false;
                ZilAtom shadyElseAtom = null;

                switch (flag)
                {
                    case ZilAtom atom when (value = Context.GetCompilationFlagValue(atom)) != null:
                    case ZilString str when (value = Context.GetCompilationFlagValue(str.Text)) != null:
                        // name of a defined compilation flag
                        match = value.IsTrue;
                        break;

                    case ZilForm form:
                        form = Subrs.SubstituteIfflagForm(Context, form);
                        match = ((ZilObject)form.Eval(Context)).IsTrue;
                        break;

                    case ZilAtom atom when atom.StdAtom != StdAtom.ELSE && atom.StdAtom != StdAtom.T:
                        shadyElseAtom = atom;
                        goto default;

                    default:
                        match = isElse = true;
                        break;
                }

                // does this clause match?
                if (!match)
                    continue;

                // emit code for clause
                var clauseResult = CompileClauseBody(rb, body, wantResult, resultStorage);

                // warn if this is an else clause and there are more clauses below
                if (!isElse || clauses.IsEmpty)
                    return wantResult ? clauseResult : null;

                var warning = new CompilerError(src, CompilerMessages._0_Clauses_After_Else_Part_Will_Never_Be_Evaluated, "IFFLAG");

                if (shadyElseAtom != null)
                {
                    // if the else clause wasn't introduced with ELSE or T, it might not have been meant as an else clause
                    warning = warning.Combine(new CompilerError(
                        flag.SourceLine,
                        CompilerMessages.Undeclared_Compilation_Flag_0,
                        shadyElseAtom));
                }
                Context.HandleError(warning);

                return wantResult ? clauseResult : null;
            }

            // no matching clauses
            if (wantResult)
                rb.EmitStore(resultStorage, Game.Zero);

            return wantResult ? resultStorage : null;
        }
    }
}
