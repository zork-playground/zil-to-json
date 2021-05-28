﻿using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZilfAnalyzers.Test.Helpers;
using CodeFixVerifier = ZilfAnalyzers.Test.Helpers.CodeFixVerifier;

namespace ZilfAnalyzers.Test
{
    [TestClass, TestCategory("Analyzers")]
    public class MessageConstantTests : CodeFixVerifier
    {
        //No diagnostics expected to show up
        [TestMethod]
        [Timeout(10000)]
        public async Task MessageConstantAnalyzer_NotTriggered()
        {
            var test = @"";

            await VerifyCSharpDiagnosticAsync(test);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        [Timeout(10000)]
        public async Task MessageConstantAnalyzer_Triggered()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Zilf.Diagnostics;
using Zilf.Language;

namespace Zilf.Diagnostics {
    [AttributeUsage(AttributeTargets.Class)]
    class MessageSetAttribute : Attribute {
        public MessageSetAttribute(string s) {}
    }

    [AttributeUsage(AttributeTargets.Field)]
    class MessageAttribute : Attribute {
        public MessageAttribute(string s) {}
    }

    [MessageSet(""foo"")]
    class InterpreterMessages {
        [Message(""{0}: foo"")]
        public const int Foo = 1;
        [Message(""bar"")]
        public const int Bar = 1;
        [Message(""bar"")]
        public const int Baz = 2;
        [Message(""QU-UX?: quux {0}"")]
        public const int QUUX_Quux_0 = 2;
    }

    class InterpreterError : Exception {
        public InterpreterError(int code, params object[] args) {}
    }

    class Usage {
        void Main() {
            throw new InterpreterError(InterpreterMessages.QUUX_Quux_0);
            throw new InterpreterError(InterpreterMessages.QUUX_Quux_0, 123);
        }
    }
}
";
            var expected = new[]
            {
                new DiagnosticResult
                {
                    Id = "ZILF0002",
                    Message = "The code \'1\' is used more than once in message set \'InterpreterMessages\'",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new[] { new DiagnosticResultLocation("Test0.cs", 27, 26) }
                },
                new DiagnosticResult
                {
                    Id = "ZILF0003",
                    Message = "This format string is used more than once in message set \'InterpreterMessages\'",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new[] { new DiagnosticResultLocation("Test0.cs", 28, 18), new DiagnosticResultLocation("Test0.cs", 26, 18) }
                },
                new DiagnosticResult
                {
                    Id = "ZILF0004",
                    Message = "This format string has the prefix \'QU-UX?\', which should be moved to the call site",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new[] { new DiagnosticResultLocation("Test0.cs", 30, 18) }
                },
                new DiagnosticResult
                {
                    Id = "ZILF0002",
                    Message = "The code \'2\' is used more than once in message set \'InterpreterMessages\'",
                    Severity = DiagnosticSeverity.Warning,
                    Locations = new[] { new DiagnosticResultLocation("Test0.cs", 31, 26) }
                }
            };

            await VerifyCSharpDiagnosticAsync(test, expected);

            var fixtest = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Zilf.Diagnostics;
using Zilf.Language;

namespace Zilf.Diagnostics {
    [AttributeUsage(AttributeTargets.Class)]
    class MessageSetAttribute : Attribute {
        public MessageSetAttribute(string s) {}
    }

    [AttributeUsage(AttributeTargets.Field)]
    class MessageAttribute : Attribute {
        public MessageAttribute(string s) {}
    }

    [MessageSet(""foo"")]
    class InterpreterMessages {
        [Message(""{0}: foo"")]
        public const int Foo = 1;
        [Message(""bar"")]
        public const int Bar = 1;
        [Message(""bar"")]
        public const int Baz = 2;
        [Message(""{0}: quux {1}"")]
        public const int _0_Quux_1 = 2;
    }

    class InterpreterError : Exception {
        public InterpreterError(int code, params object[] args) {}
    }

    class Usage {
        void Main() {
            throw new InterpreterError(InterpreterMessages._0_Quux_1, ""QU-UX?"");
            throw new InterpreterError(InterpreterMessages._0_Quux_1, ""QU-UX?"", 123);
        }
    }
}
";

            await VerifyCSharpFixAsync(test, fixtest);
        }

        [NotNull]
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new MessageConstantAnalyzer();
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            throw new System.NotImplementedException();
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new MessageConstantCodeFixProvider();
        }

        protected override CodeFixProvider GetBasicCodeFixProvider()
        {
            throw new System.NotImplementedException();
        }
    }
}