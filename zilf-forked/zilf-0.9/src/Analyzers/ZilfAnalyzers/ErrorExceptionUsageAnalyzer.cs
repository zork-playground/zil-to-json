using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZilfAnalyzers
{
    // TODO: remove references to Workspaces types to resolve warning RS1022 (see https://github.com/dotnet/roslyn-sdk/issues/105)
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ErrorExceptionUsageAnalyzer : DiagnosticAnalyzer
    {
        const string Title = "Obsolete error format";
        const string MessageFormat = "This exception should use a diagnostic code instead";
        const string Category = "Error Reporting";

        static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticIds.ExceptionShouldUseDiagnosticCode, Title, MessageFormat,
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize([NotNull] AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ObjectCreationExpression);
        }

        static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var creationExpr = (ObjectCreationExpressionSyntax)context.Node;

            if (TryMatchLiteralCreation(creationExpr, context.SemanticModel, out _))
            {
                var diagnostic = Diagnostic.Create(Rule, creationExpr.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        public static bool TryMatchLiteralCreation(ObjectCreationExpressionSyntax creationExpr,
            SemanticModel semanticModel, [CanBeNull] out LiteralCreation literalCreation)
        {
            var typeSymbol = semanticModel.GetTypeInfo(creationExpr);
            var qualifiedFormat = new SymbolDisplayFormat(
              typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);
            var exceptionTypeName = typeSymbol.Type.ToDisplayString(qualifiedFormat);

            literalCreation = null;

            if (exceptionTypeName == null || !ZilfFacts.MessageTypeMap.ContainsKey(exceptionTypeName))
                return false;

            var args = creationExpr.ArgumentList.Arguments;

            if (args.Count < 1)
                return false;

            var argTypes = args.Select(a => semanticModel.GetTypeInfo(a.Expression).ConvertedType).ToArray();
            int i = 0;

            // ignore the (string, int, int) constructor
            if (argTypes.Length == 3 && argTypes[0]?.SpecialType == SpecialType.System_String &&
                argTypes[1]?.SpecialType == SpecialType.System_Int32 && argTypes[2]?.SpecialType == SpecialType.System_Int32)
            {
                return false;
            }

            // if the first argument is ISourceLine or IProvideSourceLine, it's the error source expr
            switch (argTypes[0]?.Name)
            {
                case "ISourceLine":
                case "IProvideSourceLine":

                    i++;
                    if (i >= argTypes.Length)
                        return false;

                    break;
            }

            // if the next argument is a string...
            if (argTypes[i]?.SpecialType == SpecialType.System_String)
            {
                // we got a winner!
                var expressionToReplace = args[i].Expression;

                if (TryExtractFormatAndArgs(expressionToReplace, semanticModel, out var newMessageFormat, out var newMessageArgs))
                {
                    literalCreation = new LiteralCreation(
                        exceptionTypeName: exceptionTypeName,
                        expressionToReplace: expressionToReplace,
                        newMessageFormat: newMessageFormat,
                        newMessageArgs: newMessageArgs);
                    return true;
                }
            }

            return false;
        }

        static bool TryExtractFormatAndArgs(
            ExpressionSyntax expressionToReplace, [NotNull] SemanticModel semanticModel,
            [CanBeNull] out string newMessageFormat, out ImmutableList<ExpressionSyntax> newMessageArgs)
        {
            string format;
            ImmutableList<ExpressionSyntax> newArgs;

            // has a constant value?
            var constantValue = semanticModel.GetConstantValue(expressionToReplace);

            if (constantValue.HasValue)
            {
                format = (string)constantValue.Value;
                newArgs = ImmutableList<ExpressionSyntax>.Empty;
            }
            else if (!TryUnpackCallToStringFormat(expressionToReplace, semanticModel, out format, out newArgs) &&
                !TryRewriteConcatAsFormatAndArgs(expressionToReplace, semanticModel, out format, out newArgs))
            {
                newMessageFormat = null;
                newMessageArgs = null;
                return false;
            }

            // prefixed?
            var match = MessageConstantAnalyzer.PrefixedMessageFormatRegex.Match(format);

            if (match.Success)
            {
                var prefix = match.Groups["prefix"].Value;
                var rest = match.Groups["rest"].Value;

                format = "{0}" + IncrementFormatTokens(rest);
                newArgs = newArgs.Insert(
                    0,
                    SyntaxFactory.LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Literal(prefix)));
            }

            newMessageFormat = format;
            newMessageArgs = newArgs;
            return true;
        }

        static bool IsSpecialTypeMethod([CanBeNull] ISymbol symbol, SpecialType specialType, string methodName)
        {
            return
                symbol?.Kind == SymbolKind.Method &&
                symbol.ContainingType?.SpecialType == specialType &&
                symbol.Name == methodName;
        }

        static bool TryUnpackCallToStringFormat(ExpressionSyntax expressionToReplace, SemanticModel semanticModel, out string formatStr, out ImmutableList<ExpressionSyntax> formatArgs)
        {
            formatStr = null;
            formatArgs = null;

            if (!(expressionToReplace is InvocationExpressionSyntax invocationExpr))
                return false;

            var invokedSymbol = semanticModel.GetSymbolInfo(invocationExpr.Expression);

            if (!IsSpecialTypeMethod(invokedSymbol.Symbol, SpecialType.System_String, "Format"))
                return false;

            var formatExpr = invocationExpr.ArgumentList.Arguments.FirstOrDefault()?.Expression;

            if (formatExpr == null || semanticModel.GetTypeInfo(formatExpr).Type?.SpecialType != SpecialType.System_String)
                return false;

            var formatConstValue = semanticModel.GetConstantValue(formatExpr);

            if (!formatConstValue.HasValue)
                return false;

            formatStr = (string)formatConstValue.Value;
            formatArgs = invocationExpr.ArgumentList.Arguments.Skip(1).Select(a => a.Expression).ToImmutableList();
            return true;
        }

        static bool TryRewriteConcatAsFormatAndArgs(ExpressionSyntax expressionToReplace, SemanticModel semanticModel, out string formatStr, out ImmutableList<ExpressionSyntax> formatArgs)
        {
            formatStr = null;
            formatArgs = null;

            if (!(expressionToReplace is BinaryExpressionSyntax binaryExpr) || binaryExpr.Kind() != SyntaxKind.AddExpression)
                return false;

            var sb = new StringBuilder();
            var argList = new List<ExpressionSyntax>();
            var tokenIdx = 0;

            foreach (var expr in UnravelAddExpressions(binaryExpr))
            {
                var constValue = semanticModel.GetConstantValue(expr);

                if (constValue.HasValue && constValue.Value is string constStr)
                {
                    sb.Append(constStr);
                }
                else
                {
                    sb.Append('{');
                    sb.Append(tokenIdx++);
                    sb.Append('}');

                    argList.Add(expr);
                }
            }

            formatStr = sb.ToString();
            formatArgs = argList.ToImmutableList();
            return true;
        }

        static IEnumerable<ExpressionSyntax> UnravelAddExpressions(ExpressionSyntax expr)
        {
            if (expr is BinaryExpressionSyntax binaryExpr &&
                binaryExpr.Kind() == SyntaxKind.AddExpression)
            {
                foreach (var child in UnravelAddExpressions(binaryExpr.Left))
                    yield return child;

                foreach (var child in UnravelAddExpressions(binaryExpr.Right))
                    yield return child;
            }
            else
            {
                yield return expr;
            }
        }

        [NotNull]
        public static string IncrementFormatTokens([NotNull] string format)
        {
            return MessageConstantAnalyzer.FormatTokenRegex.Replace(
                format,
                match =>
                    $"{{{int.Parse(match.Groups["number"].Value) + 1}{match.Groups["suffix"].Value}}}");
        }
    }

    public class LiteralCreation
    {
        /// <summary>
        /// The name of the exception being created, e.g. InterpreterError.
        /// </summary>
        public string ExceptionTypeName { get; }
        /// <summary>
        /// The expression to replace in the constructor arguments. Usually a <see cref="LiteralExpressionSyntax"/>.
        /// </summary>
        public ExpressionSyntax ExpressionToReplace { get; }
        /// <summary>
        /// The format string to be used for the new message.
        /// </summary>
        public string NewMessageFormat { get; }
        /// <summary>
        /// The initial arguments to be used at the call site when formatting the new message.
        /// </summary>
        public IImmutableList<ExpressionSyntax> NewMessageArgs { get; }

        public LiteralCreation(
            string exceptionTypeName,
            ExpressionSyntax expressionToReplace,
            string newMessageFormat, IImmutableList<ExpressionSyntax> newMessageArgs)
        {
            ExceptionTypeName = exceptionTypeName;
            ExpressionToReplace = expressionToReplace;
            NewMessageFormat = newMessageFormat;
            NewMessageArgs = newMessageArgs;
        }
    }
}
