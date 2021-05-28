using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ZilfAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [UsedImplicitly]
    public class MessageConstantAnalyzer : DiagnosticAnalyzer
    {
        static readonly DiagnosticDescriptor Rule_DuplicateMessageCode = new DiagnosticDescriptor(
            DiagnosticIds.DuplicateMessageCode,
            "Duplicate message code",
            "The code '{0}' is used more than once in message set '{1}'",
            "Error Reporting",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        static readonly DiagnosticDescriptor Rule_DuplicateMessageFormat = new DiagnosticDescriptor(
            DiagnosticIds.DuplicateMessageFormat,
            "Duplicate message format",
            "This format string is used more than once in message set '{0}'",
            "Error Reporting",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        static readonly DiagnosticDescriptor Rule_PrefixedMessageFormat = new DiagnosticDescriptor(
            DiagnosticIds.PrefixedMessageFormat,
            "Message has hardcoded prefix",
            "This format string has the prefix '{0}', which should be moved to the call site",
            "Error Reporting",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly Regex PrefixedMessageFormatRegex = new Regex(
            @"^(?<prefix>[^a-z .,;:()\[\]{}]+)(?<rest>: .*)$");
        public static readonly Regex FormatTokenRegex = new Regex(
            @"\{(?<number>\d+)(?<suffix>:[^}]*)?\}");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(
                Rule_DuplicateMessageCode,
                Rule_DuplicateMessageFormat,
                Rule_PrefixedMessageFormat);

        public override void Initialize([NotNull] AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ClassDeclaration);
        }

        static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;

            if (!IsMessageSet(classDecl, context.SemanticModel))
                return;

            var seenCodes = ImmutableHashSet<int>.Empty;
            var seenFormats = ImmutableDictionary<string, Location>.Empty;

            foreach (var field in GetConstIntFields(classDecl, context.SemanticModel))
            {
                foreach (var attr in field.DescendantNodes().OfType<AttributeSyntax>())
                {
                    if (IsMessageAttribute(context.SemanticModel, attr) && attr.ArgumentList.Arguments.Count >= 1)
                    {
                        var formatExpr = attr.ArgumentList.Arguments[0].Expression;
                        var constValue = context.SemanticModel.GetConstantValue(formatExpr);

                        if (!constValue.HasValue || !(constValue.Value is string formatStr))
                            continue;

                        // check for duplicate message
                        Diagnostic diagnostic;

                        if (seenFormats.ContainsKey(formatStr))
                        {
                            diagnostic = Diagnostic.Create(
                                Rule_DuplicateMessageFormat,
                                formatExpr.GetLocation(),
                                new[] { seenFormats[formatStr] },
                                classDecl.Identifier);

                            context.ReportDiagnostic(diagnostic);
                        }
                        else
                        {
                            seenFormats = seenFormats.Add(formatStr, formatExpr.GetLocation());
                        }

                        // check for prefixed message
                        var match = PrefixedMessageFormatRegex.Match(formatStr);

                        if (!match.Success)
                            continue;

                        diagnostic = Diagnostic.Create(
                            Rule_PrefixedMessageFormat,
                            formatExpr.GetLocation(),
                            match.Groups["prefix"].Value);

                        context.ReportDiagnostic(diagnostic);
                    }
                }

                foreach (var varDecl in field.Declaration.Variables)
                {
                    if (varDecl.Initializer != null)
                    {
                        var constValue = context.SemanticModel.GetConstantValue(varDecl.Initializer.Value);

                        if (!constValue.HasValue)
                            continue;

                        // check for duplicate code
                        var value = (int)constValue.Value;

                        if (seenCodes.Contains(value))
                        {
                            var diagnostic = Diagnostic.Create(
                                Rule_DuplicateMessageCode,
                                varDecl.GetLocation(),
                                varDecl.Initializer.Value,
                                classDecl.Identifier);

                            context.ReportDiagnostic(diagnostic);
                        }
                        else
                        {
                            seenCodes = seenCodes.Add(value);
                        }
                    }
                }
            }
        }

        public static bool IsMessageAttribute(SemanticModel semanticModel, [NotNull] AttributeSyntax attr)
        {
            var type = semanticModel.GetTypeInfo(attr).Type;

            while (type != null)
            {
                if (type.Name == "MessageAttribute")
                    return true;

                type = type.BaseType;
            }

            return false;
        }

        [NotNull]
        static IEnumerable<FieldDeclarationSyntax> GetConstIntFields([NotNull] ClassDeclarationSyntax classDecl, SemanticModel semanticModel)
        {
            return from field in classDecl.Members.OfType<FieldDeclarationSyntax>()
                   where field.Modifiers.Any(SyntaxKind.PublicKeyword) && field.Modifiers.Any(SyntaxKind.ConstKeyword)
                   where semanticModel.GetTypeInfo(field.Declaration.Type).Type?.SpecialType == SpecialType.System_Int32
                   select field;
        }

        static bool IsMessageSet([NotNull] ClassDeclarationSyntax classDecl, SemanticModel semanticModel)
        {
            var attributes = from alist in classDecl.AttributeLists
                             from attr in alist.Attributes
                             select semanticModel.GetTypeInfo(attr);

            return attributes.Any(ti =>
                ti.Type?.Name == "MessageSetAttribute" &&
                ti.Type?.ContainingNamespace.ToString() == "Zilf.Diagnostics");
        }
    }
}
