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
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zilf.Interpreter;
using Zilf.Interpreter.Values;
using Zilf.Language;
using Zilf.ZModel.Values;

// ReSharper disable InconsistentNaming

namespace Zilf.Tests.Interpreter
{
    [TestClass, TestCategory("Interpreter")]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public class TypeTests
    {
        Context ctx;

        [TestInitialize]
        public void Initialize()
        {
            ctx = new Context();
            ctx.RegisterType(ZilAtom.Parse("WACKY", ctx), PrimType.LIST);

            // monad types
            ctx.SetLocalVal(ZilAtom.Parse("A-ATOM", ctx), ZilAtom.Parse("FOO", ctx));
            ctx.SetLocalVal(ZilAtom.Parse("A-CHARACTER", ctx), new ZilChar('C'));
            ctx.SetLocalVal(ZilAtom.Parse("A-FALSE", ctx), ctx.FALSE);
            ctx.SetLocalVal(ZilAtom.Parse("A-FIX", ctx), new ZilFix(123));

            // structured types
            ctx.SetLocalVal(ZilAtom.Parse("A-LIST", ctx), new ZilList(new ZilObject[] {
                new ZilFix(1),
                new ZilFix(2),
                new ZilFix(3)
            }));
            ctx.SetLocalVal(ZilAtom.Parse("A-FORM", ctx), new ZilForm(new ZilObject[] {
                ctx.GetStdAtom(StdAtom.Plus),
                new ZilFix(1),
                new ZilFix(2)
            }));
            ctx.SetLocalVal(ZilAtom.Parse("A-STRING", ctx), ZilString.FromString("hello"));
            ctx.SetLocalVal(ZilAtom.Parse("A-SUBR", ctx), ZilSubr.FromString(ctx, "+"));
            ctx.SetLocalVal(ZilAtom.Parse("A-FSUBR", ctx), ZilFSubr.FromString(ctx, "QUOTE"));
            ctx.SetLocalVal(ZilAtom.Parse("A-FUNCTION", ctx), new ZilFunction(
                ZilAtom.Parse("MYFUNC", ctx),
                null,
                new ZilObject[] { },
                null,
                new ZilObject[] { new ZilFix(3) }
            ));
            ctx.SetLocalVal(ZilAtom.Parse("A-MACRO", ctx), new ZilEvalMacro(
                new ZilFunction(
                    ZilAtom.Parse("MYMAC", ctx),
                    null,
                    new ZilObject[] { },
                    null,
                    new ZilObject[] {
                        new ZilForm(new ZilObject[] {
                            ctx.GetStdAtom(StdAtom.FORM),
                            ctx.GetStdAtom(StdAtom.Plus),
                            new ZilFix(1),
                            new ZilFix(2)
                        })
                    }
                )
            ));
            ctx.SetLocalVal(ZilAtom.Parse("A-VECTOR", ctx), new ZilVector(new ZilFix(4), new ZilFix(8), new ZilFix(15), new ZilFix(16), new ZilFix(23), new ZilFix(42)));
            ctx.SetLocalVal(ZilAtom.Parse("A-ADECL", ctx), new ZilAdecl(
                ZilString.FromString("FIDO"),
                ZilAtom.Parse("DOG", ctx)
            ));
            ctx.SetLocalVal(ZilAtom.Parse("A-OFFSET", ctx), new ZilOffset(
                2,
                new ZilForm(new[] { ctx.GetStdAtom(StdAtom.LIST), ctx.GetStdAtom(StdAtom.FIX), ctx.GetStdAtom(StdAtom.ATOM) }),
                ctx.GetStdAtom(StdAtom.ATOM)
            ));

            // special types
            ctx.SetLocalVal(ZilAtom.Parse("A-SEGMENT", ctx), new ZilSegment(
                new ZilForm(new ZilObject[]
                {
                    ctx.GetStdAtom(StdAtom.LIST),
                    new ZilFix(1),
                    new ZilFix(2)
                })));
            ctx.SetLocalVal(ZilAtom.Parse("A-WACKY", ctx),
                new ZilHash(ZilAtom.Parse("WACKY", ctx), PrimType.LIST, new ZilList(null, null)));

            // TODO: test other ZilObject descendants:
            // ObList, ZilActivation, ZilEnvironment, ZilChannel, ZilRoutine, ZilConstant, ZilGlobal, ZilTable, ZilModelObject, OffsetString?
        }

        [TestCleanup]
        public void Cleanup()
        {
            ctx = null;
        }

        [TestMethod]
        public void TestTYPE()
        {
            TestHelpers.EvalAndAssert(ctx, "<TYPE .A-ADECL>", ctx.GetStdAtom(StdAtom.ADECL));
            TestHelpers.EvalAndAssert(ctx, "<TYPE .A-ATOM>", ctx.GetStdAtom(StdAtom.ATOM));
            TestHelpers.EvalAndAssert(ctx, "<TYPE .A-CHARACTER>", ctx.GetStdAtom(StdAtom.CHARACTER));
            TestHelpers.EvalAndAssert(ctx, "<TYPE .A-FALSE>", ctx.GetStdAtom(StdAtom.FALSE));
            TestHelpers.EvalAndAssert(ctx, "<TYPE .A-FIX>", ctx.GetStdAtom(StdAtom.FIX));
            TestHelpers.EvalAndAssert(ctx, "<TYPE .A-LIST>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "<TYPE .A-FORM>", ctx.GetStdAtom(StdAtom.FORM));
            TestHelpers.EvalAndAssert(ctx, "<TYPE .A-STRING>", ctx.GetStdAtom(StdAtom.STRING));
            TestHelpers.EvalAndAssert(ctx, "<TYPE .A-SUBR>", ctx.GetStdAtom(StdAtom.SUBR));
            TestHelpers.EvalAndAssert(ctx, "<TYPE .A-FSUBR>", ctx.GetStdAtom(StdAtom.FSUBR));
            TestHelpers.EvalAndAssert(ctx, "<TYPE .A-FUNCTION>", ctx.GetStdAtom(StdAtom.FUNCTION));
            TestHelpers.EvalAndAssert(ctx, "<TYPE .A-MACRO>", ctx.GetStdAtom(StdAtom.MACRO));
            TestHelpers.EvalAndAssert(ctx, "<TYPE .A-SEGMENT>", ctx.GetStdAtom(StdAtom.SEGMENT));
            TestHelpers.EvalAndAssert(ctx, "<TYPE .A-VECTOR>", ctx.GetStdAtom(StdAtom.VECTOR));
            TestHelpers.EvalAndAssert(ctx, "<TYPE .A-OFFSET>", ctx.GetStdAtom(StdAtom.OFFSET));
            TestHelpers.EvalAndAssert(ctx, "<TYPE .A-WACKY>", ZilAtom.Parse("WACKY", ctx));

            // literals
            TestHelpers.EvalAndAssert(ctx, "<TYPE <QUOTE 5:FIX>>", ctx.GetStdAtom(StdAtom.ADECL));
            TestHelpers.EvalAndAssert(ctx, "<TYPE FOO>", ctx.GetStdAtom(StdAtom.ATOM));
            TestHelpers.EvalAndAssert(ctx, "<TYPE !\\c>", ctx.GetStdAtom(StdAtom.CHARACTER));
            TestHelpers.EvalAndAssert(ctx, "<TYPE <>>", ctx.GetStdAtom(StdAtom.FALSE));
            TestHelpers.EvalAndAssert(ctx, "<TYPE 5>", ctx.GetStdAtom(StdAtom.FIX));
            TestHelpers.EvalAndAssert(ctx, "<TYPE '(1 2)>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "<TYPE '<+ 1 2>>", ctx.GetStdAtom(StdAtom.FORM));
            TestHelpers.EvalAndAssert(ctx, "<TYPE \"HELLO\">", ctx.GetStdAtom(StdAtom.STRING));
            TestHelpers.EvalAndAssert(ctx, "<TYPE #SUBR \"+\">", ctx.GetStdAtom(StdAtom.SUBR));
            TestHelpers.EvalAndAssert(ctx, "<TYPE #FSUBR \"QUOTE\">", ctx.GetStdAtom(StdAtom.FSUBR));
            TestHelpers.EvalAndAssert(ctx, "<TYPE '!<FOO>>", ctx.GetStdAtom(StdAtom.SEGMENT));
            TestHelpers.EvalAndAssert(ctx, "<TYPE [1 2 3]>", ctx.GetStdAtom(StdAtom.VECTOR));
            // no literals for FUNCTION, MACRO, OFFSET, WACKY

            // must have 1 argument
            TestHelpers.EvalAndCatch<InterpreterError>("<TYPE>");
            TestHelpers.EvalAndCatch<InterpreterError>("<TYPE FOO BAR>");
        }

        [TestMethod]
        public void TestTYPE_P()
        {
            TestHelpers.EvalAndAssert(ctx, "<TYPE? .A-ATOM ATOM>", ctx.GetStdAtom(StdAtom.ATOM));
            TestHelpers.EvalAndAssert(ctx, "<TYPE? .A-ATOM STRING ATOM>", ctx.GetStdAtom(StdAtom.ATOM));
            TestHelpers.EvalAndAssert(ctx, "<TYPE? .A-LIST STRING ATOM>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<TYPE? .A-LIST LIST STRING ATOM>", ctx.GetStdAtom(StdAtom.LIST));

            // must have at least 2 arguments
            TestHelpers.EvalAndCatch<InterpreterError>("<TYPE?>");
            TestHelpers.EvalAndCatch<InterpreterError>("<TYPE? FOO>");
        }

        [TestMethod]
        public void TestPRIMTYPE()
        {
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE .A-ADECL>", ctx.GetStdAtom(StdAtom.VECTOR));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE .A-ATOM>", ctx.GetStdAtom(StdAtom.ATOM));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE .A-CHARACTER>", ctx.GetStdAtom(StdAtom.FIX));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE .A-FALSE>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE .A-FIX>", ctx.GetStdAtom(StdAtom.FIX));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE .A-LIST>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE .A-FORM>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE .A-STRING>", ctx.GetStdAtom(StdAtom.STRING));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE .A-SUBR>", ctx.GetStdAtom(StdAtom.STRING));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE .A-FSUBR>", ctx.GetStdAtom(StdAtom.STRING));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE .A-FUNCTION>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE .A-MACRO>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE .A-SEGMENT>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE .A-VECTOR>", ctx.GetStdAtom(StdAtom.VECTOR));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE .A-OFFSET>", ctx.GetStdAtom(StdAtom.VECTOR));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE .A-WACKY>", ctx.GetStdAtom(StdAtom.LIST));

            // literals
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE <QUOTE 5:FIX>>", ctx.GetStdAtom(StdAtom.VECTOR));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE FOO>", ctx.GetStdAtom(StdAtom.ATOM));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE !\\c>", ctx.GetStdAtom(StdAtom.FIX));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE <>>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE 5>", ctx.GetStdAtom(StdAtom.FIX));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE '(1 2)>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE '<+ 1 2>>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE \"HELLO\">", ctx.GetStdAtom(StdAtom.STRING));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE #SUBR \"+\">", ctx.GetStdAtom(StdAtom.STRING));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE #FSUBR \"QUOTE\">", ctx.GetStdAtom(StdAtom.STRING));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE '!<FOO>>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "<PRIMTYPE [1 2 3]>", ctx.GetStdAtom(StdAtom.VECTOR));
            // no literals for FUNCTION, MACRO, OFFSET, WACKY

            // must have 1 argument
            TestHelpers.EvalAndCatch<InterpreterError>("<PRIMTYPE>");
            TestHelpers.EvalAndCatch<InterpreterError>("<PRIMTYPE FOO BAR>");
        }

        [TestMethod]
        public void TestTYPEPRIM()
        {
            TestHelpers.EvalAndAssert(ctx, "<TYPEPRIM ADECL>", ctx.GetStdAtom(StdAtom.VECTOR));
            TestHelpers.EvalAndAssert(ctx, "<TYPEPRIM ATOM>", ctx.GetStdAtom(StdAtom.ATOM));
            TestHelpers.EvalAndAssert(ctx, "<TYPEPRIM CHARACTER>", ctx.GetStdAtom(StdAtom.FIX));
            TestHelpers.EvalAndAssert(ctx, "<TYPEPRIM FALSE>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "<TYPEPRIM FIX>", ctx.GetStdAtom(StdAtom.FIX));
            TestHelpers.EvalAndAssert(ctx, "<TYPEPRIM LIST>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "<TYPEPRIM FORM>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "<TYPEPRIM STRING>", ctx.GetStdAtom(StdAtom.STRING));
            TestHelpers.EvalAndAssert(ctx, "<TYPEPRIM SUBR>", ctx.GetStdAtom(StdAtom.STRING));
            TestHelpers.EvalAndAssert(ctx, "<TYPEPRIM FSUBR>", ctx.GetStdAtom(StdAtom.STRING));
            TestHelpers.EvalAndAssert(ctx, "<TYPEPRIM FUNCTION>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "<TYPEPRIM MACRO>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "<TYPEPRIM SEGMENT>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "<TYPEPRIM VECTOR>", ctx.GetStdAtom(StdAtom.VECTOR));
            TestHelpers.EvalAndAssert(ctx, "<TYPEPRIM OFFSET>", ctx.GetStdAtom(StdAtom.VECTOR));
            TestHelpers.EvalAndAssert(ctx, "<TYPEPRIM WACKY>", ctx.GetStdAtom(StdAtom.LIST));

            // error if not a registered type
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<TYPEPRIM XYZZY>");

            // must have 1 argument
            TestHelpers.EvalAndCatch<InterpreterError>("<TYPEPRIM>");
            TestHelpers.EvalAndCatch<InterpreterError>("<TYPEPRIM FOO BAR>");
        }

        [TestMethod]
        public void TestCHTYPE()
        {
            // everything can be coerced to its own type
            string[] types = {
                "ADECL", "ATOM", "CHARACTER", "FALSE", "FIX", "LIST", "FORM", "STRING",
                "SUBR", "FSUBR", "FUNCTION", "MACRO", "SEGMENT", "VECTOR", "OFFSET", "WACKY"
            };

            foreach (var t in types)
            {
                TestHelpers.EvalAndAssert(
                    ctx,
                    string.Format("<CHTYPE .A-{0} {0}>", t),
                    ctx.GetLocalVal(ZilAtom.Parse("A-" + t, ctx)) ?? throw new InvalidOperationException());
            }

            // specific type coercions are tested in other methods

            // must have 2 arguments
            TestHelpers.EvalAndCatch<InterpreterError>("<CHTYPE>");
            TestHelpers.EvalAndCatch<InterpreterError>("<CHTYPE 5>");
            TestHelpers.EvalAndCatch<InterpreterError>("<CHTYPE 5 FIX FIX>");
        }

        [TestMethod]
        public void TestCHTYPE_to_ATOM()
        {
            // nothing can be coerced to ATOM
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ADECL ATOM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-CHARACTER ATOM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FALSE ATOM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FIX ATOM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-LIST ATOM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FORM ATOM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-STRING ATOM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SUBR ATOM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FSUBR ATOM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FUNCTION ATOM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-MACRO ATOM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SEGMENT ATOM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-VECTOR ATOM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-OFFSET ATOM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-WACKY ATOM>");
        }

        [TestMethod]
        public void TestCHTYPE_to_CHARACTER()
        {
            // FIX can be coerced to CHARACTER
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-FIX CHARACTER>",
                new ZilChar((char)123));

            // other types can't
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ADECL CHARACTER>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ATOM CHARACTER>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FALSE CHARACTER>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-LIST CHARACTER>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FORM CHARACTER>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-STRING CHARACTER>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SUBR CHARACTER>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FSUBR CHARACTER>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FUNCTION CHARACTER>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-MACRO CHARACTER>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SEGMENT CHARACTER>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-VECTOR CHARACTER>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-OFFSET CHARACTER>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-WACKY CHARACTER>");
        }

        [TestMethod]
        public void TestCHTYPE_to_FALSE()
        {
            // list-based types can be coerced to FALSE
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-LIST FALSE>",
                new ZilFalse(new ZilList(new ZilObject[] {
                    new ZilFix(1), new ZilFix(2), new ZilFix(3)
                })));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-FORM FALSE>",
                new ZilFalse(new ZilList(new ZilObject[] {
                    ctx.GetStdAtom(StdAtom.Plus), new ZilFix(1), new ZilFix(2)
                })));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-FUNCTION FALSE>",
                new ZilFalse(new ZilList(new ZilObject[] {
                    new ZilList(null, null),
                    new ZilFix(3)
                })));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-MACRO FALSE>",
                new ZilFalse(new ZilList(new ZilFunction(
                        null,
                        null,
                        new ZilObject[] { },
                        null,
                        new ZilObject[] {
                            new ZilForm(new ZilObject[] {
                                ctx.GetStdAtom(StdAtom.FORM),
                                ctx.GetStdAtom(StdAtom.Plus),
                                new ZilFix(1),
                                new ZilFix(2)
                            })
                        }),
                    new ZilList(null, null))));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-SEGMENT FALSE>",
                new ZilFalse(new ZilList(new ZilObject[] {
                    ctx.GetStdAtom(StdAtom.LIST), new ZilFix(1), new ZilFix(2)
                })));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-WACKY FALSE>",
                new ZilFalse(new ZilList(null, null)));

            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ADECL FALSE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ATOM FALSE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-CHARACTER FALSE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FIX FALSE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-STRING FALSE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SUBR FALSE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FSUBR FALSE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-VECTOR FALSE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-OFFSET FALSE>");
        }

        [TestMethod]
        public void TestCHTYPE_to_FIX()
        {
            // CHARACTER can be coerced to FIX
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-CHARACTER FIX>", new ZilFix(67));

            // other types can't
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ADECL FIX>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ATOM FIX>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FALSE FIX>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-LIST FIX>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FORM FIX>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-STRING FIX>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SUBR FIX>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FSUBR FIX>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FUNCTION FIX>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-MACRO FIX>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SEGMENT FIX>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-VECTOR FIX>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-OFFSET FIX>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-WACKY FIX>");
        }

        [TestMethod]
        public void TestCHTYPE_to_LIST()
        {
            // list-based types can be coerced to LIST
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-FALSE LIST>",
                new ZilList(null, null));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-FORM LIST>",
                new ZilList(new ZilObject[] {
                    ctx.GetStdAtom(StdAtom.Plus), new ZilFix(1), new ZilFix(2)
                }));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-FUNCTION LIST>",
                new ZilList(new ZilObject[] {
                    new ZilList(null, null),
                    new ZilFix(3)
                }));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-MACRO LIST>",
                new ZilList(new ZilFunction(
                        null,
                        null,
                        new ZilObject[] { },
                        null,
                        new ZilObject[] {
                            new ZilForm(new ZilObject[] {
                                ctx.GetStdAtom(StdAtom.FORM),
                                ctx.GetStdAtom(StdAtom.Plus),
                                new ZilFix(1),
                                new ZilFix(2)
                            })
                        }),
                    new ZilList(null, null)));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-SEGMENT LIST>",
                new ZilList(new ZilObject[] {
                    ctx.GetStdAtom(StdAtom.LIST), new ZilFix(1), new ZilFix(2)
                }));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-WACKY LIST>",
                new ZilList(null, null));

            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ADECL LIST>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ATOM LIST>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-CHARACTER LIST>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FIX LIST>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-STRING LIST>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SUBR LIST>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FSUBR LIST>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-VECTOR LIST>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-OFFSET LIST>");
        }

        [TestMethod]
        public void TestCHTYPE_to_FORM()
        {
            // list-based types can be coerced to FORM
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-FALSE FORM>",
                new ZilForm(new ZilObject[] { }));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-LIST FORM>",
                new ZilForm(new ZilObject[] {
                    new ZilFix(1), new ZilFix(2), new ZilFix(3)
                }));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-FUNCTION FORM>",
                new ZilForm(new ZilObject[] {
                    new ZilList(null, null),
                    new ZilFix(3)
                }));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-MACRO FORM>",
                new ZilForm(new ZilObject[] { new ZilFunction(
                    null,
                    null,
                    new ZilObject[] { },
                    null,
                    new ZilObject[] {
                        new ZilForm(new ZilObject[] {
                            ctx.GetStdAtom(StdAtom.FORM),
                            ctx.GetStdAtom(StdAtom.Plus),
                            new ZilFix(1),
                            new ZilFix(2)
                        })
                    })
                }));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-SEGMENT FORM>",
                new ZilForm(new ZilObject[] {
                    ctx.GetStdAtom(StdAtom.LIST), new ZilFix(1), new ZilFix(2)
                }));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-WACKY FORM>",
                new ZilForm(new ZilObject[] { }));

            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ADECL FORM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ATOM FORM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-CHARACTER FORM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FIX FORM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-STRING FORM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SUBR FORM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FSUBR FORM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-VECTOR FORM>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-OFFSET FORM>");
        }

        [TestMethod]
        public void TestCHTYPE_to_STRING()
        {
            // string-based types can be coerced to STRING
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-SUBR STRING>",
                ZilString.FromString("+"));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-FSUBR STRING>",
                ZilString.FromString("QUOTE"));

            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ADECL STRING>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ATOM STRING>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-CHARACTER STRING>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FIX STRING>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FALSE STRING>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-LIST STRING>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FORM STRING>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FUNCTION STRING>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-MACRO STRING>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SEGMENT STRING>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-VECTOR STRING>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-OFFSET STRING>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-WACKY STRING>");
        }

        [TestMethod]
        public void TestCHTYPE_to_SUBR()
        {
            // string-based types can be coerced to SUBR if they name an appropriate Subrs method
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE \"+\" SUBR>",
                ZilSubr.FromString(ctx, "+"));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-FSUBR SUBR>",
                ZilSubr.FromString(ctx, "QUOTE"));

            // arbitrary strings and non-matching methods can't
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE \"\" SUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE \"foobarbaz\" SUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE \"PerformArithmetic\" SUBR>");

            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ADECL SUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ATOM SUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-CHARACTER SUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FIX SUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FALSE SUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-LIST SUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FORM SUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FUNCTION SUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-MACRO SUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SEGMENT SUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-VECTOR SUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-OFFSET SUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-WACKY SUBR>");
        }

        [TestMethod]
        public void TestCHTYPE_to_FSUBR()
        {
            // string-based types can be coerced to FSUBR if they name an appropriate Subrs method
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE \"DEFINE\" FSUBR>",
                ZilFSubr.FromString(ctx, "DEFINE"));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-SUBR FSUBR>",
                ZilFSubr.FromString(ctx, "+"));

            // arbitrary strings and non-matching methods can't
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE \"\" FSUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE \"foobarbaz\" FSUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE \"PerformArithmetic\" FSUBR>");

            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ADECL FSUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ATOM FSUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-CHARACTER FSUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FIX FSUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FALSE FSUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-LIST FSUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FORM FSUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FUNCTION FSUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-MACRO FSUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SEGMENT FSUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-VECTOR FSUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-OFFSET FSUBR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-WACKY FSUBR>");
        }

        [TestMethod]
        public void TestCHTYPE_to_FUNCTION()
        {
            // list-based types can be coerced to FUNCTION if they fit the pattern ((argspec) body)
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE ((X) '<TYPE X>) FUNCTION>",
                new ZilFunction(
                    null,
                    null,
                    new ZilObject[] { ZilAtom.Parse("X", ctx) },
                    null,
                    new ZilObject[] {
                        new ZilForm(new ZilObject[] {
                            ZilAtom.Parse("TYPE", ctx),
                            ZilAtom.Parse("X", ctx)
                        })
                    }
                ));

            // arbitrary lists and other values can't
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ADECL FUNCTION>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ATOM FUNCTION>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-CHARACTER FUNCTION>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FIX FUNCTION>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FALSE FUNCTION>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-LIST FUNCTION>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FORM FUNCTION>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-STRING FUNCTION>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SUBR FUNCTION>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FSUBR FUNCTION>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-MACRO FUNCTION>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SEGMENT FUNCTION>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-VECTOR FUNCTION>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-OFFSET FUNCTION>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-WACKY FUNCTION>");
        }

        [TestMethod]
        public void TestCHTYPE_to_MACRO()
        {
            // list-based types can be coerced to MACRO if they fit the pattern (applicable)
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE (.A-FUNCTION) MACRO>",
                new ZilEvalMacro(new ZilFunction(
                    ZilAtom.Parse("MYFUNC", ctx),
                    null,
                    new ZilObject[] { },
                    null,
                    new ZilObject[] { new ZilFix(3) })));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE '<#SUBR \"+\"> MACRO>",
                new ZilEvalMacro(ZilSubr.FromString(ctx, "+")));

            // arbitrary lists and other values can't
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ADECL MACRO>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ATOM MACRO>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-CHARACTER MACRO>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FIX MACRO>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FALSE MACRO>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-LIST MACRO>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FORM MACRO>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-STRING MACRO>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SUBR MACRO>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FSUBR MACRO>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FUNCTION MACRO>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SEGMENT MACRO>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-VECTOR MACRO>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-OFFSET MACRO>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-WACKY MACRO>");
        }

        [TestMethod]
        public void TestCHTYPE_to_SEGMENT()
        {
            // list-based types can be coerced to SEGMENT
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-FALSE SEGMENT>",
                new ZilSegment(new ZilForm(new ZilObject[] { })));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-LIST SEGMENT>",
                new ZilSegment(new ZilForm(new ZilObject[] {
                    new ZilFix(1), new ZilFix(2), new ZilFix(3)
                })));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-FUNCTION SEGMENT>",
                new ZilSegment(new ZilForm(new ZilObject[] {
                    new ZilList(null, null),
                    new ZilFix(3)
                })));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-MACRO SEGMENT>",
                new ZilSegment(new ZilForm(new ZilObject[] { new ZilFunction(
                    null,
                    null,
                    new ZilObject[] { },
                    null,
                    new ZilObject[] {
                        new ZilForm(new ZilObject[] {
                            ctx.GetStdAtom(StdAtom.FORM),
                            ctx.GetStdAtom(StdAtom.Plus),
                            new ZilFix(1),
                            new ZilFix(2)
                        })
                    })
                })));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-FORM SEGMENT>",
                new ZilSegment(new ZilForm(new ZilObject[] {
                    ctx.GetStdAtom(StdAtom.Plus), new ZilFix(1), new ZilFix(2)
                })));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-WACKY SEGMENT>",
                new ZilSegment(new ZilForm(new ZilObject[] { })));

            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ADECL SEGMENT>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ATOM SEGMENT>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-CHARACTER SEGMENT>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FIX SEGMENT>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-STRING SEGMENT>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SUBR SEGMENT>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FSUBR SEGMENT>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-VECTOR SEGMENT>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-OFFSET SEGMENT>");
        }

        [TestMethod]
        public void TestCHTYPE_to_Unregistered_Type()
        {
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ADECL NOT-A-TYPE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ATOM NOT-A-TYPE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-CHARACTER NOT-A-TYPE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FIX NOT-A-TYPE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FALSE NOT-A-TYPE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-LIST NOT-A-TYPE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FORM NOT-A-TYPE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-STRING NOT-A-TYPE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SUBR NOT-A-TYPE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FSUBR NOT-A-TYPE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FUNCTION NOT-A-TYPE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-MACRO NOT-A-TYPE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SEGMENT NOT-A-TYPE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-VECTOR NOT-A-TYPE>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-OFFSET NOT-A-TYPE>");
        }

        [TestMethod]
        public void TestCHTYPE_to_VECTOR()
        {
            // vector-based types can be coerced to VECTOR
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-ADECL VECTOR>",
                new ZilVector(ZilString.FromString("FIDO"), ZilAtom.Parse("DOG", ctx))
            );
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE .A-OFFSET VECTOR>",
                new ZilVector(new ZilFix(2), new ZilForm(new[] { ctx.GetStdAtom(StdAtom.LIST), ctx.GetStdAtom(StdAtom.FIX), ctx.GetStdAtom(StdAtom.ATOM) }), ctx.GetStdAtom(StdAtom.ATOM))
            );

            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ATOM VECTOR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-CHARACTER VECTOR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FIX VECTOR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FALSE VECTOR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-LIST VECTOR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FORM VECTOR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-STRING VECTOR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SUBR VECTOR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FSUBR VECTOR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FUNCTION VECTOR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-MACRO VECTOR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SEGMENT VECTOR>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-WACKY VECTOR>");
        }

        [TestMethod]
        public void TestCHTYPE_to_ADECL()
        {
            // vector-based types can be coerced to ADECL, but only if they have length 2
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE <VECTOR FIX 5> ADECL>",
                new ZilAdecl(ctx.GetStdAtom(StdAtom.FIX), new ZilFix(5))
            );

            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ATOM ADECL>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-CHARACTER ADECL>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FIX ADECL>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FALSE ADECL>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-LIST ADECL>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FORM ADECL>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-STRING ADECL>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SUBR ADECL>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FSUBR ADECL>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FUNCTION ADECL>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-MACRO ADECL>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SEGMENT ADECL>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-VECTOR ADECL>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-OFFSET ADECL>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-WACKY ADECL>");
        }

        [TestMethod]
        public void TestCHTYPE_TO_OFFSET()
        {
            // vector-based types can be coerced to OFFSET, but only if they have length 3
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE <VECTOR 1 LIST FIX> OFFSET>",
                new ZilOffset(1, ctx.GetStdAtom(StdAtom.LIST), ctx.GetStdAtom(StdAtom.FIX))
            );

            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ADECL OFFSET>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-ATOM OFFSET>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-CHARACTER OFFSET>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FIX OFFSET>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FALSE OFFSET>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-LIST OFFSET>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FORM OFFSET>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-STRING OFFSET>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SUBR OFFSET>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FSUBR OFFSET>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-FUNCTION OFFSET>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-MACRO OFFSET>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-SEGMENT OFFSET>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-VECTOR OFFSET>");
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<CHTYPE .A-WACKY OFFSET>");
        }

        [TestMethod]
        public void TestAPPLICABLE_P()
        {
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? .A-FIX>", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? .A-SUBR>", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? .A-FSUBR>", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? .A-FUNCTION>", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? .A-MACRO>", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? .A-OFFSET>", ctx.TRUE);

            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? .A-ADECL>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? .A-ATOM>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? .A-CHARACTER>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? .A-FALSE>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? .A-LIST>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? .A-FORM>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? .A-STRING>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? .A-SEGMENT>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? .A-VECTOR>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? .A-WACKY>", ctx.FALSE);

            // must have 1 argument
            TestHelpers.EvalAndCatch<InterpreterError>("<APPLICABLE?>");
            TestHelpers.EvalAndCatch<InterpreterError>("<APPLICABLE? FOO BAR>");
        }

        [TestMethod]
        public void TestSTRUCTURED_P()
        {
            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? .A-ADECL>", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? .A-FALSE>", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? .A-FUNCTION>", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? .A-MACRO>", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? .A-LIST>", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? .A-FORM>", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? .A-STRING>", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? .A-SEGMENT>", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? .A-VECTOR>", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? .A-OFFSET>", ctx.TRUE);

            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? .A-FIX>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? .A-ATOM>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? .A-CHARACTER>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? .A-SUBR>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? .A-FSUBR>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? .A-WACKY>", ctx.FALSE);

            // must have 1 argument
            TestHelpers.EvalAndCatch<InterpreterError>("<STRUCTURED?>");
            TestHelpers.EvalAndCatch<InterpreterError>("<STRUCTURED? FOO BAR>");
        }

        [TestMethod]
        public void TestFORM()
        {
            TestHelpers.EvalAndAssert(ctx, "<FORM>", new ZilForm(null, null));
            TestHelpers.EvalAndAssert(ctx, "<FORM + 1 2>", new ZilForm(
                new ZilObject[] {
                    ctx.GetStdAtom(StdAtom.Plus),
                    new ZilFix(1),
                    new ZilFix(2)
                }
            ));
        }

        [TestMethod]
        public void TestLIST()
        {
            TestHelpers.EvalAndAssert("<LIST>", new ZilList(null, null));
            TestHelpers.EvalAndAssert("<LIST 1>", new ZilList(new ZilFix(1), new ZilList(null, null)));
            TestHelpers.EvalAndAssert("<LIST 1 2 3>", new ZilList(
                new ZilObject[] {
                    new ZilFix(1),
                    new ZilFix(2),
                    new ZilFix(3)
                }
            ));
        }

        [TestMethod]
        public void TestVECTOR()
        {
            TestHelpers.EvalAndAssert("<VECTOR>", new ZilVector());
            TestHelpers.EvalAndAssert("<VECTOR 1>", new ZilVector(new ZilFix(1)));
            TestHelpers.EvalAndAssert("<VECTOR 1 2 3>", new ZilVector(new ZilFix(1), new ZilFix(2), new ZilFix(3)));

            TestHelpers.EvalAndAssert("<REST [1 2 3]>",
                new ZilVector(new ZilFix(2), new ZilFix(3)));

            TestHelpers.EvalAndAssert("<SET X [1 2 3]> <SET Y <REST .X>> <1 .Y 0> .X",
                new ZilVector(new ZilFix(1), new ZilFix(0), new ZilFix(3)));
        }

        [TestMethod]
        public void TestCONS()
        {
            TestHelpers.EvalAndAssert(ctx, "<CONS FOO (BAR)>",
                new ZilList(ZilAtom.Parse("FOO", ctx),
                    new ZilList(ZilAtom.Parse("BAR", ctx),
                        new ZilList(null, null))));
            TestHelpers.EvalAndAssert(ctx, "<CONS () ()>",
                new ZilList(new ZilList(null, null),
                    new ZilList(null, null)));

            // second argument can be a form, but the tail of the new list will still become a list
            TestHelpers.EvalAndAssert(ctx, "<CONS FOO '<BAR>>",
                new ZilList(ZilAtom.Parse("FOO", ctx),
                    new ZilList(ZilAtom.Parse("BAR", ctx),
                        new ZilList(null, null))));
            TestHelpers.EvalAndAssert(ctx, "<TYPE <REST <CONS FOO '<BAR>>>>",
                ctx.GetStdAtom(StdAtom.LIST));

            // second argument can't be another type
            TestHelpers.EvalAndCatch<InterpreterError>("<CONS FOO BAR>");
            TestHelpers.EvalAndCatch<InterpreterError>("<CONS () FOO>");

            // must have 2 arguments
            TestHelpers.EvalAndCatch<InterpreterError>("<CONS>");
            TestHelpers.EvalAndCatch<InterpreterError>("<CONS FOO>");
            TestHelpers.EvalAndCatch<InterpreterError>("<CONS FOO () BAR>");
        }

        [TestMethod]
        public void TestFUNCTION()
        {
            TestHelpers.EvalAndAssert("<FUNCTION () 5>", new ZilFunction(
                null,
                null,
                new ZilObject[] { },
                null,
                new ZilObject[] { new ZilFix(5) }
            ));

            // argument list must be valid
            TestHelpers.EvalAndCatch<InterpreterError>("<FUNCTION 1 2>");
            TestHelpers.EvalAndCatch<InterpreterError>("<FUNCTION (()) 123>");
            TestHelpers.EvalAndCatch<InterpreterError>("<FUNCTION (FOO 9) 123>");
            TestHelpers.EvalAndCatch<InterpreterError>("<FUNCTION ('9) 123>");

            // must have at least 2 arguments
            TestHelpers.EvalAndCatch<InterpreterError>("<FUNCTION>");
            TestHelpers.EvalAndCatch<InterpreterError>("<FUNCTION ()>");
        }

        [TestMethod]
        public void TestSTRING()
        {
            TestHelpers.EvalAndAssert("<STRING>", ZilString.FromString(""));
            TestHelpers.EvalAndAssert("<STRING !\\A !\\B>", ZilString.FromString("AB"));
            TestHelpers.EvalAndAssert("<STRING \"hello\">", ZilString.FromString("hello"));
            TestHelpers.EvalAndAssert("<STRING \"hel\" \"lo\" !\\!>", ZilString.FromString("hello!"));

            // arguments must be characters or strings
            TestHelpers.EvalAndCatch<InterpreterError>("<STRING 123>");
        }

        [TestMethod]
        public void TestASCII()
        {
            TestHelpers.EvalAndAssert("<ASCII !\\A>", new ZilFix(65));
            TestHelpers.EvalAndAssert("<ASCII 65>", new ZilChar('A'));

            // argument must be a character or FIX
            TestHelpers.EvalAndCatch<InterpreterError>("<ASCII \"A\">");

            // must have 1 argument
            TestHelpers.EvalAndCatch<InterpreterError>("<ASCII>");
            TestHelpers.EvalAndCatch<InterpreterError>("<ASCII !\\A !\\B>");
        }

        [TestMethod]
        public void TestCustomType_BYTE()
        {
            TestHelpers.EvalAndAssert(ctx, "<TYPE #BYTE 255>", ctx.GetStdAtom(StdAtom.BYTE));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE #BYTE 255 FIX>", new ZilFix(255));

            TestHelpers.EvalAndCatch<InterpreterError>("#BYTE \"f\"");

            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? #BYTE 0>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? #BYTE 0>", ctx.FALSE);

            TestHelpers.EvalAndAssert(ctx, "<=? #BYTE 255 #BYTE 255>", ctx.TRUE);
        }

        [TestMethod]
        public void TestCustomType_DECL()
        {
            TestHelpers.EvalAndAssert(ctx, "<TYPE #DECL ((FOO) FIX)>", ZilAtom.Parse("DECL", ctx));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE #DECL ((FOO) FIX) LIST>",
                new ZilList(new ZilObject[] {
                    new ZilList(ZilAtom.Parse("FOO", ctx), new ZilList(null, null)),
                    ctx.GetStdAtom(StdAtom.FIX)
                }));

            TestHelpers.EvalAndCatch<InterpreterError>("#DECL BLAH");

            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? #DECL ((FOO) FIX)>", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? #DECL ((FOO) FIX)>", ctx.FALSE);

            TestHelpers.EvalAndAssert(ctx, "<=? #DECL ((FOO) FIX) #DECL ((FOO) FIX)>", ctx.TRUE);
        }

        [TestMethod]
        public void TestCustomType_SEMI()
        {
            TestHelpers.EvalAndAssert(ctx, "<TYPE #SEMI \"hello world\">", ctx.GetStdAtom(StdAtom.SEMI));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE #SEMI \"hello world\" STRING>", ZilString.FromString("hello world"));

            TestHelpers.EvalAndCatch<InterpreterError>("#SEMI (foo)");

            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? #SEMI \"hello world\">", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? #SEMI \"hello world\">", ctx.FALSE);

            TestHelpers.EvalAndAssert(ctx, "<=? #SEMI \"hello\" #SEMI \"hello\">", ctx.TRUE);
        }

        [TestMethod]
        public void TestCustomType_VOC()
        {
            TestHelpers.EvalAndAssert(ctx, "<TYPE #VOC SWORD>", ctx.GetStdAtom(StdAtom.VOC));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE #VOC SWORD ATOM>", ZilAtom.Parse("SWORD", ctx));

            TestHelpers.EvalAndCatch<InterpreterError>("#VOC 1");

            TestHelpers.EvalAndAssert(ctx, "<STRUCTURED? #VOC SWORD>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<APPLICABLE? #VOC SWORD>", ctx.FALSE);

            TestHelpers.EvalAndAssert(ctx, "<=? #VOC FOO #VOC FOO>", ctx.TRUE);
        }

        [TestMethod]
        public void TestApplicableFIX()
        {
            TestHelpers.EvalAndAssert(ctx, "<SET O <LIST <FORM + 1 2>>> <1 .O>",
                new ZilForm(new ZilObject[] { ctx.GetStdAtom(StdAtom.Plus), new ZilFix(1), new ZilFix(2) }));
        }

        [TestMethod]
        public void TestEvalStructures()
        {
            TestHelpers.EvalAndAssert(ctx, "(<+ 1 2> <+ 3 4>)",
                new ZilList(new ZilObject[] { new ZilFix(3), new ZilFix(7) }));
            TestHelpers.EvalAndAssert(ctx, "[<+ 1 2> <+ 3 4>]",
                new ZilVector(new ZilFix(3), new ZilFix(7)));

            TestHelpers.EvalAndAssert(ctx, "<+ 1 2>:FIX", new ZilFix(3));

            TestHelpers.EvalAndAssert(ctx, "<>", ctx.FALSE);
        }

        [TestMethod]
        public void TestSPLICE()
        {
            TestHelpers.Evaluate(ctx, "<DEFMAC FOO () #SPLICE (4 5)>");
            TestHelpers.EvalAndAssert(ctx, "<+ <FOO>>", new ZilFix(9));

            // should only be expanded when returned from a macro
            TestHelpers.Evaluate(ctx, "<DEFINE BAR () #SPLICE (4 5)>");
            var vector = (ZilVector)TestHelpers.Evaluate(ctx, "<VECTOR <BAR>>");
            Assert.AreEqual(1, vector.GetLength());
            var first = vector[0];
            Assert.IsInstanceOfType(first, typeof(ZilSplice));

            TestHelpers.Evaluate(ctx, "<DEFINE BAZ (\"ARGS\" A) .A>");
            var list = (ZilList)TestHelpers.Evaluate(ctx, "<BAZ #SPLICE (1 2) 3>");
            Assert.AreEqual(2, ((IStructure)list).GetLength());
        }

        [TestMethod]
        public void TestNEWTYPE()
        {
            var firstname = ZilAtom.Parse("FIRSTNAME", ctx);
            var middleName = ZilAtom.Parse("MIDDLENAME", ctx);
            var lastname = ZilAtom.Parse("LASTNAME", ctx);

            TestHelpers.EvalAndAssert(ctx, "<NEWTYPE FIRSTNAME ATOM>", firstname);
            TestHelpers.EvalAndAssert(ctx, "#FIRSTNAME ALFONSO",
                new ZilHash(firstname, PrimType.ATOM, ZilAtom.Parse("ALFONSO", ctx)));
            TestHelpers.EvalAndAssert(ctx, "<=? ALFONSO #FIRSTNAME ALFONSO>", ctx.FALSE);

            TestHelpers.EvalAndAssert(ctx, "<NEWTYPE LASTNAME FIRSTNAME>", lastname);
            TestHelpers.EvalAndAssert(ctx, "#LASTNAME MCBOOMBOOM",
                new ZilHash(lastname, PrimType.ATOM, ZilAtom.Parse("MCBOOMBOOM", ctx)));
            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "#LASTNAME 5");
            TestHelpers.EvalAndAssert(ctx, "<=? #FIRSTNAME MADISON #LASTNAME MADISON>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<=? #LASTNAME SMITH #LASTNAME SMITH>", ctx.TRUE);

            TestHelpers.EvalAndCatch<InterpreterError>(ctx, "<NEWTYPE MIDDLENAME NOT-A-TYPE>");

            // optional third argument specifies a DECL that CHTYPE enforces
            TestHelpers.EvalAndAssert(ctx,
                "<NEWTYPE MIDDLENAME VECTOR '<<PRIMTYPE VECTOR> <OR STRING ATOM>>>", middleName);
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE [DANGER] MIDDLENAME>",
                new ZilStructuredHash(middleName, PrimType.VECTOR,
                    new ZilVector(ZilAtom.Parse("DANGER", ctx))));
            TestHelpers.EvalAndAssert(ctx, "<CHTYPE [\"BENDING\"] MIDDLENAME>",
                new ZilStructuredHash(middleName, PrimType.VECTOR,
                    new ZilVector(ZilString.FromString("BENDING"))));
            TestHelpers.EvalAndCatch<DeclCheckError>(ctx, "<CHTYPE [1] MIDDLENAME>");
        }

        // Adapted from an example in _The MDL Programming Language_
        const string SRomanPrint = @"
<DEFINE ROMAN-PRINT (ROMAN ""AUX"" (RNUM <CHTYPE .ROMAN FIX>))
    <COND (<OR <L=? .RNUM 0> <G? .RNUM 3999>>
           <PRINC .RNUM>)
          (T
           <RCPRINT </ .RNUM 1000> '![!\M]>
           <RCPRINT </ .RNUM  100> '![!\C !\D !\M]>
           <RCPRINT </ .RNUM   10> '![!\X !\L !\C]>
           <RCPRINT    .RNUM       '![!\I !\V !\X]>)>>

<DEFINE RCPRINT (MODN V)
    <SET MODN <MOD .MODN 10>>
    <COND (<==? 0 .MODN>)
          (<==? 1 .MODN> <PRINC <1 .V>>)
          (<==? 2 .MODN> <PRINC <1 .V>> <PRINC <1 .V>>)
          (<==? 3 .MODN> <PRINC <1 .V>> <PRINC <1 .V>> <PRINC <1 .V>>)
          (<==? 4 .MODN> <PRINC <1 .V>> <PRINC <2 .V>>)
          (<==? 5 .MODN> <PRINC <2 .V>>)
          (<==? 6 .MODN> <PRINC <2 .V>> <PRINC <1 .V>>)
          (<==? 7 .MODN> <PRINC <2 .V>> <PRINC <1 .V>> <PRINC <1 .V>>)
          (<==? 8 .MODN> <PRINC <2 .V>> <PRINC <1 .V>> <PRINC <1 .V>> <PRINC <1 .V>>)
          (<==? 9 .MODN> <PRINC <1 .V>> <PRINC <3 .V>>)>>
";

        [TestMethod]
        public void TestPRINTTYPE()
        {
            TestHelpers.Evaluate(ctx, SRomanPrint);
            TestHelpers.Evaluate(ctx, "<NEWTYPE ROMAN FIX>");
            TestHelpers.EvalAndAssert(ctx, "<PRINTTYPE ROMAN ,ROMAN-PRINT>", ZilAtom.Parse("ROMAN", ctx));
            TestHelpers.EvalAndAssert(ctx, "<==? <PRINTTYPE ROMAN> ,ROMAN-PRINT>", ctx.TRUE);

            var roman = TestHelpers.Evaluate(ctx, "#ROMAN 1984");
            Assert.AreEqual("MCMLXXXIV", roman.ToStringContext(ctx, false));

            TestHelpers.Evaluate(ctx, "<NEWTYPE ROMAN2 FIX>");
            TestHelpers.Evaluate(ctx, "<PRINTTYPE ROMAN2 ROMAN>");
            // when 2nd arg is a type that already has a custom PRINTTYPE, its current handler is copied
            TestHelpers.EvalAndAssert(ctx, "<==? <PRINTTYPE ROMAN2> ,ROMAN-PRINT>", ctx.TRUE);
            var roman2 = TestHelpers.Evaluate(ctx, "#ROMAN2 2015");
            Assert.AreEqual("MMXV", roman2.ToStringContext(ctx, false));

            TestHelpers.Evaluate(ctx, "<PRINTTYPE ROMAN ,PRINT>");
            TestHelpers.EvalAndAssert(ctx, "<=? <PRINTTYPE ROMAN> <>>", ctx.TRUE);
            Assert.AreEqual("#ROMAN 1984", roman.ToStringContext(ctx, false));

            // changing ROMAN's handler now doesn't affect ROMAN2's, because it was copied earlier
            Assert.AreEqual("MMXV", roman2.ToStringContext(ctx, false));

            TestHelpers.Evaluate(ctx, "<PRINTTYPE ROMAN2 FIX>");
            Assert.AreEqual("2015", roman2.ToStringContext(ctx, false));

            // PRINTTYPE works on built-in types too
            TestHelpers.Evaluate(ctx, "<PRINTTYPE FIX ,ROMAN-PRINT>");
            Assert.AreEqual("CXXIII", new ZilFix(123).ToStringContext(ctx, false));

            // but that doesn't affect ROMAN2, which copied FIX's default handler
            Assert.AreEqual("2015", roman2.ToStringContext(ctx, false));

            // it does affect ROMAN2 if we change it again
            TestHelpers.Evaluate(ctx, "<PRINTTYPE ROMAN2 FIX>");
            Assert.AreEqual("MMXV", roman2.ToStringContext(ctx, false));

            // the object to print should not be double-evaluated
            TestHelpers.Evaluate(ctx, "<PRINTTYPE FORM <FUNCTION (F) <PRIN1 <CHTYPE .F LIST>>>>");
            var form = TestHelpers.Evaluate(ctx, "<FORM + 1 2>");
            Assert.AreEqual("(+ I II)", form.ToStringContext(ctx, false));
        }

        [TestMethod]
        public void TestEVALTYPE()
        {
            var gritch = ZilAtom.Parse("GRITCH", ctx);

            TestHelpers.Evaluate(ctx, "<NEWTYPE GRITCH LIST>");
            TestHelpers.EvalAndAssert(ctx, "<EVALTYPE GRITCH>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<EVALTYPE GRITCH LIST>", gritch);
            TestHelpers.EvalAndAssert(ctx, "<EVALTYPE GRITCH>", ctx.GetStdAtom(StdAtom.LIST));
            TestHelpers.EvalAndAssert(ctx, "#GRITCH (A <+ 1 2 3> !<SET A \"ABC\">)",
                new ZilStructuredHash(gritch, PrimType.LIST, new ZilList(new ZilObject[]
                {
                    ZilAtom.Parse("A", ctx),
                    new ZilFix(6),
                    new ZilChar('A'),
                    new ZilChar('B'),
                    new ZilChar('C')
                })));

            TestHelpers.Evaluate(ctx, "<EVALTYPE LIST FORM>");
            TestHelpers.EvalAndAssert(ctx, "(+ 1 2)", new ZilFix(3));
        }

        [TestMethod]
        public void TestAPPLYTYPE()
        {
            var winner = ZilAtom.Parse("WINNER", ctx);

            TestHelpers.Evaluate(ctx, "<NEWTYPE WINNER LIST>");
            TestHelpers.EvalAndAssert(ctx, "<APPLYTYPE WINNER>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<APPLYTYPE WINNER <FUNCTION (W \"TUPLE\" T) (!.W !.T)>>", winner);
            TestHelpers.EvalAndAssert(ctx, "<#WINNER (A B C) <+ 1 2> q>",
                new ZilList(new ZilObject[]
                {
                    ZilAtom.Parse("A", ctx),
                    ZilAtom.Parse("B", ctx),
                    ZilAtom.Parse("C", ctx),
                    new ZilFix(3),
                    ZilAtom.Parse("q", ctx)
                }));
        }

        [ItemNotNull]
        [NotNull]
        static IEnumerable<Type> GetConcreteZilObjectTypes()
        {
            return from t in typeof(ZilObject).Assembly.GetTypes()
                   where typeof(ZilObject).IsAssignableFrom(t) && !t.IsAbstract
                   select t;
        }

        [TestMethod]
        public void All_ZilObject_Classes_Have_A_Builtin_Attribute()
        {
            var typesMissingAttribute =
                from t in GetConcreteZilObjectTypes()
                let builtinTypeAttrs = t.GetCustomAttributes(typeof(BuiltinTypeAttribute), false)
                let builtinAltAttrs = t.GetCustomAttributes(typeof(BuiltinAlternateAttribute), false)
                let builtinMetaAttrs = t.GetCustomAttributes(typeof(BuiltinMetaAttribute), false)
                where builtinTypeAttrs.Length + builtinAltAttrs.Length + builtinMetaAttrs.Length != 1
                select t.Name;

            var missingList = string.Join(", ", typesMissingAttribute);
            Assert.AreEqual("", missingList,
                $"Some {nameof(ZilObject)} classes are missing {nameof(BuiltinTypeAttribute)}/" +
                $"{nameof(BuiltinAlternateAttribute)}/{nameof(BuiltinMetaAttribute)}");

            var alternatesWithBadMainTypes =
                from t in GetConcreteZilObjectTypes()
                let builtinAltAttrs = t.GetCustomAttributes(typeof(BuiltinAlternateAttribute), false)
                where builtinAltAttrs.Length > 0
                let mainType = ((BuiltinAlternateAttribute)builtinAltAttrs[0]).MainType
                let mainAttrs = mainType.GetCustomAttributes(typeof(BuiltinTypeAttribute), false)
                where mainAttrs.Length == 0
                select new { AlternateType = t.Name, MainType = mainType.Name };

            var alternatesBadList = string.Join(
                ", ",
                alternatesWithBadMainTypes.Select(
                    p => $"{p.AlternateType} (main type {p.MainType})"));

            Assert.AreEqual("", alternatesBadList,
                $"Some {nameof(ZilObject)} classes with {nameof(BuiltinAlternateAttribute)} " +
                $"point to main types without {nameof(BuiltinTypeAttribute)}");
        }

        [TestMethod]
        public void All_ZilObject_Classes_With_Structured_PrimTypes_Implement_IStructure()
        {
            bool IsStructuredPrimType(PrimType pt)
            {
                switch (pt)
                {
                    case PrimType.LIST:
                    case PrimType.VECTOR:
                        return true;

                    //case PrimType.STRING:     // ZilSubr and ZilFSubr shouldn't be structured, come on
                    default:
                        return false;
                }
            }

            var typesWithPrimTypes =
                (from t in GetConcreteZilObjectTypes()
                 where !typeof(IStructure).IsAssignableFrom(t)
                 let builtinTypeAttrs = t.GetCustomAttributes(typeof(BuiltinTypeAttribute), false)
                 where builtinTypeAttrs.Length == 1
                 let attr = (BuiltinTypeAttribute)builtinTypeAttrs[0]
                 select new { attr.PrimType, Type = t })
                .ToArray();

            var typesMissingIStructure =
                from t in typesWithPrimTypes
                where IsStructuredPrimType(t.PrimType) && !typeof(IStructure).IsAssignableFrom(t.Type)
                select t.Type.Name;

            var missingList = string.Join(", ", typesMissingIStructure);
            Assert.AreEqual("", missingList,
                $"Some {nameof(ZilObject)} classes with structured PRIMTYPEs do not implement {nameof(IStructure)}");

            var unexpectedlyStructuredTypes =
                from t in typesWithPrimTypes
                where !IsStructuredPrimType(t.PrimType) && typeof(IStructure).IsAssignableFrom(t.Type)
                select t.Type.Name;

            var unexpectedList = string.Join(", ", unexpectedlyStructuredTypes);
            Assert.AreEqual("", unexpectedList,
                $"Some {nameof(ZilObject)} classes with non-structured PRIMTYPEs unexpectedly implement {nameof(IStructure)}");
        }

        [TestMethod]
        public void VECTOR_Can_Be_ChTyped_To_TABLE()
        {
            var table = (ZilTable)TestHelpers.Evaluate(ctx, "<CHTYPE [1 2 3] TABLE>");

            Assert.AreEqual(3, table.ElementCount);

            var array = new ZilObject[3];
            table.CopyTo(array, (zo, isWord) => zo, null, ctx);

            var expected = new ZilObject[]
            {
                new ZilFix(1),
                new ZilFix(2),
                new ZilFix(3)
            };

            TestHelpers.AssertStructurallyEqual(expected, array, "Unexpected table contents");
        }

        [TestMethod]
        public void TestALLTYPES_And_VALID_TYPE_P()
        {
            string[] expectedTypes =
            {
                "FIX", "SUBR", "FSUBR", "FUNCTION", "MACRO", "ADECL", "ATOM", "CHARACTER",
                "FALSE", "LIST", "FORM", "STRING", "SEGMENT", "VECTOR", "OFFSET", "WACKY",

                "OBLIST", "ACTIVATION", "ENVIRONMENT", "CHANNEL", "ROUTINE", "CONSTANT",
                "GLOBAL", "TABLE", "OBJECT"
            };

            const string unexpectedType = "NOT-A-TYPE";

            var allTypes = TestHelpers.Evaluate(ctx, "<ALLTYPES>");

            Assert.IsInstanceOfType(allTypes, typeof(ZilVector));

            var allTypesVector = (ZilVector)allTypes;
            var returnedTypes = new HashSet<ZilAtom>();

            var len = allTypesVector.GetLength();
            for (int i = 0; i < len; i++)
            {
                var item = allTypesVector[i];
                Assert.IsInstanceOfType(item, typeof(ZilAtom));
                returnedTypes.Add((ZilAtom)item);
            }

            foreach (var typeName in expectedTypes)
            {
                var atom = ZilAtom.Parse(typeName, ctx);
                Assert.IsTrue(returnedTypes.Contains(atom), "expected {0} to be in <ALLTYPES>", typeName);

                TestHelpers.EvalAndAssert(ctx, $"<VALID-TYPE? {typeName}>", atom);
            }

            Assert.IsFalse(returnedTypes.Contains(ZilAtom.Parse(unexpectedType, ctx)));

            TestHelpers.EvalAndAssert(ctx, $"<VALID-TYPE? {unexpectedType}>", ctx.FALSE);
        }

        [TestMethod]
        public void TestLEGAL_P_For_ACTIVATION()
        {
            TestHelpers.Evaluate(ctx, "<DEFINE FOO ACT () <SETG ACT .ACT> <LEGAL? .ACT>>");

            TestHelpers.EvalAndAssert(ctx, "<FOO>", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<LEGAL? ,ACT>", ctx.FALSE);
        }

        [TestMethod]
        public void TestLEGAL_P_For_ENVIRONMENT()
        {
            TestHelpers.Evaluate(ctx, "<DEFINE FOO () <BAR>>");
            TestHelpers.Evaluate(ctx, "<DEFINE BAR (\"BIND\" ENV) <SETG ENV .ENV> <LEGAL? .ENV>>");

            // with the call to FOO, BAR gets FOO's environment, which expires after the call returns
            TestHelpers.EvalAndAssert(ctx, "<FOO>", ctx.TRUE);
            GC.Collect();
            TestHelpers.EvalAndAssert(ctx, "<LEGAL? ,ENV>", ctx.FALSE);

            // without the call to FOO, BAR gets the root environment, which won't expire
            TestHelpers.EvalAndAssert(ctx, "<BAR>", ctx.TRUE);
            GC.Collect();
            TestHelpers.EvalAndAssert(ctx, "<LEGAL? ,ENV>", ctx.TRUE);
        }

        [TestMethod]
        public void FORM_And_LIST_Should_Not_Be_Equal()
        {
            TestHelpers.EvalAndAssert(ctx, "<=? '(1 2 3) '<1 2 3>>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<=? '<1 2 3> '(1 2 3)>", ctx.FALSE);
        }

        [TestMethod]
        public void LVAL_And_GVAL_Should_Use_Value_Comparison_For_Eeq()
        {
            TestHelpers.EvalAndAssert(ctx, "<==? ',FOO ',FOO>", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<==? '.FOO '.FOO>", ctx.TRUE);

            TestHelpers.EvalAndAssert(ctx, "<==? '.FOO ',FOO>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<==? '.FOO '<LVAL FOO X>>", ctx.FALSE);

            // and the inverse for N==?
            TestHelpers.EvalAndAssert(ctx, "<N==? ',FOO ',FOO>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<N==? '.FOO '.FOO>", ctx.FALSE);

            TestHelpers.EvalAndAssert(ctx, "<N==? '.FOO ',FOO>", ctx.TRUE);
            TestHelpers.EvalAndAssert(ctx, "<N==? '.FOO '<LVAL FOO X>>", ctx.TRUE);
        }

        [TestMethod]
        public void TYPE_P_Should_Handle_LVAL_And_GVAL()
        {
            TestHelpers.EvalAndAssert(ctx, "<TYPE? '.X LVAL>", ctx.GetStdAtom(StdAtom.LVAL));
            TestHelpers.EvalAndAssert(ctx, "<TYPE? ',X LVAL>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<TYPE? '.X GVAL>", ctx.FALSE);
            TestHelpers.EvalAndAssert(ctx, "<TYPE? ',X GVAL>", ctx.GetStdAtom(StdAtom.GVAL));
        }

        [TestMethod]
        public void Accessing_Elements_Of_FORM_Should_Work()
        {
            TestHelpers.Evaluate(ctx, "<SET X '.A>");
            TestHelpers.EvalAndAssert(ctx, "<1 .X>", ctx.GetStdAtom(StdAtom.LVAL));
            TestHelpers.Evaluate(ctx, "<1 .X EVAL>");
            TestHelpers.EvalAndAssert(ctx, "<1 .X>", ctx.GetStdAtom(StdAtom.EVAL));
            TestHelpers.Evaluate(ctx, "<PUT .X 1 GVAL>");
            TestHelpers.EvalAndAssert(ctx, "<1 .X>", ctx.GetStdAtom(StdAtom.GVAL));
            TestHelpers.Evaluate(ctx, "<2 .X Y>");
            TestHelpers.EvalAndAssert(ctx, "<2 .X>", ZilAtom.Parse("Y", ctx));
        }

        [TestMethod]
        public void REST_0_Should_Return_Primitive_Type()
        {
            TestHelpers.EvalAndAssert(ctx, "<REST '<1> 0>", new ZilList(new ZilFix(1), new ZilList(null, null)));
        }
    }
}
