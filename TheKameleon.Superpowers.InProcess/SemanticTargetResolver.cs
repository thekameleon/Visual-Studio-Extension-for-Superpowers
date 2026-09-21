using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TheKameleon.Superpowers.Bridge.Contracts;

namespace TheKameleon.Superpowers.InProcess;

internal static class SemanticTargetResolver
{
    public static async Task<SemanticTargetInfo?> ResolveAsync(
        Solution solution,
        string filePath,
        SourceText capturedText,
        int position,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(filePath) || position < 0 || position >= capturedText.Length)
        {
            return null;
        }

        var documents = solution.Projects.SelectMany(project => project.Documents)
            .Where(candidate => string.Equals(candidate.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            .Take(2).ToArray();
        if (documents.Length != 1 || documents[0].Project.Language != LanguageNames.CSharp)
        {
            return null;
        }

        var document = documents[0];
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        if (!text.ContentEquals(capturedText))
        {
            return null;
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || model is null || position < 0 || position > root.FullSpan.End)
        {
            return null;
        }

        var node = root.FindToken(position).Parent;
        while (node is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var symbol = model.GetDeclaredSymbol(node, cancellationToken);
            if (symbol is INamedTypeSymbol or IMethodSymbol)
            {
                var line = node.GetLocation().GetLineSpan().StartLinePosition;
                return new SemanticTargetInfo
                {
                    Kind = symbol.Kind.ToString(),
                    Name = symbol.Name,
                    DisplayName = symbol.ToDisplayString(),
                    FilePath = document.FilePath,
                    StartLine = line.Line,
                    StartColumn = line.Character
                };
            }

            node = node.Parent;
        }

        return null;
    }
}
