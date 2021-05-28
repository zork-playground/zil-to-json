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

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Zilf.Tests.Integration
{
    [TestClass, TestCategory("Code Generation")]
    public class CodeGenTests : IntegrationTestClass
    {
        [TestMethod]
        public void TestAddToVariable()
        {
            AssertRoutine("\"AUX\" X Y", "<SET X <+ .X .Y>>")
                .GeneratesCodeMatching(@"ADD X,Y >X\r\n\s*RETURN X");
        }

        [TestMethod]
        public void TestAddInVoidContextBecomesINC()
        {
            AssertRoutine("\"AUX\" X", "<SET X <+ .X 1>> .X")
                .GeneratesCodeMatching(@"INC 'X\r\n\s*RETURN X");
        }

        [TestMethod]
        public void TestAddInValueContextBecomesINC()
        {
            AssertRoutine("\"AUX\" X", "<SET X <+ .X 1>>")
                .GeneratesCodeMatching(@"INC 'X\r\n\s*RETURN X");
        }

        [TestMethod]
        public void TestSubtractInVoidContextThenLessBecomesDLESS()
        {
            AssertRoutine("\"AUX\" X", "<SET X <- .X 1>> <COND (<L? .X 0> <PRINTI \"blah\">)>")
                .GeneratesCodeMatching(@"DLESS\? 'X,0");
        }

        [TestMethod]
        public void TestSubtractInValueContextThenLessBecomesDLESS()
        {
            AssertRoutine("\"AUX\" X", "<COND (<L? <SET X <- .X 1>> 0> <PRINTI \"blah\">)>")
                .GeneratesCodeMatching(@"DLESS\? 'X,0");
        }

        [TestMethod]
        public void TestRoutineResultIntoVariable()
        {
            AssertRoutine("\"AUX\" FOO", "<SET FOO <WHATEVER>>")
                .WithGlobal("<ROUTINE WHATEVER () 123>")
                .InV3()
                .GeneratesCodeMatching("CALL WHATEVER >FOO");
        }

        [TestMethod]
        public void TestPrintiCrlfRtrueBecomesPRINTR()
        {
            AssertRoutine("", "<PRINTI \"hi\"> <CRLF> <RTRUE>")
                .GeneratesCodeMatching("PRINTR \"hi\"");
        }

        [TestMethod]
        public void TestAdjacentEqualsCombine()
        {
            AssertRoutine("\"AUX\" X", "<COND (<OR <=? .X 1> <=? .X 2>> <RTRUE>)>")
                .GeneratesCodeMatching(@"EQUAL\? X,1,2 /TRUE");
            AssertRoutine("\"AUX\" X", "<COND (<OR <EQUAL? .X 1 2> <EQUAL? .X 3 4>> <RTRUE>)>")
                .GeneratesCodeMatching(@"EQUAL\? X,1,2,3 /TRUE");
            AssertRoutine("\"AUX\" X", "<COND (<OR <EQUAL? .X 1 2 3> <=? .X 4> <EQUAL? .X 5 6>> <RTRUE>)>")
                .GeneratesCodeMatching(@"EQUAL\? X,1,2,3 /TRUE\r\n\s*EQUAL\? X,4,5,6 /TRUE");
        }

        [TestMethod]
        public void TestEqualZeroBecomesZERO_P()
        {
            AssertRoutine("\"AUX\" X", "<COND (<=? .X 0> <RTRUE>)>")
                .GeneratesCodeMatching(@"ZERO\? X /TRUE");
            AssertRoutine("\"AUX\" X", "<COND (<=? 0 .X> <RTRUE>)>")
                .GeneratesCodeMatching(@"ZERO\? X /TRUE");
        }

        [TestMethod]
        public void TestAdjacentEqualsCombineEvenIfZero()
        {
            AssertRoutine("\"AUX\" X", "<COND (<OR <=? .X 0> <=? .X 2>> <RTRUE>)>")
                .GeneratesCodeMatching(@"EQUAL\? X,0,2 /TRUE");
            AssertRoutine("\"AUX\" X", "<COND (<OR <=? .X 0> <=? .X 0>> <RTRUE>)>")
                .GeneratesCodeMatching(@"EQUAL\? X,0,0 /TRUE");
            AssertRoutine("\"AUX\" X", "<COND (<OR <EQUAL? .X 1 2> <=? .X 0> <EQUAL? .X 3 4>> <RTRUE>)>")
                .GeneratesCodeMatching(@"EQUAL\? X,1,2,0 /TRUE");
            AssertRoutine("\"AUX\" X", "<COND (<OR <EQUAL? .X 1 2> <EQUAL? .X 3 0>> <RTRUE>)>")
                .GeneratesCodeMatching(@"EQUAL\? X,1,2,3 /TRUE\r\n\s*ZERO\? X /TRUE");
        }

        [TestMethod]
        public void TestValuePredicateContext()
        {
            AssertRoutine("\"AUX\" X Y", "<COND (<NOT <SET X <FIRST? .Y>>> <RTRUE>)>")
                .GeneratesCodeMatching(@"FIRST\? Y >X \\TRUE");
            AssertRoutine("\"AUX\" X Y", "<COND (<NOT .Y> <SET X <>>) (T <SET X <FIRST? .Y>>)> <OR .X <RTRUE>>")
                .GeneratesCodeMatching(@"FIRST\? Y >X (?![/\\]TRUE)");
        }

        [TestMethod]
        public void TestValuePredicateContext_Calls()
        {
            AssertRoutine("\"AUX\" X", "<COND (<NOT <SET X <FOO>>> <RTRUE>)>")
                .WithGlobal("<ROUTINE FOO () <>>")
                .GeneratesCodeMatching(@"CALL FOO >X\r\n\s*ZERO\? X /TRUE");
        }

        [TestMethod]
        public void TestValuePredicateContext_Constants()
        {
            AssertRoutine("\"AUX\" X", "<COND (<NOT <SET X <>>> <RTRUE>)>")
                .GeneratesCodeMatching(@"SET 'X,0\r\n\s*RTRUE");
            AssertRoutine("\"AUX\" X", "<COND (<NOT <SET X 0>> <RTRUE>)>")
                .GeneratesCodeMatching(@"SET 'X,0\r\n\s*RTRUE");
            AssertRoutine("\"AUX\" X", "<COND (<NOT <SET X 100>> <RTRUE>)>")
                .GeneratesCodeMatching(@"SET 'X,100\r\n\s*RFALSE");
            AssertRoutine("\"AUX\" X", "<COND (<NOT <SET X T>> <RTRUE>)>")
                .GeneratesCodeMatching(@"SET 'X,1\r\n\s*RFALSE");
            AssertRoutine("\"AUX\" X", "<COND (<NOT <SET X \"blah\">> <RTRUE>)>")
                .GeneratesCodeMatching(@"SET 'X,STR\?\d+\r\n\s*RFALSE");
        }

        [TestMethod]
        public void TestMergeAdjacentTerminators()
        {
            AssertRoutine("OBJ \"AUX\" (CNT 0) X",
@"<COND (<SET X <FIRST? .OBJ>>
	<REPEAT ()
		<SET CNT <+ .CNT 1>>
		<COND (<NOT <SET X <NEXT? .X>>> <RETURN>)>>)>
.CNT").WhenCalledWith("<>")
      .GeneratesCodeMatching(@"NEXT\? X >X /\?L\d+\r\n\s*\?L\d+:\s*RETURN CNT\r\n\r\n");
        }

        [TestMethod]
        public void TestBranchToSameCondition()
        {
            AssertRoutine("\"AUX\" P",
@"<OBJECTLOOP I ,HERE
    <COND (<AND <NOT <FSET? .I ,TOUCHBIT>> <SET P <GETP .I ,P?FDESC>>>
		   <PRINT .P> <CRLF>)>>")
                           .WithGlobal(@"<DEFMAC OBJECTLOOP ('VAR 'LOC ""ARGS"" BODY)
    <FORM REPEAT <LIST <LIST .VAR <FORM FIRST? .LOC>>>
        <FORM COND
            <LIST <FORM LVAL .VAR>
                !.BODY
                <FORM SET .VAR <FORM NEXT? <FORM LVAL .VAR>>>>
            '(ELSE <RETURN>)>>>")
                           .WithGlobal("<GLOBAL HERE <>>")
                           .WithGlobal("<GLOBAL TOUCHBIT <>>")
                           .WithGlobal("<GLOBAL P?FDESC <>>")
                           .GeneratesCodeMatching(@"\A(?:(?!ZERO I\?).)*PRINT P(?:(?!ZERO\? I).)*\Z");
        }

        [TestMethod]
        public void TestReturnOrWithPred()
        {
            AssertRoutine("\"AUX\" X", "<OR <EQUAL? .X 123> <FOO>>")
                .WithGlobal("<ROUTINE FOO () <>>")
                .GeneratesCodeNotMatching(@"PUSH|ZERO\?");
        }

        [TestMethod]
        public void TestSetOrWithPred()
        {
            AssertRoutine("\"AUX\" X Y", "<SET Y <OR <EQUAL? .X 123> <FOO>>>")
                .WithGlobal("<ROUTINE FOO () <>>")
                .GeneratesCodeNotMatching(@"ZERO\?");
        }

        [TestMethod]
        public void TestPrintrOverBranch_1()
        {
            AssertRoutine("\"AUX\" X", "<COND (.X <PRINTI \"foo\">) (T <PRINTI \"bar\">)> <CRLF> <RTRUE>")
                .GeneratesCodeMatching("PRINTR \"foo\".*PRINTR \"bar\"");

        }
        [TestMethod]
        public void TestPrintrOverBranch_2()
        {
            AssertRoutine("\"AUX\" X", "<COND (.X <PRINTI \"foo\"> <CRLF>) (T <PRINTI \"bar\"> <CRLF>)> <RTRUE>")
                .GeneratesCodeMatching("PRINTR \"foo\".*PRINTR \"bar\"");
        }

        [TestMethod]
        public void TestSimpleAND_1()
        {
            AssertRoutine("\"AUX\" A", "<AND .A <FOO>>")
                .WithGlobal("<ROUTINE FOO () <>>")
                .GeneratesCodeNotMatching(@"\?TMP");
        }

        [TestMethod]
        public void TestSimpleAND_2()
        {
            AssertRoutine("\"AUX\" A", "<AND <OR <0? .A> <FOO>> <BAR>>")
                .WithGlobal("<ROUTINE FOO () <>>")
                .WithGlobal("<ROUTINE BAR () <>>")
                .GeneratesCodeNotMatching(@"\?TMP");
        }

        [TestMethod]
        public void TestSimpleOR_1()
        {
            AssertRoutine("\"AUX\" A", "<OR .A <FOO>>")
                .WithGlobal("<ROUTINE FOO () <>>")
                .GeneratesCodeNotMatching(@"\?TMP");
        }

        [TestMethod]
        public void TestSimpleOR_2()
        {
            AssertRoutine("\"AUX\" OBJ", "<OR <FIRST? .OBJ> <FOO>>")
                .WithGlobal("<ROUTINE FOO () <>>")
                .GeneratesCodeMatching(@"RETURN \?TMP.*RSTACK");
        }

        [TestMethod]
        public void TestSimpleOR_3()
        {
            AssertRoutine("\"AUX\" A", "<OR <SET A <FOO>> <BAR>>")
                .WithGlobal("<ROUTINE FOO () <>>")
                .WithGlobal("<ROUTINE BAR () <>>")
                .GeneratesCodeNotMatching(@"\?TMP");
        }

        [TestMethod]
        public void TestNestedTempVariables()
        {
            // this code should use 3 temp variables:
            // ?TMP is the value of GLOB before calling FOO
            // ?TMP?1 is the value returned by FOO
            // ?TMP?2 is the value of GLOB before calling BAR

            AssertRoutine("\"AUX\" X", @"<PUT ,GLOB <FOO> <+ .X <GET ,GLOB <BAR>>>>")
                .WithGlobal("<GLOBAL GLOB <>>")
                .WithGlobal("<ROUTINE FOO () <>>")
                .WithGlobal("<ROUTINE BAR () <>>")
                .GeneratesCodeMatching(@"\?TMP\?2");
        }

        [TestMethod]
        public void TestNestedBindVariables()
        {
            AssertRoutine("", @"<BIND (X)
                                  <SET X 0>
                                  <BIND (X)
                                    <SET X 1>
                                    <BIND (X)
                                      <SET X 2>>>>")
                .GeneratesCodeMatching(@"X\?2");
        }

        [TestMethod]
        public void TestTimeHeader_V3()
        {
            AssertRoutine("", "<>")
                .WithVersionDirective("<VERSION ZIP TIME>")
                .GeneratesCodeMatching(@"^\s*\.TIME\s*$");
        }

        [TestMethod]
        public void TestSoundHeader_V3()
        {
            AssertRoutine("", "<>")
                .WithGlobal("<SETG SOUND-EFFECTS? T>")
                .InV3()
                .GeneratesCodeMatching(@"^\s*\.SOUND\s*$");
        }

        [TestMethod]
        public void TestCleanStack_V3_NoClean()
        {
            AssertRoutine("", "<FOO> 456")
                .WithGlobal("<ROUTINE FOO () 123>")
                .InV3()
                .GeneratesCodeNotMatching(@"FSTACK");
        }

        [TestMethod]
        public void TestCleanStack_V3_Clean()
        {
            AssertRoutine("", "<FOO> 456")
                .WithGlobal("<FILE-FLAGS CLEAN-STACK?>")
                .WithGlobal("<ROUTINE FOO () 123>")
                .InV3()
                .GeneratesCodeMatching(@"FSTACK");
        }

        [TestMethod]
        public void TestCleanStack_V4_NoClean()
        {
            AssertRoutine("", "<FOO> 456")
                .WithGlobal("<ROUTINE FOO () 123>")
                .InV4()
                .GeneratesCodeNotMatching(@"FSTACK");
        }

        [TestMethod]
        public void TestCleanStack_V4_Clean()
        {
            AssertRoutine("", "<FOO> 456")
                .WithGlobal("<FILE-FLAGS CLEAN-STACK?>")
                .WithGlobal("<ROUTINE FOO () 123>")
                .InV4()
                .GeneratesCodeMatching(@"FSTACK");
        }

        [TestMethod]
        public void TestReuseTemp()
        {
            // the first G? allocates one temp var, then releases it afterward
            // the BIND consumes the same temp var and binds it to a new atom
            // the second G? allocates a new temp var, which must not collide with the first
            AssertRoutine("",
                "<COND (<G? <FOO> <BAR>> <RTRUE>)> " +
                "<BIND ((Z 0)) <COND (<G? <BAR> <FOO>> <RFALSE>)>>")
                .WithGlobal("<ROUTINE FOO () 123>")
                .WithGlobal("<ROUTINE BAR () 456>")
                .Compiles();
        }

        [TestMethod]
        public void TestNoTempForSet()
        {
            // this shouldn't use any temp vars, since the expressions are going into named variables
            AssertRoutine("\"AUX\" X Y",
                "<COND (<G? <SET X <FOO>> <SET Y <BAR>>> <RTRUE>)> " +
                "<BIND ((Z 0)) <COND (<G? <SET X <BAR>> <SET Y <FOO>>> <RFALSE>)>>")
                .WithGlobal("<ROUTINE FOO () 123>")
                .WithGlobal("<ROUTINE BAR () 456>")
                .GeneratesCodeNotMatching(@"\?TMP");

            // this one should, since X is modified in a subsequent arg
            AssertRoutine("\"AUX\" X Y",
                "<COND (<G? <SET X <FOO>> <SET Y <SET X <BAR>>>> <RTRUE>)> " +
                "<BIND ((Z 0)) <COND (<G? <SET X <BAR>> <SET Y <SET X <FOO>>>> <RFALSE>)>>")
                .WithGlobal("<ROUTINE FOO () 123>")
                .WithGlobal("<ROUTINE BAR () 456>")
                .GeneratesCodeMatching(@"\?TMP");
        }

        [TestMethod]
        public void CHRSET_Should_Generate_Directive()
        {
            AssertGlobals(
                "<CHRSET 0 \"zyxwvutsrqponmlkjihgfedcba\">")
                .InV5()
                .GeneratesCodeMatching(@"\.CHRSET 0,122,121,120,119,118,117,116,115,114,113,112,111,110,109,108,107,106,105,104,103,102,101,100,99,98,97");
        }

        [TestMethod]
        public void Table_And_Verb_Names_Should_Be_Sanitized()
        {
            AssertGlobals(
                @"<SYNTAX \,TELL = V-TELL>",
                "<ROUTINE V-TELL () <>>",
                @"<CONSTANT \,TELLTAB1 <ITABLE 1>>",
                @"<GLOBAL \,TELLTAB2 <ITABLE 1>>")
                .GeneratesCodeNotMatching(@",TELL");
        }

        [TestMethod]
        public void Constant_Arithmetic_Operations_Should_Be_Folded()
        {
            // binary operators
            AssertRoutine("",
                "<+ 1 <* 2 3> <* 4 5>>")
                .GeneratesCodeMatching("RETURN 27");

            AssertRoutine("",
                "<+ ,EIGHT ,SIXTEEN>")
                .WithGlobal("<CONSTANT EIGHT 8>")
                .WithGlobal("<CONSTANT SIXTEEN 16>")
                .GeneratesCodeMatching("RETURN 24");

            AssertRoutine("",
                "<MOD 1000 16>")
                .GeneratesCodeMatching("RETURN 8");

            AssertRoutine("",
                "<ASH -32768 -2>")
                .InV5()
                .GeneratesCodeMatching("RETURN -8192");

            AssertRoutine("",
                "<LSH -32768 -2>")
                .InV5()
                .GeneratesCodeMatching("RETURN 8192");

            AssertRoutine("",
                "<XORB 25 -1>")
                .GeneratesCodeMatching("RETURN -26");

            // unary operators
            AssertRoutine("",
                "<BCOM 123>")
                .GeneratesCodeMatching("RETURN -124");
        }

        [TestMethod]
        public void Constant_Comparisons_Should_Be_Folded()
        {
            // unary comparisons
            AssertRoutine("",
                "<0? ,FALSE-VALUE>")
                .GeneratesCodeMatching(@"\.FUNCT TEST\?ROUTINE\r?\n\s*RTRUE");

            AssertRoutine("",
                "<1? <- 6 5>>")
                .GeneratesCodeMatching(@"\.FUNCT TEST\?ROUTINE\r?\n\s*RTRUE");

            AssertRoutine("",
                "<T? <+ 1 2 3>>")
                .GeneratesCodeMatching(@"\.FUNCT TEST\?ROUTINE\r?\n\s*RTRUE");

            AssertRoutine("",
                "<F? <+ 1 2 3>>")
                .GeneratesCodeMatching(@"\.FUNCT TEST\?ROUTINE\r?\n\s*RFALSE");

            AssertRoutine("",
                "<NOT <- 6 4 2>>")
                .GeneratesCodeMatching(@"\.FUNCT TEST\?ROUTINE\r?\n\s*RTRUE");

            // binary comparisons
            AssertRoutine("",
                "<L? 1 10>")
                .GeneratesCodeMatching(@"\.FUNCT TEST\?ROUTINE\r?\n\s*RTRUE");

            AssertRoutine("",
                "<G=? ,FALSE-VALUE ,TRUE-VALUE>")
                .GeneratesCodeMatching(@"\.FUNCT TEST\?ROUTINE\r?\n\s*RFALSE");

            AssertRoutine("",
                "<BTST <+ 64 32 8> <+ 32 8>>")
                .GeneratesCodeMatching(@"\.FUNCT TEST\?ROUTINE\r?\n\s*RTRUE");

            AssertRoutine("",
                "<BTST <+ 64 32 8> <+ 16 8>>")
                .GeneratesCodeMatching(@"\.FUNCT TEST\?ROUTINE\r?\n\s*RFALSE");

            // varargs equality comparisons
            AssertRoutine("",
                "<=? 50 10 <- 100 50> 100>")
                .GeneratesCodeMatching(@"\.FUNCT TEST\?ROUTINE\r?\n\s*RTRUE");

            AssertRoutine("",
                "<=? 49 10 <- 100 50> 100>")
                .GeneratesCodeMatching(@"\.FUNCT TEST\?ROUTINE\r?\n\s*RFALSE");

            // here we still have to call the function to get its side effect, but we can ignore its result
            AssertRoutine("",
                "<=? 50 10 <- 100 50> <FOO>>")
                .WithGlobal("<FILE-FLAGS CLEAN-STACK?>")
                .WithGlobal("<ROUTINE FOO () 100>")
                .GeneratesCodeMatching(@"\.FUNCT TEST\?ROUTINE\r?\n\s*CALL FOO >STACK\r?\n\s*FSTACK\r?\n\s*RTRUE");

            // here we can't simplify the branch, because <FOO> might return 49, but we can skip testing the constants
            AssertRoutine("",
                "<=? 49 10 <- 100 50> <FOO>>")
                .WithGlobal("<FILE-FLAGS CLEAN-STACK?>")
                .WithGlobal("<ROUTINE FOO () 100>")
                .GeneratesCodeMatching(@"EQUAL\? 49,STACK (/TRUE|\\FALSE)");

            AssertRoutine("",
                "<=? 49 1 <FOO> 2 <FOO> 3 <FOO> 4 <FOO> 5>")
                .WithGlobal("<FILE-FLAGS CLEAN-STACK?>")
                .WithGlobal("<ROUTINE FOO () 100>")
                .GeneratesCodeMatching(@"EQUAL\? 49(,(STACK|\?TMP(\?\d+)?)){3} /TRUE\r?\n\s*EQUAL\? 49,(STACK|\?TMP(\?\d+)?) (/TRUE|\\FALSE)");
        }

        [TestMethod]
        public void BAND_In_Predicate_Context_With_Power_Of_Two_Should_Be_Optimized()
        {
            AssertRoutine("\"AUX\" X",
                "<COND (<BAND .X 4> <RTRUE>)>")
                .GeneratesCodeMatching(@"BTST X,4 (/TRUE|\\FALSE)");

            AssertRoutine("\"AUX\" X",
                "<COND (<BAND 4 .X> <RTRUE>)>")
                .GeneratesCodeMatching(@"BTST X,4 (/TRUE|\\FALSE)");

            // BAND with zero is never true
            AssertRoutine("\"AUX\" X",
                "<COND (<BAND 0 .X> <RTRUE>)>")
                .GeneratesCodeMatching("RFALSE");

            AssertRoutine("\"AUX\" X",
                "<COND (<BAND .X 0> <RTRUE>)>")
                .GeneratesCodeMatching("RFALSE");

            // doesn't work with non-powers-of-two
            AssertRoutine("\"AUX\" X",
                "<COND (<BAND .X 6> <RTRUE>)>")
                .GeneratesCodeMatching(@"BAND X,6 >STACK\r?\n\s*ZERO\? STACK (\\TRUE|/FALSE)");
        }

        [TestMethod]
        public void Stacked_BAND_Or_BOR_Should_Collapse()
        {
            AssertRoutine("\"AUX\" X",
                "<BAND 48 <BAND .X 96>>")
                .GeneratesCodeMatching("BAND X,32 >STACK");

            AssertRoutine("\"AUX\" X",
                "<BOR <BOR 96 .X> 48>")
                .GeneratesCodeMatching("BOR X,112 >STACK");
        }

        [TestMethod]
        public void Predicate_Inside_BIND_Does_Not_Rely_On_PUSH()
        {
            AssertRoutine("\"AUX\" X",
                "<COND (<BIND ((Y <* 2 .X>)) <G? .Y 123>> <RTRUE>)>")
                .GeneratesCodeMatching(@"GRTR\? Y,123 (/TRUE|\\FALSE)");
        }

        [TestMethod]
        public void POP_In_V6_Stores()
        {
            AssertRoutine("\"AUX\" X",
                "<SET X <POP>>")
                .InV6()
                .GeneratesCodeMatching(@"POP >X");
        }

        [TestMethod]
        public void IndirectStore_From_Stack_In_V5_Uses_POP()
        {
            AssertRoutine("\"AUX\" X",
                "<SETG .X <FOO>> <RTRUE>")
                .WithGlobal("<ROUTINE FOO () <>>")
                .InV5()
                .GeneratesCodeMatching(@"POP X");
        }

        [TestMethod]
        public void IndirectStore_From_Stack_In_V6_Uses_SET()
        {
            AssertRoutine("\"AUX\" X",
                "<SETG .X <FOO>> <RTRUE>")
                .WithGlobal("<ROUTINE FOO () <>>")
                .InV6()
                .GeneratesCodeMatching(@"SET X,STACK");
        }

        [TestMethod]
        public void OR_In_Value_Context_Avoids_Unnecessary_Value_Preservation()
        {
            AssertRoutine("OBJ", @"
    <OR ;""We can always see the contents of surfaces""
            <FSET? .OBJ ,SURFACEBIT>
            ;""We can see inside containers if they're open, transparent, or
              unopenable (= always-open)""
            <AND <FSET? .OBJ ,CONTBIT>
                 <OR <FSET? .OBJ ,OPENBIT>
                     <FSET? .OBJ ,TRANSBIT>
                     <NOT <FSET? .OBJ ,OPENABLEBIT>>>>>")
                .WithGlobal("<CONSTANT SURFACEBIT 1>")
                .WithGlobal("<CONSTANT CONTBIT 2>")
                .WithGlobal("<CONSTANT OPENBIT 3>")
                .WithGlobal("<CONSTANT TRANSBIT 4>")
                .WithGlobal("<CONSTANT OPENABLEBIT 5>")
                .WhenCalledWith("<>")
                .GeneratesCodeNotMatching(@"\?TMP");
        }

        [TestMethod]
        public void AND_In_Value_Context_Avoids_Unnecessary_Value_Preservation()
        {
            AssertRoutine("OBJ", @"
    <AND ;""We can always see the contents of surfaces""
            <FSET? .OBJ ,SURFACEBIT>
            ;""We can see inside containers if they're open, transparent, or
              unopenable (= always-open)""
            <AND <FSET? .OBJ ,CONTBIT>
                 <OR <FSET? .OBJ ,OPENBIT>
                     <FSET? .OBJ ,TRANSBIT>
                     <NOT <FSET? .OBJ ,OPENABLEBIT>>>>>")
                .WithGlobal("<CONSTANT SURFACEBIT 1>")
                .WithGlobal("<CONSTANT CONTBIT 2>")
                .WithGlobal("<CONSTANT OPENBIT 3>")
                .WithGlobal("<CONSTANT TRANSBIT 4>")
                .WithGlobal("<CONSTANT OPENABLEBIT 5>")
                .WhenCalledWith("<>")
                .GeneratesCodeNotMatching(@"\?TMP");
        }

        [TestMethod]
        public void ZREST_With_Constant_Table_Uses_Assembler_Math()
        {
            AssertRoutine("", "<REST ,MY-TABLE 2>")
                .WithGlobal("<CONSTANT MY-TABLE <TABLE 1 2 3 4>>")
                .GeneratesCodeMatching(@"MY-TABLE\+2");
        }

        [TestMethod]
        public void VALUE_VARNAME_Does_Not_Use_An_Instruction()
        {
            AssertRoutine("", "<VALUE MY-GLOBAL>")
                .WithGlobal("<GLOBAL MY-GLOBAL 123>")
                .GeneratesCodeMatching("RETURN MY-GLOBAL");
        }

        [TestMethod, TestCategory("NEW-PARSER?")]
        public void Words_Containing_Hash_Sign_Compile_Correctly_With_New_Parser()
        {
            AssertRoutine("", @"<PRINTB ,W?\#FOO>")
                .WithGlobal(VocabTests.SNewParserBootstrap)
                .WithGlobal(@"<SYNTAX \#FOO = V-FOO>")
                .WithGlobal("<ROUTINE V-FOO () <>>")
                .Outputs("#foo");
        }

        [TestMethod]
        public void Words_Containing_Hash_Sign_Compile_Correctly_With_Old_Parser()
        {
            AssertRoutine("", @"<PRINTB ,W?\#FOO>")
                .WithGlobal(@"<SYNTAX \#FOO = V-FOO>")
                .WithGlobal("<ROUTINE V-FOO () <>>")
                .Outputs("#foo");
        }

        [TestMethod]
        public void Properties_Containing_Backslash_Compile_Correctly()
        {
            AssertRoutine("", @"<PRINTN <GETP ,OBJ ,P?FOO\\BAR>>")
                .WithGlobal(@"<OBJECT OBJ (FOO\\BAR 123)>")
                .Outputs("123");
        }

        [TestMethod]
        public void Instructions_With_Debug_Line_Info_Should_Not_Be_Duplicated()
        {
            const string ArgSpec =
                @"""OPT"" W PS (P1 -1) ""AUX"" F";
            const string Body = @"
                <COND (<0? .W> <RFALSE>)>
                <SET F <GETB .W ,VOCAB-FL>>
                <SET F <COND (<BTST .F .PS>
                              <COND (<L? .P1 0>
                                     <RTRUE>)
                                    (<==? <BAND .F ,P1MASK> .P1>
                                     <GETB .W ,VOCAB-V1>)
                                    (ELSE <GETB .W ,VOCAB-V2>)>)>>
                .F";

            AssertRoutine(ArgSpec, Body)
                .WithGlobal("<CONSTANT P1MASK 255>")
                .WithGlobal("<CONSTANT VOCAB-FL 1>")
                .WithGlobal("<CONSTANT VOCAB-V1 2>")
                .WithGlobal("<CONSTANT VOCAB-V2 3>")
                .WithDebugInfo()
                .GeneratesCodeMatching(@"\.DEBUG-LINE")
                .AndNotMatching(@"(\.DEBUG-LINE ([^\r\n]*)\r?\n).*\1");
        }
    }
}
