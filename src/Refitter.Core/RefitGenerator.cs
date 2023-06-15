﻿using NSwag;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Refitter.Core;

public class RefitGenerator
{
    private readonly RefitGeneratorSettings settings;
    private readonly OpenApiDocument document;
    private readonly CSharpClientGeneratorFactory factory;

    private RefitGenerator(RefitGeneratorSettings settings, OpenApiDocument document)
    {
        this.settings = settings;
        this.document = document;
        factory = new CSharpClientGeneratorFactory(settings, document);
    }

    public static async Task<RefitGenerator> CreateAsync(RefitGeneratorSettings settings)
    {
        if (IsHttp(settings.OpenApiPath) && IsYaml(settings.OpenApiPath))
            return new RefitGenerator(settings, await OpenApiYamlDocument.FromUrlAsync(settings.OpenApiPath));
        if (IsHttp(settings.OpenApiPath))
            return new RefitGenerator(settings, await OpenApiDocument.FromUrlAsync(settings.OpenApiPath));
        if (IsYaml(settings.OpenApiPath))
            return new RefitGenerator(settings, await OpenApiYamlDocument.FromFileAsync(settings.OpenApiPath));
        return new RefitGenerator(settings, await OpenApiDocument.FromFileAsync(settings.OpenApiPath));
    }

    public string Generate()
    {
        var generator = factory.Create();
        var contracts = RefitInterfaceImports
            .GetImportedNamespaces(settings)
            .Aggregate(
                generator.GenerateFile(),
                (current, import) => current.Replace($"{import}.", string.Empty));

        var interfaceGenerator = new RefitInterfaceGenerator(settings, document, generator);
        var client = GenerateClient(interfaceGenerator);

        return new StringBuilder()
            .AppendLine(client)
            .AppendLine()
            .AppendLine(settings.GenerateContracts ? contracts : string.Empty)
            .ToString();
    }

    private string GenerateClient(RefitInterfaceGenerator interfaceGenerator)
    {
        var code = new StringBuilder();
        GenerateAutoGeneratedHeader(code);
        code.AppendLine(RefitInterfaceImports.GenerateNamespaceImports(settings))
            .AppendLine();

        code.AppendLine($$"""
            namespace {{settings.Namespace}}
            {
            {{interfaceGenerator.GenerateRefitInterface()}}
            }
            """);

        return code.ToString();
    }

    private void GenerateAutoGeneratedHeader(StringBuilder code)
    {
        if (!settings.AddAutoGeneratedHeader)
            return;

        code.AppendLine("""
            // <auto-generated>
            //     This code was generated by Refitter.
            // </auto-generated>

            """);
    }

    private static bool IsHttp(string path)
    {
        return path.StartsWith("http://") || path.StartsWith("https://");
    }

    private static bool IsYaml(string path)
    {
        return path.EndsWith("yaml") || path.EndsWith("yml");
    }
}